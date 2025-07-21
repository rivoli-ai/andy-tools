using Andy.Tools.Core;

namespace Andy.Tools.Library.Common;

/// <summary>
/// Common result types and factory methods for tools.
/// </summary>
public static class ToolResults
{
    /// <summary>
    /// Creates a successful result with data.
    /// </summary>
    /// <param name="data">The result data.</param>
    /// <param name="message">Optional success message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <returns>A successful tool result.</returns>
    public static ToolResult Success(object? data = null, string? message = null, Dictionary<string, object?>? metadata = null)
    {
        var result = ToolResult.Success(data, metadata ?? []);
        if (!string.IsNullOrEmpty(message))
        {
            result.Metadata["message"] = message;
        }

        return result;
    }

    /// <summary>
    /// Creates a successful result with file information.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="message">Optional message.</param>
    /// <param name="additionalData">Additional data to include.</param>
    /// <returns>A successful tool result with file information.</returns>
    public static ToolResult FileSuccess(string filePath, string? message = null, Dictionary<string, object?>? additionalData = null)
    {
        var fileInfo = new FileInfo(filePath);
        var data = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["file_name"] = fileInfo.Name,
            ["file_size"] = fileInfo.Length,
            ["last_modified"] = fileInfo.LastWriteTimeUtc,
            ["exists"] = fileInfo.Exists
        };

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        return Success(data, message ?? $"File operation completed: {fileInfo.Name}");
    }

    /// <summary>
    /// Creates a successful result with directory information.
    /// </summary>
    /// <param name="directoryPath">The directory path.</param>
    /// <param name="message">Optional message.</param>
    /// <param name="additionalData">Additional data to include.</param>
    /// <returns>A successful tool result with directory information.</returns>
    public static ToolResult DirectorySuccess(string directoryPath, string? message = null, Dictionary<string, object?>? additionalData = null)
    {
        var dirInfo = new DirectoryInfo(directoryPath);
        var data = new Dictionary<string, object?>
        {
            ["directory_path"] = directoryPath,
            ["directory_name"] = dirInfo.Name,
            ["last_modified"] = dirInfo.LastWriteTimeUtc,
            ["exists"] = dirInfo.Exists
        };

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        return Success(data, message ?? $"Directory operation completed: {dirInfo.Name}");
    }

    /// <summary>
    /// Creates a successful result with a list of items.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The list of items.</param>
    /// <param name="message">Optional message.</param>
    /// <param name="totalCount">Optional total count if different from items count.</param>
    /// <returns>A successful tool result with list data.</returns>
    public static ToolResult ListSuccess<T>(IEnumerable<T> items, string? message = null, int? totalCount = null)
    {
        var itemList = items.ToList();
        var data = new Dictionary<string, object?>
        {
            ["items"] = itemList,
            ["count"] = itemList.Count,
            ["total_count"] = totalCount ?? itemList.Count
        };

        return Success(data, message ?? $"Found {itemList.Count} items");
    }

    /// <summary>
    /// Creates a successful result with text content.
    /// </summary>
    /// <param name="content">The text content.</param>
    /// <param name="contentType">The content type (e.g., "text/plain", "application/json").</param>
    /// <param name="message">Optional message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <returns>A successful tool result with text content.</returns>
    public static ToolResult TextSuccess(string content, string contentType = "text/plain", string? message = null, Dictionary<string, object?>? metadata = null)
    {
        var data = new Dictionary<string, object?>
        {
            ["content"] = content,
            ["content_type"] = contentType,
            ["length"] = content.Length,
            ["line_count"] = content.Split('\n').Length
        };

        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        return Success(data, message ?? "Text content processed successfully");
    }

    /// <summary>
    /// Creates a successful result for a web request.
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="content">The response content.</param>
    /// <param name="headers">Optional response headers.</param>
    /// <param name="message">Optional message.</param>
    /// <returns>A successful tool result with web request data.</returns>
    public static ToolResult WebSuccess(string url, int statusCode, string content, Dictionary<string, string>? headers = null, string? message = null)
    {
        var data = new Dictionary<string, object?>
        {
            ["url"] = url,
            ["status_code"] = statusCode,
            ["content"] = content,
            ["content_length"] = content.Length,
            ["response_time"] = DateTime.UtcNow
        };

        if (headers != null)
        {
            data["headers"] = headers;
        }

        return Success(data, message ?? $"Web request completed with status {statusCode}");
    }

    /// <summary>
    /// Creates a failure result with detailed error information.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="errorCode">Optional error code.</param>
    /// <param name="details">Optional error details.</param>
    /// <param name="innerException">Optional inner exception.</param>
    /// <returns>A failure tool result.</returns>
    public static ToolResult Failure(string error, string? errorCode = null, object? details = null, Exception? innerException = null)
    {
        var errors = new List<string> { error };

        if (innerException != null)
        {
            errors.Add($"Inner exception: {innerException.Message}");
        }

        var metadata = new Dictionary<string, object?>();

        if (errorCode != null)
        {
            metadata["error_code"] = errorCode;
        }

        if (details != null)
        {
            metadata["error_details"] = details;
        }

        if (innerException != null)
        {
            metadata["exception_type"] = innerException.GetType().Name;
            metadata["stack_trace"] = innerException.StackTrace;
        }

        var result = ToolResult.Failure(error, metadata);
        if (errors?.Count > 0)
        {
            result.Metadata["errors"] = errors;
        }

        return result;
    }

    /// <summary>
    /// Creates a failure result for file not found errors.
    /// </summary>
    /// <param name="filePath">The file path that was not found.</param>
    /// <returns>A failure tool result.</returns>
    public static ToolResult FileNotFound(string filePath)
    {
        return Failure(
            $"File not found: {filePath}",
            "FILE_NOT_FOUND",
            new { file_path = filePath, checked_at = DateTime.UtcNow }
        );
    }

    /// <summary>
    /// Creates a failure result for directory not found errors.
    /// </summary>
    /// <param name="directoryPath">The directory path that was not found.</param>
    /// <returns>A failure tool result.</returns>
    public static ToolResult DirectoryNotFound(string directoryPath)
    {
        return Failure(
            $"Directory not found: {directoryPath}",
            "DIRECTORY_NOT_FOUND",
            new { directory_path = directoryPath, checked_at = DateTime.UtcNow }
        );
    }

    /// <summary>
    /// Creates a failure result for access denied errors.
    /// </summary>
    /// <param name="resource">The resource that was denied access.</param>
    /// <param name="operation">The operation that was attempted.</param>
    /// <returns>A failure tool result.</returns>
    public static ToolResult AccessDenied(string resource, string operation = "access")
    {
        return Failure(
            $"Access denied: Cannot {operation} {resource}",
            "ACCESS_DENIED",
            new { resource, operation, attempted_at = DateTime.UtcNow }
        );
    }

    /// <summary>
    /// Creates a failure result for invalid parameter errors.
    /// </summary>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="value">The invalid value.</param>
    /// <param name="reason">The reason why it's invalid.</param>
    /// <returns>A failure tool result.</returns>
    public static ToolResult InvalidParameter(string parameterName, object? value, string reason)
    {
        return Failure(
            $"Invalid parameter '{parameterName}': {reason}",
            "INVALID_PARAMETER",
            new { parameter_name = parameterName, parameter_value = value?.ToString(), reason }
        );
    }

    /// <summary>
    /// Creates a failure result for network errors.
    /// </summary>
    /// <param name="url">The URL that failed.</param>
    /// <param name="error">The error message.</param>
    /// <param name="statusCode">Optional HTTP status code.</param>
    /// <returns>A failure tool result.</returns>
    public static ToolResult NetworkError(string url, string error, int? statusCode = null)
    {
        return Failure(
            $"Network error accessing {url}: {error}",
            "NETWORK_ERROR",
            new { url, error, status_code = statusCode, attempted_at = DateTime.UtcNow }
        );
    }

    /// <summary>
    /// Creates a failure result for timeout errors.
    /// </summary>
    /// <param name="operation">The operation that timed out.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>A failure tool result.</returns>
    public static ToolResult Timeout(string operation, TimeSpan timeout)
    {
        return Failure(
            $"Operation timed out: {operation} (timeout: {timeout.TotalSeconds:F1}s)",
            "TIMEOUT",
            new { operation, timeout_seconds = timeout.TotalSeconds, occurred_at = DateTime.UtcNow }
        );
    }
}
