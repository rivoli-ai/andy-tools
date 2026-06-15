using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.Web;

/// <summary>
/// Tool for fetching a URL and returning readable text/markdown content.
/// Unlike <see cref="HttpRequestTool"/> (which returns the raw response body), this tool strips
/// HTML to human-readable text. It reuses the same hardened, SSRF-safe fetching approach:
/// a pooled <see cref="SocketsHttpHandler"/> whose <c>ConnectCallback</c> rejects private/local
/// addresses, a preflight host check, a streaming size cap, and charset-aware decoding.
/// </summary>
public class WebFetchTool : ToolBase
{
    /// <summary>Default maximum number of characters returned.</summary>
    private const int DefaultMaxLength = 100_000;

    /// <summary>Hard cap on bytes downloaded before HTML/text extraction (8 MB).</summary>
    private const long MaxDownloadBytes = 8L * 1024 * 1024;

    // Removes whole <script>...</script> and <style>...</style> blocks (including their content).
    private static readonly Regex ScriptStyleRegex = new(
        @"<(script|style)\b[^>]*>.*?</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Matches any remaining HTML/XML tag.
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Singleline | RegexOptions.Compiled);

    // Collapses runs of whitespace (within a line) to a single space.
    private static readonly Regex InlineWhitespaceRegex = new("[^\\S\n]+", RegexOptions.Compiled);

    // Collapses 3+ consecutive newlines down to a blank-line separator.
    private static readonly Regex MultiNewlineRegex = new("\n{3,}", RegexOptions.Compiled);

    // Pooled clients keyed by whether internal targets are permitted (mirrors HttpRequestTool).
    private static readonly ConcurrentDictionary<bool, HttpClient> Clients = new();

