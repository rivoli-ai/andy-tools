using System.Collections;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.FileSystem;

/// <summary>
/// Tool for reading the text content of many files at once, resolving explicit paths and glob patterns.
/// </summary>
public class ReadManyFilesTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "read_many_files",
        Name = "Read Many Files",
        Description = "Reads the text content of multiple files specified by explicit paths and/or glob patterns relative to the working directory",
        Version = "1.0.0",
        Category = ToolCategory.FileSystem,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
        Parameters =
        [
            new()
            {
                Name = "paths",
                Description = "An array of file paths and/or glob patterns (e.g. 'src/*.cs') relative to the working directory",
                Type = "array",
                Required = true
            },
            new()
            {
                Name = "max_files",
                Description = "Maximum number of files to read (default: 50)",
                Type = "integer",
                Required = false,
                DefaultValue = 50,
                MinValue = 1
            },
            new()
            {
                Name = "max_total_size_mb",
                Description = "Maximum cumulative size of file content to read in MB. No limit if not specified.",
                Type = "number",
                Required = false,
                MinValue = 0.1
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var patterns = GetParameterAsStringList(parameters, "paths") ?? [];
        var maxFiles = GetParameter<int>(parameters, "max_files", 50);
        var maxTotalSizeMb = GetParameter<double?>(parameters, "max_total_size_mb");

        if (patterns.Count == 0)
        {
            return ToolResults.InvalidParameter("paths", null, "At least one path or glob pattern is required");
        }

        var workingDirectory = context.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var maxTotalBytes = maxTotalSizeMb.HasValue ? (long)(maxTotalSizeMb.Value * 1024 * 1024) : long.MaxValue;

        try
        {
            // Resolve all candidate files (explicit paths + globs), confined and de-duplicated.
            var resolved = ResolveCandidates(patterns, workingDirectory, context);

            var files = new List<Dictionary<string, object?>>();
            long totalBytes = 0;
            var skippedBinary = 0;
            var skippedOutsideAllowed = 0;
            var limitReached = false;

            foreach (var safePath in resolved)
            {
                if (files.Count >= maxFiles)
                {
                    limitReached = true;
                    break;
                }

                if (!ToolHelpers.IsPathWithinAllowedPaths(safePath, context.Permissions))
                {
                    skippedOutsideAllowed++;
                    continue;
                }

                if (IsBinaryFile(safePath))
                {
                    skippedBinary++;
                    continue;
                }

                var encoding = ToolHelpers.DetectEncoding(safePath);
                var content = await ToolHelpers.ReadTextFileAsync(safePath, encoding, context.CancellationToken);

                var truncated = false;
                var contentBytes = encoding.GetByteCount(content);
                if (totalBytes + contentBytes > maxTotalBytes)
                {
                    var remaining = maxTotalBytes - totalBytes;
                    if (remaining <= 0)
                    {
                        limitReached = true;
                        break;
                    }

                    // Truncate by characters as an approximation of the remaining byte budget.
                    var keep = (int)Math.Min(content.Length, remaining);
                    content = content[..keep];
                    contentBytes = encoding.GetByteCount(content);
                    truncated = true;
                    limitReached = true;
                }

                totalBytes += contentBytes;
                files.Add(new Dictionary<string, object?>
                {
                    ["path"] = safePath,
                    ["content"] = content,
                    ["truncated"] = truncated
                });

                if (truncated)
                {
                    break;
                }
            }

            var metadata = new Dictionary<string, object?>
            {
                ["files_read"] = files.Count,
                ["files_matched"] = resolved.Count,
                ["skipped_binary"] = skippedBinary,
                ["skipped_outside_allowed"] = skippedOutsideAllowed,
                ["total_bytes"] = totalBytes,
                ["limit_reached"] = limitReached
            };

            var data = new Dictionary<string, object?>
            {
                ["files"] = files,
                ["count"] = files.Count
            };

            return ToolResults.Success(
                data,
                $"Read {files.Count} file(s) from {resolved.Count} match(es)",
                metadata
            );
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResults.AccessDenied(workingDirectory, "read");
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Failed to read files: {ex.Message}", "READ_ERROR", details: ex);
        }
    }

    private static List<string> ResolveCandidates(List<string> patterns, string workingDirectory, ToolExecutionContext context)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                foreach (var match in ExpandGlob(pattern, workingDirectory, context))
                {
                    if (seen.Add(match))
                    {
                        results.Add(match);
                    }
                }
            }
            else
            {
                string safePath;
                try
                {
                    safePath = ToolHelpers.GetSafePath(pattern, workingDirectory);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (File.Exists(safePath) && seen.Add(safePath))
                {
                    results.Add(safePath);
                }
            }
        }

        return results;
    }

    private static IEnumerable<string> ExpandGlob(string pattern, string workingDirectory, ToolExecutionContext context)
    {
        // Split the pattern into a directory portion (literal) and a file-name portion (glob).
        var directoryPart = Path.GetDirectoryName(pattern);
        var filePart = Path.GetFileName(pattern);

        string searchDirectory;
        try
        {
            searchDirectory = string.IsNullOrEmpty(directoryPart)
                ? ToolHelpers.GetSafePath(".", workingDirectory)
                : ToolHelpers.GetSafePath(directoryPart, workingDirectory);
        }
        catch (ArgumentException)
        {
            yield break;
        }

        if (!Directory.Exists(searchDirectory))
        {
            yield break;
        }

        // Recurse only when the glob targets the directory portion; here we match file names in-place.
        IEnumerable<string> candidates;
        try
        {
            candidates = Directory.EnumerateFiles(searchDirectory, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException)
        {
            yield break;
        }

        foreach (var candidate in candidates)
        {
            var name = Path.GetFileName(candidate);
            if (ToolHelpers.IsGlobMatch(name, filePart))
            {
                yield return candidate;
            }
        }
    }

    /// <summary>
    /// Heuristically determines whether a file is binary by scanning the first chunk for a null byte.
    /// </summary>
    private static bool IsBinaryFile(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[8192];
            var read = stream.Read(buffer, 0, buffer.Length);
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    private static List<string>? GetParameterAsStringList(Dictionary<string, object?> parameters, string name)
    {
        if (!parameters.TryGetValue(name, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            List<string> list => list,
            string[] array => [.. array],
            IEnumerable<string> enumerable => [.. enumerable],
            IEnumerable enumerable => [.. enumerable.Cast<object?>().Select(o => o?.ToString() ?? string.Empty)],
            _ => null
        };
    }
}
