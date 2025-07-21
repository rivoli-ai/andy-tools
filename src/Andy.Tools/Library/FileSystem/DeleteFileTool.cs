using System.Text.RegularExpressions;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.FileSystem;

/// <summary>
/// Tool for deleting files and directories.
/// </summary>
public class DeleteFileTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "delete_file",
        Name = "Delete File",
        Description = "Deletes files or directories with optional backup and safety features",
        Version = "1.0.0",
        Category = ToolCategory.FileSystem,
        RequiredPermissions = ToolPermissionFlags.FileSystemWrite,
        Parameters =
        [
            new()
            {
                Name = "target_path",
                Description = "The path to the file or directory to delete",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "recursive",
                Description = "Whether to delete directories recursively (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "create_backup",
                Description = "Whether to create backup before deletion (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "backup_location",
                Description = "Directory to store backups (default: system temp directory)",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "force",
                Description = "Whether to force deletion of read-only files (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "confirm_delete",
                Description = "Require explicit confirmation for deletion (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "exclude_patterns",
                Description = "Array of file patterns to exclude from deletion (e.g., ['*.log', 'backup*'])",
                Type = "array",
                Required = false,
                ItemType = new ToolParameter
                {
                    Type = "string",
                    Description = "File pattern to exclude (e.g., '*.log')"
                }
            },
            new()
            {
                Name = "max_size_mb",
                Description = "Maximum total size to delete in MB (safety limit, default: 1000MB)",
                Type = "number",
                Required = false,
                DefaultValue = 1000,
                MinValue = 1,
                MaxValue = 10000
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var targetPath = GetParameter<string>(parameters, "target_path");
        var recursive = GetParameter(parameters, "recursive", false);
        var createBackup = GetParameter(parameters, "create_backup", false);
        var backupLocation = GetParameter<string>(parameters, "backup_location");
        var force = GetParameter(parameters, "force", false);
        var confirmDelete = GetParameter(parameters, "confirm_delete", true);
        var excludePatterns = GetParameter<List<string>>(parameters, "exclude_patterns", []);
        var maxSizeMb = GetParameter<double>(parameters, "max_size_mb", 1000);

        try
        {
            // Resolve and validate the target path
            var safeTargetPath = ToolHelpers.GetSafePath(targetPath, context.WorkingDirectory);

            if (!File.Exists(safeTargetPath) && !Directory.Exists(safeTargetPath))
            {
                return ToolResults.Failure(
                    $"Target path does not exist: {safeTargetPath}",
                    "TARGET_NOT_FOUND"
                );
            }

            ReportProgress(context, "Analyzing deletion target...", 10);

            var isDirectory = Directory.Exists(safeTargetPath);
            var deleteStats = new DeleteStatistics
            {
                TargetPath = safeTargetPath,
                IsDirectory = isDirectory
            };

            // Analyze what will be deleted
            if (isDirectory)
            {
                if (!recursive)
                {
                    var dirInfo = new DirectoryInfo(safeTargetPath);
                    if (dirInfo.GetFiles().Length > 0 || dirInfo.GetDirectories().Length > 0)
                    {
                        return ToolResults.Failure(
                            "Directory is not empty and recursive deletion is disabled",
                            "DIRECTORY_NOT_EMPTY"
                        );
                    }
                }

                await AnalyzeDirectoryAsync(safeTargetPath, excludePatterns, deleteStats, context);
            }
            else
            {
                var fileInfo = new FileInfo(safeTargetPath);
                if (!ShouldExclude(fileInfo.Name, excludePatterns))
                {
                    deleteStats.FilesToDelete = 1;
                    deleteStats.TotalSizeBytes = fileInfo.Length;
                }
                else
                {
                    return ToolResults.Failure(
                        $"File matches exclusion pattern: {fileInfo.Name}",
                        "FILE_EXCLUDED"
                    );
                }
            }

            // Safety checks
            var maxSizeBytes = (long)(maxSizeMb * 1024 * 1024);
            if (deleteStats.TotalSizeBytes > maxSizeBytes)
            {
                return ToolResults.Failure(
                    $"Total size to delete ({ToolHelpers.FormatFileSize(deleteStats.TotalSizeBytes)}) exceeds safety limit ({maxSizeMb}MB)",
                    "SIZE_LIMIT_EXCEEDED",
                    new { total_size = deleteStats.TotalSizeBytes, limit_size = maxSizeBytes }
                );
            }

            // Confirmation check (in a real implementation, this might prompt the user)
            if (confirmDelete && deleteStats.FilesToDelete > 10)
            {
                ReportProgress(context, $"WARNING: About to delete {deleteStats.FilesToDelete} files ({ToolHelpers.FormatFileSize(deleteStats.TotalSizeBytes)})", 30);
            }

            ReportProgress(context, "Preparing deletion operation...", 40);

            // Create backup if requested
            if (createBackup)
            {
                await CreateBackupAsync(safeTargetPath, backupLocation, deleteStats, context);
                ReportProgress(context, "Backup created", 60);
            }

            ReportProgress(context, "Deleting files...", 70);

            // Perform deletion
            if (isDirectory)
            {
                await DeleteDirectoryAsync(safeTargetPath, force, excludePatterns, deleteStats, context);
            }
            else
            {
                await DeleteFileAsync(safeTargetPath, force, deleteStats, context);
            }

            ReportProgress(context, "Deletion completed", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["target_path"] = safeTargetPath,
                ["is_directory"] = isDirectory,
                ["files_deleted"] = deleteStats.FilesDeleted,
                ["directories_deleted"] = deleteStats.DirectoriesDeleted,
                ["bytes_deleted"] = deleteStats.BytesDeleted,
                ["bytes_deleted_formatted"] = ToolHelpers.FormatFileSize(deleteStats.BytesDeleted),
                ["backup_created"] = deleteStats.BackupPath != null,
                ["backup_path"] = deleteStats.BackupPath,
                ["operation_time"] = deleteStats.OperationTime,
                ["recursive"] = recursive,
                ["force"] = force,
                ["exclude_patterns"] = excludePatterns,
                ["errors"] = deleteStats.Errors
            };

            var message = isDirectory
                ? $"Successfully deleted directory: {deleteStats.FilesDeleted} files, {deleteStats.DirectoriesDeleted} directories"
                : $"Successfully deleted file: {ToolHelpers.FormatFileSize(deleteStats.BytesDeleted)}";

            return ToolResults.Success(deleteStats, message, metadata);
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResults.AccessDenied(targetPath, "delete");
        }
        catch (DirectoryNotFoundException)
        {
            return ToolResults.DirectoryNotFound(targetPath);
        }
        catch (IOException ex) when (ex.Message.Contains("being used"))
        {
            return ToolResults.Failure(
                $"File is in use and cannot be deleted: {targetPath}",
                "FILE_IN_USE",
                details: ex
            );
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Failed to delete: {ex.Message}", "DELETE_ERROR", details: ex);
        }
    }

    private static async Task AnalyzeDirectoryAsync(
        string directoryPath,
        List<string> excludePatterns,
        DeleteStatistics stats,
        ToolExecutionContext context)
    {
        var directory = new DirectoryInfo(directoryPath);

        // Analyze files
        foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (!ShouldExclude(file.Name, excludePatterns))
            {
                stats.FilesToDelete++;
                stats.TotalSizeBytes += file.Length;
            }
        }

        // Count directories
        stats.DirectoriesToDelete = directory.GetDirectories("*", SearchOption.AllDirectories).Length + 1; // +1 for root directory

        await Task.CompletedTask;
    }

    private static async Task CreateBackupAsync(
        string targetPath,
        string? backupLocation,
        DeleteStatistics stats,
        ToolExecutionContext context)
    {
        backupLocation ??= Path.GetTempPath();
        var backupName = $"backup_{Path.GetFileName(targetPath)}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        stats.BackupPath = Path.Combine(backupLocation, backupName);

        if (stats.IsDirectory)
        {
            await Task.Run(() => CopyDirectory(targetPath, stats.BackupPath), context.CancellationToken);
        }
        else
        {
            File.Copy(targetPath, stats.BackupPath, true);
        }
    }

    private static async Task DeleteFileAsync(
        string filePath,
        bool force,
        DeleteStatistics stats,
        ToolExecutionContext context)
    {
        var fileInfo = new FileInfo(filePath);

        if (force && fileInfo.IsReadOnly)
        {
            fileInfo.IsReadOnly = false;
        }

        await Task.Run(() => File.Delete(filePath), context.CancellationToken);

        stats.FilesDeleted++;
        stats.BytesDeleted += fileInfo.Length;
    }

    private static async Task DeleteDirectoryAsync(
        string directoryPath,
        bool force,
        List<string> excludePatterns,
        DeleteStatistics stats,
        ToolExecutionContext context)
    {
        var directory = new DirectoryInfo(directoryPath);

        // Delete files first
        foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (ShouldExclude(file.Name, excludePatterns))
            {
                continue;
            }

            try
            {
                if (force && file.IsReadOnly)
                {
                    file.IsReadOnly = false;
                }

                file.Delete();
                stats.FilesDeleted++;
                stats.BytesDeleted += file.Length;
            }
            catch (Exception ex)
            {
                stats.Errors.Add($"Failed to delete file {file.Name}: {ex.Message}");
            }
        }

        // Delete directories (bottom-up)
        var directories = directory.GetDirectories("*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.FullName.Length)
            .ToList();

        foreach (var dir in directories)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (force)
                {
                    // Remove readonly attribute from directory
                    if (dir.Attributes.HasFlag(FileAttributes.ReadOnly))
                    {
                        dir.Attributes &= ~FileAttributes.ReadOnly;
                    }
                }

                dir.Delete();
                stats.DirectoriesDeleted++;
            }
            catch (Exception ex)
            {
                stats.Errors.Add($"Failed to delete directory {dir.Name}: {ex.Message}");
            }
        }

        // Finally delete the root directory
        try
        {
            if (force && directory.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                directory.Attributes &= ~FileAttributes.ReadOnly;
            }

            directory.Delete();
            stats.DirectoriesDeleted++;
        }
        catch (Exception ex)
        {
            stats.Errors.Add($"Failed to delete root directory: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    private static bool ShouldExclude(string name, List<string> excludePatterns)
    {
        return excludePatterns.Any(pattern =>
        {
            if (pattern.Contains('*'))
            {
                var regexPattern = "^" + pattern.Replace("*", ".*") + "$";
                return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
            }

            return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var source = new DirectoryInfo(sourceDir);
        var destination = new DirectoryInfo(destinationDir);

        if (!destination.Exists)
        {
            destination.Create();
        }

        foreach (var file in source.GetFiles())
        {
            file.CopyTo(Path.Combine(destination.FullName, file.Name), true);
        }

        foreach (var subDir in source.GetDirectories())
        {
            CopyDirectory(subDir.FullName, Path.Combine(destination.FullName, subDir.Name));
        }
    }

    private class DeleteStatistics
    {
        public string TargetPath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public int FilesToDelete { get; set; }
        public int DirectoriesToDelete { get; set; }
        public long TotalSizeBytes { get; set; }
        public int FilesDeleted { get; set; }
        public int DirectoriesDeleted { get; set; }
        public long BytesDeleted { get; set; }
        public string? BackupPath { get; set; }
        public List<string> Errors { get; set; } = [];
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public TimeSpan OperationTime => DateTime.UtcNow - StartTime;
    }
}
