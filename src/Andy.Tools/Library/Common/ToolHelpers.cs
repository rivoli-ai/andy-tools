using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Andy.Tools.Core;
using SystemDiagnostics = System.Diagnostics;
using SystemNet = System.Net;

namespace Andy.Tools.Library.Common;

/// <summary>
/// Helper utilities for tool implementations.
/// </summary>
public static class ToolHelpers
{
    /// <summary>
    /// Safely converts a path to an absolute path, ensuring it exists within allowed boundaries.
    /// </summary>
    /// <param name="path">The input path.</param>
    /// <param name="workingDirectory">The working directory context.</param>
    /// <param name="allowAbsolutePaths">Whether to allow absolute paths.</param>
    /// <returns>The absolute path if valid.</returns>
    /// <exception cref="ArgumentException">Thrown when path is invalid or outside boundaries.</exception>
    public static string GetSafePath(string path, string? workingDirectory = null, bool allowAbsolutePaths = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            // Handle relative paths
            if (!Path.IsPathRooted(path))
            {
                var baseDir = workingDirectory ?? Directory.GetCurrentDirectory();
                path = Path.Combine(baseDir, path);
            }
            else if (!allowAbsolutePaths)
            {
                throw new ArgumentException("Absolute paths are not allowed in this context");
            }

            // Get the full path and normalize it
            var fullPath = Path.GetFullPath(path);

            // Security check: ensure the path doesn't escape the working directory if specified
            if (workingDirectory != null)
            {
                var normalizedWorkingDir = Path.GetFullPath(workingDirectory);
                if (!fullPath.StartsWith(normalizedWorkingDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Path '{path}' is outside the allowed working directory");
                }
            }

            return fullPath;
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new ArgumentException($"Invalid path '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if a path is within the allowed paths specified in permissions.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="permissions">The tool permissions containing allowed paths.</param>
    /// <returns>True if the path is allowed, false otherwise.</returns>
    public static bool IsPathWithinAllowedPaths(string path, ToolPermissions permissions)
    {
        if (permissions.AllowedPaths == null || permissions.AllowedPaths.Count == 0)
        {
            // If no specific paths are configured, allow all paths (if FileSystemAccess is true)
            return permissions.FileSystemAccess;
        }

        try
        {
            var normalizedPath = Path.GetFullPath(path);

            foreach (var allowedPath in permissions.AllowedPaths)
            {
                var normalizedAllowedPath = Path.GetFullPath(allowedPath);
                if (normalizedPath.StartsWith(normalizedAllowedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates that a directory exists and is accessible.
    /// </summary>
    /// <param name="directoryPath">The directory path to validate.</param>
    /// <param name="requireWriteAccess">Whether write access is required.</param>
    /// <returns>True if directory is valid and accessible.</returns>
    public static bool IsValidDirectory(string directoryPath, bool requireWriteAccess = false)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return false;
            }

            // Test read access
            _ = Directory.GetFiles(directoryPath);

            // Test write access if required
            if (requireWriteAccess)
            {
                var testFile = Path.Combine(directoryPath, $".test_{Guid.NewGuid():N}");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Safely reads text from a file with encoding detection.
    /// </summary>
    /// <param name="filePath">The file path to read.</param>
    /// <param name="encoding">Optional encoding, will auto-detect if null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file content as text.</returns>
    public static async Task<string> ReadTextFileAsync(string filePath, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        encoding ??= DetectEncoding(filePath);
        return await File.ReadAllTextAsync(filePath, encoding, cancellationToken);
    }

    /// <summary>
    /// Safely writes text to a file with backup creation.
    /// </summary>
    /// <param name="filePath">The file path to write.</param>
    /// <param name="content">The content to write.</param>
    /// <param name="encoding">The encoding to use.</param>
    /// <param name="createBackup">Whether to create a backup of existing file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WriteTextFileAsync(string filePath, string content, Encoding? encoding = null, bool createBackup = true, CancellationToken cancellationToken = default)
    {
        encoding ??= Encoding.UTF8;

        // Create backup if file exists and backup is requested
        if (createBackup && File.Exists(filePath))
        {
            var backupPath = $"{filePath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Copy(filePath, backupPath, true);
        }

        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, content, encoding, cancellationToken);
    }

    /// <summary>
    /// Detects the encoding of a text file.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The detected encoding.</returns>
    public static Encoding DetectEncoding(string filePath)
    {
        var buffer = new byte[4];
        using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var bytesRead = file.Read(buffer, 0, 4);
        if (bytesRead < 4)
        {
            Array.Resize(ref buffer, bytesRead);
        }

        // Check for BOM
        if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            return Encoding.UTF8;
        }

        if (bytesRead >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
        {
            if (bytesRead >= 4 && buffer[2] == 0x00 && buffer[3] == 0x00)
            {
                return Encoding.UTF32; // UTF-32 LE
            }

            return Encoding.Unicode; // UTF-16 LE
        }

        if (bytesRead >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode; // UTF-16 BE
        }

        return Encoding.UTF8; // Default to UTF-8
    }

    /// <summary>
    /// Formats file size in human-readable format.
    /// </summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>Formatted size string.</returns>
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Safely parses JSON with error handling.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="json">The JSON string.</param>
    /// <param name="options">JSON serializer options.</param>
    /// <returns>The parsed object or null if parsing fails.</returns>
    public static T? TryParseJson<T>(string json, JsonSerializerOptions? options = null)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Serializes an object to JSON with error handling.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="options">JSON serializer options.</param>
    /// <returns>The JSON string or error message.</returns>
    public static string ToJson(object? obj, JsonSerializerOptions? options = null)
    {
        try
        {
            options ??= new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(obj, options);
        }
        catch (Exception ex)
        {
            return $"JSON serialization error: {ex.Message}";
        }
    }

    /// <summary>
    /// Escapes a string for safe use in regex patterns.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The escaped string.</returns>
    public static string EscapeRegex(string input)
    {
        return Regex.Escape(input);
    }

    /// <summary>
    /// Truncates text to a maximum length with ellipsis.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <param name="ellipsis">The ellipsis string.</param>
    /// <returns>The truncated text.</returns>
    public static string TruncateText(string text, int maxLength, string ellipsis = "...")
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        var truncateLength = Math.Max(0, maxLength - ellipsis.Length);
        return text[..truncateLength] + ellipsis;
    }

    /// <summary>
    /// Gets a temporary file path with optional extension.
    /// </summary>
    /// <param name="extension">The file extension (including dot).</param>
    /// <param name="directory">Optional directory, uses temp directory if null.</param>
    /// <returns>A unique temporary file path.</returns>
    public static string GetTempFilePath(string? extension = null, string? directory = null)
    {
        directory ??= Path.GetTempPath();
        var fileName = $"andy_tool_{Guid.NewGuid():N}{extension}";
        return Path.Combine(directory, fileName);
    }

    /// <summary>
    /// Validates an email address format.
    /// </summary>
    /// <param name="email">The email address to validate.</param>
    /// <returns>True if email format is valid.</returns>
    public static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new SystemNet.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a URL format.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>True if URL format is valid.</returns>
    public static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Measures execution time of an operation.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="operation">The operation to measure.</param>
    /// <returns>A tuple containing the result and execution time.</returns>
    public static async Task<(T Result, TimeSpan Duration)> MeasureAsync<T>(Func<Task<T>> operation)
    {
        var stopwatch = SystemDiagnostics.Stopwatch.StartNew();
        var result = await operation();
        stopwatch.Stop();
        return (result, stopwatch.Elapsed);
    }

    /// <summary>
    /// Retries an operation with exponential backoff.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="operation">The operation to retry.</param>
    /// <param name="maxRetries">Maximum number of retries.</param>
    /// <param name="baseDelay">Base delay between retries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        CancellationToken cancellationToken = default)
    {
        baseDelay ??= TimeSpan.FromMilliseconds(100);
        var attempt = 0;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception) when (attempt < maxRetries)
            {
                attempt++;
                var delay = TimeSpan.FromMilliseconds(baseDelay.Value.TotalMilliseconds * Math.Pow(2, attempt - 1));
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
