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
    /// UTF-8 encoding that does NOT emit a byte-order mark.
    /// <para>
    /// The framework's <see cref="Encoding.UTF8"/> singleton is configured with
    /// <c>encoderShouldEmitUTF8Identifier: true</c>, so passing it to
    /// <see cref="File.WriteAllTextAsync(string, string?, Encoding, CancellationToken)"/>
    /// prepends an <c>EF BB BF</c> BOM. For source files that originally had no BOM
    /// that silently corrupts the file (a spurious leading character that breaks
    /// diffs, patches, and tooling). Use this instance for all UTF-8 file writes so
    /// non-BOM files stay non-BOM.
    /// </para>
    /// </summary>
    public static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// The <see cref="StringComparison"/> to use when comparing filesystem paths on the current OS.
    /// Windows and macOS default to case-insensitive filesystems; Linux is case-sensitive. Using the
    /// OS-appropriate comparison avoids both over-permitting (treating distinct paths as equal on a
    /// case-sensitive FS) and under-blocking on a case-insensitive FS.
    /// </summary>
    public static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>
    /// Determines whether <paramref name="candidatePath"/> is the same as, or contained within,
    /// <paramref name="boundaryPath"/>, using a directory-separator-aware comparison.
    /// <para>
    /// A bare prefix test (<c>candidate.StartsWith(boundary)</c>) is unsafe: with a boundary of
    /// <c>/home/user/app</c> a sibling path <c>/home/user/app-secrets/x</c> shares the textual prefix
    /// yet is a different directory. This method requires either exact equality with the boundary or a
    /// match up to and including a directory separator, closing that escape.
    /// </para>
    /// </summary>
    /// <param name="candidatePath">The path to test. It is fully resolved before comparison.</param>
    /// <param name="boundaryPath">The boundary directory. It is fully resolved before comparison.</param>
    /// <returns><c>true</c> if the candidate is the boundary itself or lies beneath it.</returns>
    public static bool IsPathWithinBoundary(string candidatePath, string boundaryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryPath);

        var candidate = Path.GetFullPath(candidatePath);
        var boundary = Path.GetFullPath(boundaryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (candidate.Equals(boundary, PathComparison))
        {
            return true;
        }

        return candidate.StartsWith(boundary + Path.DirectorySeparatorChar, PathComparison);
    }

    /// <summary>
    /// Resolves a path to its real, symlink-free location so that confinement checks cannot be
    /// bypassed by a symbolic link inside an allowed directory that points outside it.
    /// <para>
    /// <see cref="Path.GetFullPath(string)"/> collapses <c>..</c> segments but does <b>not</b> resolve
    /// symbolic links. This walks the path component-by-component from the root, following any link
    /// target (including intermediate symlinked directories). Components that do not exist yet (e.g. a
    /// file about to be created) are resolved relative to their nearest existing, canonicalized ancestor.
    /// </para>
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The canonical absolute path. On any resolution error the best-effort full path is returned.</returns>
    public static string ResolveRealPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ResolveRealPathCore(Path.GetFullPath(path), depth: 0);
    }

    private static string ResolveRealPathCore(string full, int depth)
    {
        // Guard against symlink loops (ResolveLinkTarget also throws on cycles, but bound recursion too).
        if (depth > 40)
        {
            return full;
        }

        try
        {
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root))
            {
                return full;
            }

            var parts = full[root.Length..].Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            var current = root;
            for (var i = 0; i < parts.Length; i++)
            {
                current = Path.Combine(current, parts[i]);

                // Only existing entries can be reparse points; non-existent tail segments are appended literally.
                FileSystemInfo? info =
                    Directory.Exists(current) ? new DirectoryInfo(current)
                    : File.Exists(current) ? new FileInfo(current)
                    : null;

                var target = info?.ResolveLinkTarget(returnFinalTarget: true);
                if (target == null)
                {
                    continue;
                }

                // The target may itself sit under symlinked ancestors, so canonicalize it fully, then
                // resolve any remaining components relative to the canonical target.
                var resolvedTarget = ResolveRealPathCore(Path.GetFullPath(target.FullName), depth + 1);
                for (var j = i + 1; j < parts.Length; j++)
                {
                    resolvedTarget = Path.Combine(resolvedTarget, parts[j]);
                }

                return i + 1 < parts.Length
                    ? ResolveRealPathCore(resolvedTarget, depth + 1)
                    : resolvedTarget;
            }

            return current;
        }
        catch
        {
            // Symlink loops or permission errors: fall back to the non-resolved full path.
            return full;
        }
    }

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

            // Security check: ensure the path doesn't escape the working directory if specified.
            // Compare the *real* (symlink-resolved) paths so a symlink inside the working directory
            // that points outside it cannot be used to escape. The unresolved fullPath is still what
            // callers receive, but it can only be returned once its real target is confirmed inside.
            if (workingDirectory != null)
            {
                if (!IsPathWithinBoundary(ResolveRealPath(fullPath), ResolveRealPath(workingDirectory)))
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
            var normalizedPath = ResolveRealPath(path);

            foreach (var allowedPath in permissions.AllowedPaths)
            {
                if (IsPathWithinBoundary(normalizedPath, ResolveRealPath(allowedPath)))
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
        encoding ??= Utf8NoBom;

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

        // Check for BOM. Only when the file actually starts with a UTF-8 BOM do we
        // return the BOM-emitting Encoding.UTF8, so a rewrite preserves it. Files
        // without a BOM fall through to Utf8NoBom below and stay BOM-free.
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

        return Utf8NoBom; // Default to UTF-8 without a BOM
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
    /// Matches a name against a shell-style glob containing <c>*</c> (any run) and <c>?</c> (any single
    /// char). All other characters are treated literally — unlike a naive <c>pattern.Replace("*", ".*")</c>,
    /// which leaves regex metacharacters (e.g. <c>.</c>, <c>(</c>, <c>[</c>) active and can either match
    /// too much or throw on an invalid pattern. The match is anchored and bounded by a short timeout.
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <param name="pattern">The glob pattern.</param>
    /// <param name="ignoreCase">Whether matching is case-insensitive (default true).</param>
    public static bool IsGlobMatch(string name, string pattern, bool ignoreCase = true)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        try
        {
            return Regex.IsMatch(name, regexPattern, options, TimeSpan.FromSeconds(1));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether an IP address targets the loopback, link-local, unique-local, carrier-grade-NAT,
    /// private, multicast, or unspecified ranges — i.e. an internal/non-public address that should be
    /// blocked by default to prevent Server-Side Request Forgery (SSRF).
    /// </summary>
    /// <param name="address">The address to classify.</param>
    /// <returns><c>true</c> if the address is internal/non-routable on the public internet.</returns>
    public static bool IsPrivateOrLocalAddress(SystemNet.IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (SystemNet.IPAddress.IsLoopback(address))
        {
            return true; // 127.0.0.0/8 and ::1
        }

        var b = address.GetAddressBytes();

        if (address.AddressFamily == SystemNet.Sockets.AddressFamily.InterNetwork)
        {
            return b[0] switch
            {
                0 => true,                                   // 0.0.0.0/8 (this network / unspecified)
                10 => true,                                  // 10.0.0.0/8
                127 => true,                                 // loopback (also caught above)
                169 when b[1] == 254 => true,                // link-local 169.254.0.0/16
                172 when b[1] >= 16 && b[1] <= 31 => true,   // 172.16.0.0/12
                192 when b[1] == 168 => true,                // 192.168.0.0/16
                100 when b[1] >= 64 && b[1] <= 127 => true,  // CGNAT 100.64.0.0/10
                >= 224 => true,                              // multicast 224/4 and reserved 240/4
                _ => false
            };
        }

        if (address.AddressFamily == SystemNet.Sockets.AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || address.Equals(SystemNet.IPAddress.IPv6Any))
            {
                return true;
            }

            // Unique Local Address fc00::/7.
            return (b[0] & 0xFE) == 0xFC;
        }

        return false;
    }

    /// <summary>
    /// Determines whether a host (IP literal or DNS name) resolves to an internal/non-public address and
    /// should therefore be blocked by default for outbound requests (SSRF protection). DNS names are
    /// resolved; a host is treated as blocked if <b>any</b> resolved address is internal (defends against
    /// DNS-rebinding split answers). On a resolution failure this returns <c>false</c> so the request
    /// fails naturally at connect time, where the actual endpoint IP is the authoritative check.
    /// </summary>
    public static async Task<bool> IsBlockedInternalHostAsync(string host, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        host = host.Trim('[', ']'); // strip IPv6 literal brackets

        if (SystemNet.IPAddress.TryParse(host, out var literal))
        {
            return IsPrivateOrLocalAddress(literal);
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            var addresses = await SystemNet.Dns.GetHostAddressesAsync(host, cancellationToken);
            return addresses.Length > 0 && addresses.Any(IsPrivateOrLocalAddress);
        }
        catch
        {
            return false;
        }
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