    private static HttpClient GetClient(bool allowInternal)
    {
        return Clients.GetOrAdd(allowInternal, static allow =>
        {
            var handler = new SocketsHttpHandler
            {
                UseCookies = false,
                AllowAutoRedirect = true,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                // Validate the actual address connected to on every connection (incl. redirect targets and
                // late DNS resolution), closing redirect-based and DNS-rebinding SSRF bypasses.
                ConnectCallback = async (ctx, ct) =>
                {
                    var targetHost = ctx.DnsEndPoint.Host;
                    var addresses = IPAddress.TryParse(targetHost, out var literal)
                        ? [literal]
                        : await Dns.GetHostAddressesAsync(targetHost, ct);

                    if (!allow && addresses.Any(ToolHelpers.IsPrivateOrLocalAddress))
                    {
                        throw new HttpRequestException(
                            $"Blocked connection to internal address for host '{targetHost}' (SSRF protection).");
                    }

                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(addresses, ctx.DnsEndPoint.Port, ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            };

            // Timeout is enforced per request via a linked token, so disable the client-level timeout.
            return new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(-1) };
        });
    }

    private static Encoding ResolveResponseEncoding(HttpResponseMessage response)
    {
        var charset = response.Content.Headers.ContentType?.CharSet;
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"', '\''));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8; // unknown/invalid charset -> fall back to UTF-8
        }
    }

    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "web_fetch",
        Name = "Web Fetch",
        Description = "Fetches a URL and returns its readable text content (HTML stripped to plain text or light markdown)",
        Version = "1.0.0",
        Category = ToolCategory.Web,
        RequiredPermissions = ToolPermissionFlags.Network,
        Parameters =
        [
            new()
            {
                Name = "url",
                Description = "The URL to fetch",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "max_length",
                Description = "Maximum number of characters to return (default: 100000)",
                Type = "number",
                Required = false,
                DefaultValue = DefaultMaxLength,
                MinValue = 1,
                MaxValue = 5_000_000
            },
            new()
            {
                Name = "format",
                Description = "Output format: 'text' for plain readable text, 'markdown' for light markdown (default: text)",
                Type = "string",
                Required = false,
                DefaultValue = "text",
                AllowedValues = ["text", "markdown"]
            },
            new()
            {
                Name = "timeout_seconds",
                Description = "Request timeout in seconds (default: 30)",
                Type = "number",
                Required = false,
                DefaultValue = 30,
                MinValue = 1,
                MaxValue = 300
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var url = GetParameter<string>(parameters, "url");
        var maxLength = GetParameter(parameters, "max_length", DefaultMaxLength);
        var format = GetParameter(parameters, "format", "text");
        var timeoutSeconds = GetParameter<double>(parameters, "timeout_seconds", 30);

        if (maxLength < 1)
        {
            maxLength = DefaultMaxLength;
        }

        try
        {
            if (!ToolHelpers.IsValidUrl(url))
            {
                return ToolResults.InvalidParameter("url", url, "Invalid URL format");
            }

            ReportProgress(context, "Preparing fetch...", 10);

            // SSRF protection: by default, block requests whose target resolves to an internal/non-public
            // address. Callers may opt in via the allow_private_networks / allow_localhost permissions.
            var allowInternal = context.Permissions.CustomPermissions.ContainsKey("allow_private_networks")
                || context.Permissions.CustomPermissions.ContainsKey("allow_localhost");

            if (!allowInternal)
            {
                var requestHost = new Uri(url).Host;
                if (await ToolHelpers.IsBlockedInternalHostAsync(requestHost, context.CancellationToken))
                {
                    return ToolResults.Failure(
                        $"Request to '{requestHost}' is blocked: it resolves to an internal/non-public address (SSRF protection). Grant the 'allow_private_networks' permission to override.",
                        "BLOCKED_INTERNAL_HOST");
                }
            }

            var httpClient = GetClient(allowInternal);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeoutCts.Token);
            var requestToken = linkedCts.Token;

            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            ReportProgress(context, $"Fetching {url}...", 30);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
            {
                return ToolResults.Timeout($"Fetch of {url}", TimeSpan.FromSeconds(timeoutSeconds));
            }
            catch (HttpRequestException ex)
            {
                return ToolResults.NetworkError(url, ex.Message);
            }

            using (response)
            {
                ReportProgress(context, "Reading content...", 60);

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value > MaxDownloadBytes)
                {
                    return ToolResults.Failure(
                        $"Response too large ({ToolHelpers.FormatFileSize(contentLength.Value)}). Maximum allowed: {ToolHelpers.FormatFileSize(MaxDownloadBytes)}",
                        "RESPONSE_TOO_LARGE");
                }

                string rawContent;
                try
                {
                    await using var responseStream = await response.Content.ReadAsStreamAsync(requestToken);
                    using var buffer = new MemoryStream();
                    var chunk = new byte[81920];
                    int read;
                    while ((read = await responseStream.ReadAsync(chunk, requestToken)) > 0)
                    {
                        if (buffer.Length + read > MaxDownloadBytes)
                        {
                            return ToolResults.Failure(
                                $"Response too large (exceeds {ToolHelpers.FormatFileSize(MaxDownloadBytes)}). Download aborted.",
                                "RESPONSE_TOO_LARGE");
                        }

                        buffer.Write(chunk, 0, read);
                    }

                    rawContent = ResolveResponseEncoding(response).GetString(buffer.ToArray());
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
                {
                    return ToolResults.Timeout($"Fetch of {url}", TimeSpan.FromSeconds(timeoutSeconds));
                }
                catch (Exception ex)
                {
                    return ToolResults.Failure($"Failed to read response content: {ex.Message}", "RESPONSE_READ_ERROR");
                }

                ReportProgress(context, "Extracting readable content...", 85);

                var contentType = response.Content.Headers.ContentType?.ToString();
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
                var isHtml = mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                    || (string.IsNullOrEmpty(mediaType) && LooksLikeHtml(rawContent));

                var extracted = isHtml ? ExtractReadableText(rawContent) : rawContent;
                var usedFormat = string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase) ? "markdown" : "text";

                var truncated = extracted.Length > maxLength;
                if (truncated)
                {
                    extracted = extracted[..maxLength];
                }

                ReportProgress(context, "Fetch completed", 100);

                var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;

                var data = new Dictionary<string, object?>
                {
                    ["content"] = extracted,
                    ["final_url"] = finalUrl,
                    ["status_code"] = (int)response.StatusCode,
                    ["content_type"] = contentType,
                    ["format"] = usedFormat,
                    ["is_html"] = isHtml,
                    ["truncated"] = truncated,
                    ["content_length"] = extracted.Length
                };

                var metadata = new Dictionary<string, object?>
                {
                    ["final_url"] = finalUrl,
                    ["status_code"] = (int)response.StatusCode,
                    ["content_type"] = contentType,
                    ["format"] = usedFormat,
                    ["is_html"] = isHtml,
                    ["truncated"] = truncated
                };

                var message = response.IsSuccessStatusCode
                    ? $"Fetched {finalUrl} ({extracted.Length} chars{(truncated ? ", truncated" : "")})"
                    : $"Fetched {finalUrl} with status {(int)response.StatusCode}";

                return ToolResults.Success(data, message, metadata);
            }
        }
        catch (UriFormatException)
        {
            return ToolResults.InvalidParameter("url", url, "Invalid URL format");
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Web fetch failed: {ex.Message}", "WEB_FETCH_ERROR", details: ex);
        }
    }

    private static bool LooksLikeHtml(string content)
    {
        var head = content.Length > 1024 ? content[..1024] : content;
        return head.Contains("<html", StringComparison.OrdinalIgnoreCase)
            || head.Contains("<!doctype html", StringComparison.OrdinalIgnoreCase)
            || head.Contains("<body", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Strips HTML to readable plain text: removes script/style blocks, converts block-level tags to
    /// line breaks, strips all remaining tags, decodes HTML entities, and collapses whitespace.
    /// This is a lightweight extraction (no DOM parsing); the same output is used for both the 'text'
    /// and 'markdown' formats (a clean text extraction is the documented markdown level).
    /// </summary>
    private static string ExtractReadableText(string html)
    {
        // Drop script/style blocks entirely.
        var working = ScriptStyleRegex.Replace(html, " ");

        // Turn common block-level/line-break tags into newlines so structure survives tag stripping.
        working = Regex.Replace(
            working,
            @"<\s*(br|/p|/div|/li|/h[1-6]|/tr|/table|/ul|/ol|/section|/article|/header|/footer)\s*[^>]*>",
            "\n",
            RegexOptions.IgnoreCase);

        // Strip all remaining tags.
        working = TagRegex.Replace(working, "");

        // Decode HTML entities (&amp;, &lt;, &#39;, etc.).
        working = WebUtility.HtmlDecode(working);

        // Normalize line endings and collapse whitespace.
        working = working.Replace("\r\n", "\n").Replace('\r', '\n');
        working = InlineWhitespaceRegex.Replace(working, " ");

        // Trim trailing/leading spaces on each line.
        var lines = working.Split('\n').Select(l => l.Trim());
        working = string.Join("\n", lines);

        working = MultiNewlineRegex.Replace(working, "\n\n");

        return working.Trim();
    }
}
