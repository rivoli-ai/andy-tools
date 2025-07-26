using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.FileSystem;

/// <summary>
/// Tool for moving/renaming files and directories.
/// </summary>
public partial class MoveFileTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "move_file",
        Name = "Move File",
        Description = "Moves or renames files and directories from source to destination",
        Version = "1.0.0",
        Category = ToolCategory.FileSystem,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead | ToolPermissionFlags.FileSystemWrite,
        Parameters =
        [
            new()
            {
                Name = "source_path",
                Description = "The path to the source file or directory to move",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "destination_path",
                Description = "The path to move the file or directory to",
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
                Name = "create_destination_directory",
                Description = "Whether to create destination directory if it doesn't exist (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "backup_existing",
                Description = "Whether to create backup of existing destination file (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var sourcePath = GetParameter<string>(parameters, "source_path");
        var destinationPath = GetParameter<string>(parameters, "destination_path");
        var overwrite = GetParameter(parameters, "overwrite", false);
        var createDestinationDirectory = GetParameter(parameters, "create_destination_directory", true);
        var backupExisting = GetParameter(parameters, "backup_existing", false);

        try
        {
            // Validate input parameters
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return ToolResults.Failure("source_path cannot be empty", "MISSING_SOURCE_PATH");
            }
            
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                return ToolResults.Failure("destination_path cannot be empty", "MISSING_DESTINATION_PATH");
            }
            
            // Resolve and validate paths
            string safeSourcePath;
            string safeDestinationPath;
            try
            {
                safeSourcePath = ToolHelpers.GetSafePath(sourcePath, context.WorkingDirectory);
                safeDestinationPath = ToolHelpers.GetSafePath(destinationPath, context.WorkingDirectory);
            }
            catch (ArgumentException)
            {
                // If the path validation fails with working directory, try without it
                // This handles cases where absolute paths are provided that are valid
                safeSourcePath = ToolHelpers.GetSafePath(sourcePath, null);
                safeDestinationPath = ToolHelpers.GetSafePath(destinationPath, null);
            }

            if (!File.Exists(safeSourcePath) && !Directory.Exists(safeSourcePath))
            {
                return ToolResults.Failure(
                    $"source path does not exist: {safeSourcePath}",
                    "SOURCE_NOT_FOUND"
                );
            }

            // Check if source and destination are the same
            if (string.Equals(safeSourcePath, safeDestinationPath, StringComparison.OrdinalIgnoreCase))
            {
                return ToolResults.Failure(
                    "Source and destination paths are the same",
                    "SAME_PATH"
                );
            }
            
            // Check if moving to a subdirectory (circular move)
            if (Directory.Exists(safeSourcePath))
            {
                var sourceDirInfo = new DirectoryInfo(safeSourcePath);
                var destPath = Path.GetFullPath(safeDestinationPath);
                var srcFullPath = sourceDirInfo.FullName;
                
                if (destPath.StartsWith(srcFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    destPath.StartsWith(srcFullPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return ToolResults.Failure(
                        "Cannot move directory to its own subdirectory",
                        "CIRCULAR_MOVE"
                    );
                }
            }
            
            ReportProgress(context, "Validating move operation...", 10);

            var isDirectory = Directory.Exists(safeSourcePath);
            bool destinationExists;
            
            if (isDirectory)
            {
                // For directories, check if the destination directory exists
                destinationExists = Directory.Exists(safeDestinationPath);
            }
            else
            {
                // For files, check if the destination file exists
                destinationExists = File.Exists(safeDestinationPath);
            }

            // Check if destination exists and handle accordingly
            if (destinationExists && !overwrite)
            {
                return ToolResults.Failure(
                    $"Destination already exists and overwrite is disabled: {safeDestinationPath}",
                    "DESTINATION_EXISTS",
                    new { destination_path = safeDestinationPath, exists = true }
                );
            }

            // Create backup if requested and destination exists
            string? backupPath = null;
            if (backupExisting && destinationExists)
            {
                backupPath = $"{safeDestinationPath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
                ReportProgress(context, "Creating backup...", 30);

                if (isDirectory)
                {
                    await Task.Run(() => CopyDirectory(safeDestinationPath, backupPath), context.CancellationToken);
                }
                else
                {
                    File.Copy(safeDestinationPath, backupPath, true);
                }
            }

            // Create destination directory if needed
            if (createDestinationDirectory)
            {
                // For directories, we need the parent of the destination, not the destination itself
                // For files, we need the directory containing the file
                var destinationDir = Path.GetDirectoryName(safeDestinationPath);
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                    ReportProgress(context, "Created destination directory", 50);
                }
            }

            ReportProgress(context, "Moving...", 70);

            // Perform the move operation
            var moveStats = new MoveStatistics
            {
                SourcePath = safeSourcePath,
                DestinationPath = safeDestinationPath,
                IsDirectory = isDirectory,
                BackupPath = backupPath
            };

            if (isDirectory)
            {
                // For directories, we need to handle the move carefully
                if (destinationExists && overwrite)
                {
                    Directory.Delete(safeDestinationPath, true);
                }

                // Calculate statistics before moving
                moveStats.ItemsMoved = CountDirectoryItems(safeSourcePath);
                moveStats.BytesMoved = CalculateDirectorySize(safeSourcePath);
                
                Directory.Move(safeSourcePath, safeDestinationPath);
            }
            else
            {
                // For files, get size before moving
                var fileInfo = new FileInfo(safeSourcePath);
                moveStats.BytesMoved = fileInfo.Length;
                moveStats.ItemsMoved = 1;

                if (destinationExists && overwrite)
                {
                    File.Delete(safeDestinationPath);
                }

                File.Move(safeSourcePath, safeDestinationPath);
            }

            ReportProgress(context, "Move operation completed", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["source_path"] = safeSourcePath,
                ["destination_path"] = safeDestinationPath,
                ["is_directory"] = isDirectory,
                ["moved_items"] = moveStats.ItemsMoved,
                ["items_moved"] = moveStats.ItemsMoved,
                ["bytes_moved"] = moveStats.BytesMoved,
                ["bytes_moved_formatted"] = moveStats.BytesMoved > 0 ? ToolHelpers.FormatFileSize(moveStats.BytesMoved) : null,
                ["operation_time"] = moveStats.OperationTime,
                ["backup_created"] = backupPath != null,
                ["backup_path"] = backupPath,
                ["overwrite_enabled"] = overwrite
            };

            var operation = Path.GetDirectoryName(safeSourcePath) == Path.GetDirectoryName(safeDestinationPath) ? "renamed" : "moved";
            var message = isDirectory
                ? $"Successfully {operation} directory with {moveStats.ItemsMoved} items"
                : $"Successfully {operation} file ({ToolHelpers.FormatFileSize(moveStats.BytesMoved)})";

            return ToolResults.Success(metadata, message, metadata);
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResults.AccessDenied($"{sourcePath} or {destinationPath}", "move");
        }
        catch (DirectoryNotFoundException ex)
        {
            return ToolResults.DirectoryNotFound(ex.Message);
        }
        catch (IOException ex) when (ex.Message.Contains("already exists"))
        {
            return ToolResults.Failure(
                $"Destination already exists: {destinationPath}",
                "DESTINATION_EXISTS",
                details: ex
            );
        }
        catch (IOException ex) when (ex.Message.Contains("being used"))
        {
            return ToolResults.Failure(
                $"File is in use and cannot be moved: {sourcePath}",
                "FILE_IN_USE",
                details: ex
            );
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Failed to move: {ex.Message}", "MOVE_ERROR", details: ex);
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var source = new DirectoryInfo(sourceDir);
        var destination = new DirectoryInfo(destinationDir);

        if (!destination.Exists)
        {
            destination.Create();
        }

        // Copy files
        foreach (var file in source.GetFiles())
        {
            file.CopyTo(Path.Combine(destination.FullName, file.Name), true);
        }

        // Copy subdirectories
        foreach (var subDir in source.GetDirectories())
        {
            CopyDirectory(subDir.FullName, Path.Combine(destination.FullName, subDir.Name));
        }
    }

    private static int CountDirectoryItems(string directoryPath)
    {
        try
        {
            var directory = new DirectoryInfo(directoryPath);
            var count = directory.GetFiles("*", SearchOption.AllDirectories).Length;
            count += directory.GetDirectories("*", SearchOption.AllDirectories).Length;
            count += 1; // Include the directory itself
            return count;
        }
        catch
        {
            return 0;
        }
    }
    
    private static long CalculateDirectorySize(string directoryPath)
    {
        try
        {
            var directory = new DirectoryInfo(directoryPath);
            return directory.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }
        catch
        {
            return 0;
        }
    }
}
