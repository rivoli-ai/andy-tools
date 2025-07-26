using System.Net;
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
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

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

            // Parse headers
            var headers = ParseHeaders(headersObj);

            // Create HTTP client with custom settings
            using var handler = new HttpClientHandler()
            {
                UseCookies = false
            };

            if (!followRedirects)
            {
                handler.AllowAutoRedirect = false;
            }

            if (!validateSsl)
            {
                handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            }

            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };

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
                response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                return ToolResults.Timeout($"HTTP request to {url}", TimeSpan.FromSeconds(timeoutSeconds));
            }
            catch (HttpRequestException ex)
            {
                return ToolResults.NetworkError(url, ex.Message);
            }

            var responseTime = DateTime.UtcNow - startTime;

            ReportProgress(context, "Reading response content...", 70);

            // Check response size
            var contentLength = response.Content.Headers.ContentLength;
            var maxResponseSizeBytes = (long)(maxResponseSizeMb * 1024 * 1024);

            if (contentLength.HasValue && contentLength.Value > maxResponseSizeBytes)
            {
                return ToolResults.Failure(
                    $"Response too large ({ToolHelpers.FormatFileSize(contentLength.Value)}). Maximum allowed: {maxResponseSizeMb}MB",
                    "RESPONSE_TOO_LARGE"
                );
            }

            // Read response content
            string responseContent;
            try
            {
                var contentBytes = await response.Content.ReadAsByteArrayAsync(context.CancellationToken);

                if (contentBytes.Length > maxResponseSizeBytes)
                {
                    return ToolResults.Failure(
                        $"Response too large ({ToolHelpers.FormatFileSize(contentBytes.Length)}). Maximum allowed: {maxResponseSizeMb}MB",
                        "RESPONSE_TOO_LARGE"
                    );
                }

                responseContent = Encoding.UTF8.GetString(contentBytes);
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
                ["content_length"] = responseContent.Length,
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
