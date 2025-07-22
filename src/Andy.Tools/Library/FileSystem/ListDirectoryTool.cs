using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.FileSystem;

/// <summary>
/// Represents a file system entry returned by ListDirectoryTool.
/// </summary>
public class FileSystemEntry
{
    public string Name { get; set; } = "";
    public string? FullPath { get; set; }
    public string Type { get; set; } = "";
    public long? Size { get; set; }
    public string? SizeFormatted { get; set; }
    public string? Extension { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? Modified { get; set; }
    public string? Attributes { get; set; }
    public bool? IsHidden { get; set; }
    public bool? IsReadonly { get; set; }
    public int? Depth { get; set; }
}

/// <summary>
/// Tool for listing directory contents.
/// </summary>
public class ListDirectoryTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "list_directory",
        Name = "List Directory",
        Description = "Lists the contents of a directory including files and subdirectories",
        Version = "1.0.0",
        Category = ToolCategory.FileSystem,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
        Parameters =
        [
            new()
            {
                Name = "directory_path",
                Description = "The path to the directory to list (default: current directory)",
                Type = "string",
                Required = false,
                DefaultValue = "."
            },
            new()
            {
                Name = "recursive",
                Description = "Whether to list subdirectories recursively (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "include_hidden",
                Description = "Whether to include hidden files and directories (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "pattern",
                Description = "File name pattern to match (supports wildcards like *.txt)",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "sort_by",
                Description = "How to sort the results",
                Type = "string",
                Required = false,
                DefaultValue = "name",
                AllowedValues = ["name", "size", "modified", "created", "type"]
            },
            new()
            {
                Name = "sort_descending",
                Description = "Whether to sort in descending order (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "max_depth",
                Description = "Maximum depth for recursive listing (default: unlimited)",
                Type = "integer",
                Required = false,
                MinValue = 1,
                MaxValue = 20
            },
            new()
            {
                Name = "include_details",
                Description = "Whether to include detailed file information (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var directoryPath = GetParameter(parameters, "directory_path", ".");
        var recursive = GetParameter(parameters, "recursive", false);
        var includeHidden = GetParameter(parameters, "include_hidden", false);
        var pattern = GetParameter<string>(parameters, "pattern");
        var sortBy = GetParameter(parameters, "sort_by", "name");
        var sortDescending = GetParameter(parameters, "sort_descending", false);
        var maxDepth = GetParameter<int?>(parameters, "max_depth");
        var includeDetails = GetParameter(parameters, "include_details", true);

        try
        {
            // Resolve and validate the directory path
            var safePath = ToolHelpers.GetSafePath(directoryPath, context.WorkingDirectory);

            if (!Directory.Exists(safePath))
            {
                return ToolResults.DirectoryNotFound(safePath);
            }

            ReportProgress(context, "Scanning directory...", 10);

            // Get directory entries
            var entries = new List<FileSystemEntryInfo>();
            await ScanDirectoryAsync(safePath, entries, includeHidden, pattern, recursive, maxDepth ?? int.MaxValue, 0, context);

            ReportProgress(context, "Sorting results...", 80);

            // Sort entries
            entries = SortEntries(entries, sortBy, sortDescending);

            ReportProgress(context, "Formatting results...", 90);

            // Format results
            var items = entries.Select(entry => FormatEntry(entry, includeDetails)).ToList();

            ReportProgress(context, "Directory listing completed", 100);

            var dirInfo = new DirectoryInfo(safePath);
            var metadata = new Dictionary<string, object?>
            {
                ["directory_path"] = safePath,
                ["total_entries"] = entries.Count,
                ["file_count"] = entries.Count(e => e.IsFile),
                ["directory_count"] = entries.Count(e => !e.IsFile),
                ["recursive"] = recursive,
                ["include_hidden"] = includeHidden,
                ["pattern"] = pattern,
                ["sort_by"] = sortBy,
                ["sort_descending"] = sortDescending,
                ["scanned_at"] = DateTime.UtcNow
            };

            if (maxDepth.HasValue)
            {
                metadata["max_depth"] = maxDepth.Value;
            }

            var result = ToolResults.ListSuccess(
                items,
                $"Listed {entries.Count} items in directory: {dirInfo.Name}",
                entries.Count
            );
            
            // Add the metadata to the result
            foreach (var kvp in metadata)
            {
                result.Metadata[kvp.Key] = kvp.Value;
            }
            
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResults.AccessDenied(directoryPath, "list");
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Failed to list directory: {ex.Message}", "LIST_ERROR", details: ex);
        }
    }

    private static async Task ScanDirectoryAsync(
        string directoryPath,
        List<FileSystemEntryInfo> entries,
        bool includeHidden,
        string? pattern,
        bool recursive,
        int maxDepth,
        int currentDepth,
        ToolExecutionContext context)
    {
        if (currentDepth >= maxDepth)
        {
            return;
        }

        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);

            // Get files
            var searchOption = SearchOption.TopDirectoryOnly;
            var searchPattern = pattern ?? "*";

            foreach (var file in dirInfo.GetFiles(searchPattern, searchOption))
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (!includeHidden && file.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    continue;
                }

                entries.Add(new FileSystemEntryInfo
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsFile = true,
                    Size = file.Length,
                    CreatedTime = file.CreationTimeUtc,
                    ModifiedTime = file.LastWriteTimeUtc,
                    Attributes = file.Attributes,
                    Extension = file.Extension,
                    Depth = currentDepth
                });
            }

            // Get directories
            foreach (var directory in dirInfo.GetDirectories())
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (!includeHidden && directory.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    continue;
                }

                entries.Add(new FileSystemEntryInfo
                {
                    Name = directory.Name,
                    FullPath = directory.FullName,
                    IsFile = false,
                    Size = 0,
                    CreatedTime = directory.CreationTimeUtc,
                    ModifiedTime = directory.LastWriteTimeUtc,
                    Attributes = directory.Attributes,
                    Extension = "",
                    Depth = currentDepth
                });

                // Recurse into subdirectory
                if (recursive)
                {
                    await ScanDirectoryAsync(directory.FullName, entries, includeHidden, pattern, recursive, maxDepth, currentDepth + 1, context);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
        catch (DirectoryNotFoundException)
        {
            // Skip directories that no longer exist
        }
    }

    private static List<FileSystemEntryInfo> SortEntries(List<FileSystemEntryInfo> entries, string sortBy, bool descending)
    {
        IOrderedEnumerable<FileSystemEntryInfo> sorted = sortBy.ToLowerInvariant() switch
        {
            "size" => descending ? entries.OrderByDescending(e => e.Size) : entries.OrderBy(e => e.Size),
            "modified" => descending ? entries.OrderByDescending(e => e.ModifiedTime) : entries.OrderBy(e => e.ModifiedTime),
            "created" => descending ? entries.OrderByDescending(e => e.CreatedTime) : entries.OrderBy(e => e.CreatedTime),
            "type" => descending ? entries.OrderByDescending(e => e.Extension) : entries.OrderBy(e => e.Extension),
            _ => descending ? entries.OrderByDescending(e => e.Name) : entries.OrderBy(e => e.Name)
        };

        return [.. sorted];
    }

    private static FileSystemEntry FormatEntry(FileSystemEntryInfo entry, bool includeDetails)
    {
        var result = new FileSystemEntry
        {
            Name = entry.Name,
            Type = entry.IsFile ? "file" : "directory",
            Size = entry.IsFile ? entry.Size : null
        };

        if (includeDetails)
        {
            result.FullPath = entry.FullPath;
            result.SizeFormatted = entry.IsFile ? ToolHelpers.FormatFileSize(entry.Size) : null;
            result.Extension = entry.IsFile ? entry.Extension : null;
            result.Created = entry.CreatedTime;
            result.Modified = entry.ModifiedTime;
            result.Attributes = entry.Attributes.ToString();
            result.IsHidden = entry.Attributes.HasFlag(FileAttributes.Hidden);
            result.IsReadonly = entry.Attributes.HasFlag(FileAttributes.ReadOnly);
            result.Depth = entry.Depth;
        }

        return result;
    }

    private class FileSystemEntryInfo
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsFile { get; set; }
        public long Size { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime ModifiedTime { get; set; }
        public FileAttributes Attributes { get; set; }
        public string Extension { get; set; } = "";
        public int Depth { get; set; }
    }
}
