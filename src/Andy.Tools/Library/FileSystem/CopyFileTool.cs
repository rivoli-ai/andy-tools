using System.Text.RegularExpressions;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.FileSystem;

/// <summary>
/// Tool for copying files and directories.
/// </summary>
public class CopyFileTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "copy_file",
        Name = "Copy File",
        Description = "Copies files or directories from source to destination",
        Version = "1.0.0",
        Category = ToolCategory.FileSystem,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead | ToolPermissionFlags.FileSystemWrite,
        Parameters =
        [
            new()
            {
                Name = "source_path",
                Description = "The path to the source file or directory to copy",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "destination_path",
                Description = "The path to copy the file or directory to",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "overwrite",
                Description = "Whether to overwrite existing files (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "recursive",
                Description = "Whether to copy directories recursively (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "preserve_timestamps",
                Description = "Whether to preserve file timestamps (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "follow_symlinks",
                Description = "Whether to follow symbolic links (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "exclude_patterns",
                Description = "Array of file patterns to exclude (e.g., ['*.tmp', '.git'])",
                Type = "array",
                Required = false,
                ItemType = new ToolParameter
                {
                    Type = "string",
                    Description = "File pattern to exclude (e.g., '*.tmp')"
                }
            },
            new()
            {
                Name = "create_destination_directory",
                Description = "Whether to create destination directory if it doesn't exist (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var sourcePath = GetParameter<string>(parameters, "source_path");
        var destinationPath = GetParameter<string>(parameters, "destination_path");
        var overwrite = GetParameter(parameters, "overwrite", false);
        var recursive = GetParameter(parameters, "recursive", true);
        var preserveTimestamps = GetParameter(parameters, "preserve_timestamps", true);
        var followSymlinks = GetParameter(parameters, "follow_symlinks", false);
        // Handle exclude patterns - can be passed as array or list
        var excludePatterns = new List<string>();
        if (parameters.TryGetValue("exclude_patterns", out var excludeValue))
        {
            if (excludeValue is List<string> list)
            {
                excludePatterns = list;
            }
            else if (excludeValue is string[] array)
            {
                excludePatterns = array.ToList();
            }
        }
        var createDestinationDirectory = GetParameter(parameters, "create_destination_directory", true);

        try
        {
            // Validate parameters
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return ToolResults.Failure("Invalid source_path: cannot be null or empty", "INVALID_PARAMETER");
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                return ToolResults.Failure("Invalid destination_path: cannot be null or empty", "INVALID_PARAMETER");
            }

            // Resolve and validate paths
            var safeSourcePath = ToolHelpers.GetSafePath(sourcePath, context.WorkingDirectory);
            var safeDestinationPath = ToolHelpers.GetSafePath(destinationPath, context.WorkingDirectory);

            if (!File.Exists(safeSourcePath) && !Directory.Exists(safeSourcePath))
            {
                return ToolResults.Failure(
                    $"Source path does not exist: {safeSourcePath}",
                    "SOURCE_NOT_FOUND"
                );
            }

            ReportProgress(context, "Preparing copy operation...", 5);

            var copyStats = new CopyStatistics();
            var isDirectory = Directory.Exists(safeSourcePath);

            if (isDirectory)
            {
                await CopyDirectoryAsync(
                    safeSourcePath,
                    safeDestinationPath,
                    overwrite,
                    recursive,
                    preserveTimestamps,
                    followSymlinks,
                    excludePatterns,
                    createDestinationDirectory,
                    copyStats,
                    context
                );
            }
            else
            {
                await CopyFileAsync(
                    safeSourcePath,
                    safeDestinationPath,
                    overwrite,
                    preserveTimestamps,
                    createDestinationDirectory,
                    copyStats,
                    context
                );
            }

            ReportProgress(context, "Copy operation completed", 100);

            // Return the stats directly as data
            var data = new Dictionary<string, object?>
            {
                ["files_copied"] = copyStats.FilesCopied,
                ["directories_created"] = copyStats.DirectoriesCreated,
                ["bytes_copied"] = copyStats.BytesCopied,
                ["bytes_copied_formatted"] = ToolHelpers.FormatFileSize(copyStats.BytesCopied),
                ["files_skipped"] = copyStats.FilesSkipped,
                ["errors"] = copyStats.Errors,
                ["operation_time"] = copyStats.OperationTime.TotalSeconds
            };

            var metadata = new Dictionary<string, object?>
            {
                ["source_path"] = safeSourcePath,
                ["destination_path"] = safeDestinationPath,
                ["is_directory"] = isDirectory,
                ["overwrite_enabled"] = overwrite,
                ["recursive"] = recursive,
                ["exclude_patterns"] = excludePatterns
            };

            var message = isDirectory
                ? $"Successfully copied directory: {copyStats.FilesCopied} files, {copyStats.DirectoriesCreated} directories"
                : $"Successfully copied file: {ToolHelpers.FormatFileSize(copyStats.BytesCopied)}";

            return ToolResults.Success(data, message, metadata);
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResults.AccessDenied($"{sourcePath} or {destinationPath}", "copy");
        }
        catch (DirectoryNotFoundException ex)
        {
            return ToolResults.DirectoryNotFound(ex.Message);
        }
        catch (IOException ex) when (ex.Message.Contains("already exists"))
        {
            return ToolResults.Failure(
                $"Destination already exists and overwrite is disabled: {destinationPath}",
                "DESTINATION_EXISTS",
                details: ex
            );
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Failed to copy: {ex.Message}", "COPY_ERROR", details: ex);
        }
    }

    private async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        bool preserveTimestamps,
        bool createDestinationDirectory,
        CopyStatistics stats,
        ToolExecutionContext context)
    {
        var sourceFile = new FileInfo(sourcePath);
        var destinationFile = new FileInfo(destinationPath);

        // Create destination directory if needed
        if (createDestinationDirectory && destinationFile.DirectoryName != null)
        {
            Directory.CreateDirectory(destinationFile.DirectoryName);
        }

        // Check if destination exists
        if (destinationFile.Exists && !overwrite)
        {
            throw new IOException($"Destination file already exists: {destinationPath}");
        }

        ReportProgress(context, $"Copying file: {sourceFile.Name}", 50);

        // Copy the file
        await Task.Run(() => File.Copy(sourcePath, destinationPath, overwrite), context.CancellationToken);

        // Preserve timestamps if requested
        if (preserveTimestamps)
        {
            File.SetCreationTimeUtc(destinationPath, sourceFile.CreationTimeUtc);
            File.SetLastWriteTimeUtc(destinationPath, sourceFile.LastWriteTimeUtc);
            File.SetLastAccessTimeUtc(destinationPath, sourceFile.LastAccessTimeUtc);
        }

        stats.FilesCopied++;
        stats.BytesCopied += sourceFile.Length;
    }

    private async Task CopyDirectoryAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        bool recursive,
        bool preserveTimestamps,
        bool followSymlinks,
        List<string> excludePatterns,
        bool createDestinationDirectory,
        CopyStatistics stats,
        ToolExecutionContext context)
    {
        var sourceDir = new DirectoryInfo(sourcePath);
        var destinationDir = new DirectoryInfo(destinationPath);

        // Create destination directory
        if (!destinationDir.Exists)
        {
            destinationDir.Create();
            stats.DirectoriesCreated++;
        }

        // Copy files in current directory
        foreach (var file in sourceDir.GetFiles())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (ShouldExclude(file.Name, excludePatterns))
            {
                stats.FilesSkipped++;
                continue;
            }

            try
            {
                var destFile = Path.Combine(destinationPath, file.Name);
                await CopyFileAsync(file.FullName, destFile, overwrite, preserveTimestamps, false, stats, context);

                // Update progress
                var progressPercent = Math.Min(95, 10 + (stats.FilesCopied * 80 / Math.Max(1, CountFiles(sourceDir, recursive, excludePatterns))));
                ReportProgress(context, $"Copied: {file.Name}", progressPercent);
            }
            catch (Exception ex)
            {
                stats.Errors.Add($"Failed to copy file {file.Name}: {ex.Message}");
            }
        }

        // Copy subdirectories if recursive
        if (recursive)
        {
            foreach (var subDir in sourceDir.GetDirectories())
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (ShouldExclude(subDir.Name, excludePatterns))
                {
                    continue;
                }

                try
                {
                    var destSubDir = Path.Combine(destinationPath, subDir.Name);
                    await CopyDirectoryAsync(
                        subDir.FullName,
                        destSubDir,
                        overwrite,
                        recursive,
                        preserveTimestamps,
                        followSymlinks,
                        excludePatterns,
                        createDestinationDirectory,
                        stats,
                        context
                    );
                }
                catch (Exception ex)
                {
                    stats.Errors.Add($"Failed to copy directory {subDir.Name}: {ex.Message}");
                }
            }
        }
    }

    private static bool ShouldExclude(string name, List<string> excludePatterns)
    {
        return excludePatterns.Any(pattern =>
        {
            // Simple wildcard matching
            if (pattern.Contains('*'))
            {
                var regexPattern = "^" + pattern.Replace("*", ".*") + "$";
                return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
            }

            return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static int CountFiles(DirectoryInfo directory, bool recursive, List<string> excludePatterns)
    {
        var count = directory.GetFiles().Count(f => !ShouldExclude(f.Name, excludePatterns));

        if (recursive)
        {
            count += directory.GetDirectories()
                .Where(d => !ShouldExclude(d.Name, excludePatterns))
                .Sum(d => CountFiles(d, recursive, excludePatterns));
        }

        return count;
    }

    private class CopyStatistics
    {
        public int FilesCopied { get; set; }
        public int DirectoriesCreated { get; set; }
        public long BytesCopied { get; set; }
        public int FilesSkipped { get; set; }
        public List<string> Errors { get; set; } = [];
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public TimeSpan OperationTime => DateTime.UtcNow - StartTime;
    }
}
