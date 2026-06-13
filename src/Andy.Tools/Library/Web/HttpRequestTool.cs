using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.Web;

/// <summary>
/// Tool for making HTTP requests.
/// </summary>
public class HttpRequestTool : ToolBase
{
    // Reuse a small set of pooled clients keyed by the settings that affect the handler, instead of
    // allocating and disposing an HttpClient/handler per request (which exhausts sockets under load).
    // Per-request timeout is applied via a linked CancellationTokenSource, not HttpClient.Timeout.
    private static readonly ConcurrentDictionary<(bool FollowRedirects, bool ValidateSsl, bool AllowInternal), HttpClient> Clients = new();

    private static HttpClient GetClient(bool followRedirects, bool validateSsl, bool allowInternal)
    {
        return Clients.GetOrAdd((followRedirects, validateSsl, allowInternal), static key =>
        {
            var handler = new SocketsHttpHandler
            {
                UseCookies = false,
                AllowAutoRedirect = key.FollowRedirects,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                // Validate the actual address connected to on every connection (incl. redirect targets and
                // late DNS resolution), closing redirect-based and DNS-rebinding SSRF bypasses.
                ConnectCallback = async (ctx, ct) =>
                {
                    var targetHost = ctx.DnsEndPoint.Host;
                    var addresses = IPAddress.TryParse(targetHost, out var literal)
                        ? [literal]
                        : await Dns.GetHostAddressesAsync(targetHost, ct);

                    if (!key.AllowInternal && addresses.Any(ToolHelpers.IsPrivateOrLocalAddress))
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

            if (!key.ValidateSsl)
            {
                handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            }

            // Timeout is enforced per request via a linked token, so disable the client-level timeout
            // (-1 ms == System.Threading.Timeout.InfiniteTimeSpan).
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
        Id = "http_request",
        Name = "HTTP Request",
        Description = "Makes HTTP requests to web APIs and returns the response",
        Version = "1.0.0",
        Category = ToolCategory.Web,
        RequiredPermissions = ToolPermissionFlags.Network,
        Parameters =
        [
            new()
            {
                Name = "url",
                Description = "The URL to make the request to",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "method",
                Description = "The HTTP method to use",
                Type = "string",
                Required = false,
                DefaultValue = "GET",
                AllowedValues = ["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"]
            },
            new()
            {
                Name = "headers",
                Description = "HTTP headers to include (JSON object)",
                Type = "object",
                Required = false
            },
            new()
            {
                Name = "body",
                Description = "Request body content",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "content_type",
                Description = "Content-Type header value",
                Type = "string",
                Required = false,
                AllowedValues =
                [
                    "application/json", "application/x-www-form-urlencoded",
                    "text/plain", "text/html", "text/xml", "application/xml"
                ]
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
            },
            new()
            {
                Name = "follow_redirects",
                Description = "Whether to automatically follow redirects (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "validate_ssl",
                Description = "Whether to validate SSL certificates (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "include_response_headers",
                Description = "Whether to include response headers in result (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "max_response_size_mb",
                Description = "Maximum response size in MB (default: 10MB)",
                Type = "number",
                Required = false,
                DefaultValue = 10,
                MinValue = 0.1,
                MaxValue = 100
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var url = GetParameter<string>(parameters, "url");
        var method = GetParameter(parameters, "method", "GET");
        var headersObj = GetParameter<object>(parameters, "headers");
        var body = GetParameter<string>(parameters, "body");
        var contentType = GetParameter<string>(parameters, "content_type");
        var timeoutSeconds = GetParameter<double>(parameters, "timeout_seconds", 30);
        var followRedirects = GetParameter(parameters, "follow_redirects", true);
        var validateSsl = GetParameter(parameters, "validate_ssl", true);
        var includeResponseHeaders = GetParameter(parameters, "include_response_headers", true);
        var maxResponseSizeMb = GetParameter<double>(parameters, "max_response_size_mb", 10);

        try
        {
            // Validate URL
            if (!ToolHelpers.IsValidUrl(url))
            {
                return ToolResults.InvalidParameter("url", url, "Invalid URL format");
            }

            ReportProgress(context, "Preparing HTTP request...", 10);

            // SSRF protection: by default, block requests whose target resolves to an internal/non-public
            // address (loopback, link-local, ULA, CGNAT, private, multicast). Callers may opt in to
            // internal targets via the allow_private_networks / allow_localhost custom permissions.
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

            // Parse headers
            var headers = ParseHeaders(headersObj);

            // Reuse a pooled client for this (followRedirects, validateSsl, allowInternal) combination.
            var httpClient = GetClient(followRedirects, validateSsl, allowInternal);

            // Enforce the per-request timeout via a linked token (the shared client has no timeout).
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeoutCts.Token);
            var requestToken = linkedCts.Token;

            // Create request
            var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), url);

            // Add headers
            foreach (var header in headers)
            {
                if (string.Equals(header.Key, "content-type", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Will be set with content
                }

                try
                {
                    request.Headers.Add(header.Key, header.Value);
                }
                catch (Exception)
                {
                    // Some headers can't be added this way, skip them
                }
            }

            // Add body if provided
            if (!string.IsNullOrEmpty(body))
            {
                var encoding = Encoding.UTF8;
                var mediaType = contentType ?? "text/plain";
                request.Content = new StringContent(body, encoding, mediaType);
            }

            ReportProgress(context, $"Making {method} request to {url}...", 30);

            // Make the request
            var startTime = DateTime.UtcNow;
            HttpResponseMessage response;

            try
            {
                response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
            {
                return ToolResults.Timeout($"HTTP request to {url}", TimeSpan.FromSeconds(timeoutSeconds));
            }
            catch (HttpRequestException ex)
            {
                return ToolResults.NetworkError(url, ex.Message);
            }

            var responseTime = DateTime.UtcNow - startTime;

            ReportProgress(context, "Reading response content...", 70);

            var maxResponseSizeBytes = (long)(maxResponseSizeMb * 1024 * 1024);

            // Fast-fail when the server advertises an oversized body.
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > maxResponseSizeBytes)
            {
                return ToolResults.Failure(
                    $"Response too large ({ToolHelpers.FormatFileSize(contentLength.Value)}). Maximum allowed: {maxResponseSizeMb}MB",
                    "RESPONSE_TOO_LARGE"
                );
            }

            // Stream the body and abort as soon as the cap is exceeded, rather than buffering an
            // unbounded (e.g. chunked) response fully into memory before checking the size.
            string responseContent;
            byte[] contentBytes;
            try
            {
                await using var responseStream = await response.Content.ReadAsStreamAsync(requestToken);
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await responseStream.ReadAsync(chunk, requestToken)) > 0)
                {
                    if (buffer.Length + read > maxResponseSizeBytes)
                    {
                        return ToolResults.Failure(
                            $"Response too large (exceeds {maxResponseSizeMb}MB). Download aborted.",
                            "RESPONSE_TOO_LARGE"
                        );
                    }

                    buffer.Write(chunk, 0, read);
                }

                contentBytes = buffer.ToArray();

                // Honor the response charset instead of assuming UTF-8.
                responseContent = ResolveResponseEncoding(response).GetString(contentBytes);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
            {
                return ToolResults.Timeout($"HTTP request to {url}", TimeSpan.FromSeconds(timeoutSeconds));
            }
            catch (Exception ex)
            {
                return ToolResults.Failure($"Failed to read response content: {ex.Message}", "RESPONSE_READ_ERROR");
            }

            ReportProgress(context, "HTTP request completed", 100);

            // Build response data
            var responseData = new Dictionary<string, object?>
            {
                ["status_code"] = (int)response.StatusCode,
                ["status_description"] = response.ReasonPhrase,
                ["success"] = response.IsSuccessStatusCode,
                ["content"] = responseContent,
                ["content_length"] = contentBytes.Length,
                ["content_char_length"] = responseContent.Length,
                ["content_type"] = response.Content.Headers.ContentType?.ToString(),
                ["response_time_ms"] = responseTime.TotalMilliseconds,
                ["url"] = url,
                ["method"] = method.ToUpperInvariant()
            };

            if (includeResponseHeaders)
            {
                var responseHeaders = new Dictionary<string, string>();

                foreach (var header in response.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                foreach (var header in response.Content.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                responseData["headers"] = responseHeaders;
            }

            var metadata = new Dictionary<string, object?>
            {
                ["request_method"] = method.ToUpperInvariant(),
                ["request_url"] = url,
                ["response_time_ms"] = responseTime.TotalMilliseconds,
                ["follow_redirects"] = followRedirects,
                ["validate_ssl"] = validateSsl,
                ["timeout_seconds"] = timeoutSeconds,
                ["request_time"] = startTime
            };

            if (!string.IsNullOrEmpty(body))
            {
                metadata["request_body_length"] = body.Length;
            }

            var message = response.IsSuccessStatusCode
                ? $"HTTP {method} request successful ({response.StatusCode})"
                : $"HTTP {method} request completed with status {response.StatusCode}";

            return ToolResults.WebSuccess(url, (int)response.StatusCode, responseContent,
                includeResponseHeaders ? responseData["headers"] as Dictionary<string, string> : null, message);
        }
        catch (UriFormatException)
        {
            return ToolResults.InvalidParameter("url", url, "Invalid URL format");
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"HTTP request failed: {ex.Message}", "HTTP_REQUEST_ERROR", details: ex);
        }
    }

    private static Dictionary<string, string> ParseHeaders(object? headersObj)
    {
        var headers = new Dictionary<string, string>();

        if (headersObj == null)
        {
            return headers;
        }

        try
        {
            if (headersObj is Dictionary<string, object?> dict)
            {
                foreach (var kvp in dict)
                {
                    if (kvp.Value != null)
                    {
                        headers[kvp.Key] = kvp.Value.ToString() ?? "";
                    }
                }
            }
            else if (headersObj is string jsonString)
            {
                var parsedHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
                if (parsedHeaders != null)
                {
                    foreach (var kvp in parsedHeaders)
                    {
                        headers[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch
        {
            // If parsing fails, return empty headers
        }

        return headers;
    }
}
