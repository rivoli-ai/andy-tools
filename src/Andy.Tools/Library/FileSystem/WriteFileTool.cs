using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.FileSystem;

/// <summary>
/// Tool for writing content to files.
/// </summary>
public class WriteFileTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "write_file",
        Name = "Write File",
        Description = "Writes text content to a file on disk. Use this ONLY when explicitly asked to create or save a file. For displaying code or text to the user, output it directly instead.",
        Version = "1.0.0",
        Category = ToolCategory.FileSystem,
        RequiredPermissions = ToolPermissionFlags.FileSystemWrite,
        Parameters =
        [
            new()
            {
                Name = "file_path",
                Description = "The path to the file to write",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "content",
                Description = "The text content to write to the file",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "encoding",
                Description = "The text encoding to use (default: utf-8)",
                Type = "string",
                Required = false,
                DefaultValue = "utf-8",
                AllowedValues = ["utf-8", "ascii", "unicode", "utf-16", "utf-32"]
            },
            new()
            {
                Name = "create_backup",
                Description = "Whether to create a backup of existing file (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "overwrite",
                Description = "Whether to overwrite existing file (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "append",
                Description = "Whether to append to existing file instead of overwriting (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var filePath = GetParameter<string>(parameters, "file_path");
        var content = GetParameter<string>(parameters, "content");
        var encodingName = GetParameter(parameters, "encoding", "utf-8");
        var createBackup = GetParameter(parameters, "create_backup", true);
        var overwrite = GetParameter(parameters, "overwrite", true);
        var append = GetParameter(parameters, "append", false);

        try
        {
            // Resolve and validate the file path
            var safePath = ToolHelpers.GetSafePath(filePath, context.WorkingDirectory);

            // Check if path is within allowed paths
            if (!ToolHelpers.IsPathWithinAllowedPaths(safePath, context.Permissions))
            {
                return ToolResults.Failure($"Path '{safePath}' is not within allowed paths", "PATH_NOT_ALLOWED");
            }

            var fileExists = File.Exists(safePath);

            // Check overwrite permissions
            if (fileExists && !overwrite && !append)
            {
                return ToolResults.Failure(
                    $"File already exists and overwrite is disabled: {safePath}",
                    "FILE_EXISTS",
                    new { file_path = safePath, exists = true }
                );
            }

            // Determine encoding
            var encoding = encodingName.ToLowerInvariant() switch
            {
                "utf-8" => Encoding.UTF8,
                "ascii" => Encoding.ASCII,
                "unicode" or "utf-16" => Encoding.Unicode,
                "utf-32" => Encoding.UTF32,
                _ => throw new ArgumentException($"Unsupported encoding: {encodingName}")
            };

            ReportProgress(context, "Preparing to write file...", 10);

            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(safePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                ReportProgress(context, "Created directory structure", 30);
            }

            // Create backup if requested and file exists
            string? backupPath = null;
            if (createBackup && fileExists && !append)
            {
                backupPath = $"{safePath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Copy(safePath, backupPath, true);
                ReportProgress(context, "Created backup file", 50);
            }

            ReportProgress(context, "Writing file content...", 70);

            // Write the file
            if (append && fileExists)
            {
                await File.AppendAllTextAsync(safePath, content, encoding, context.CancellationToken);
            }
            else
            {
                await File.WriteAllTextAsync(safePath, content, encoding, context.CancellationToken);
            }

            ReportProgress(context, "File written successfully", 100);

            // Get file info for result
            var fileInfo = new FileInfo(safePath);
            var metadata = new Dictionary<string, object?>
            {
                ["file_size"] = fileInfo.Length,
                ["file_size_formatted"] = ToolHelpers.FormatFileSize(fileInfo.Length),
                ["encoding"] = encoding.EncodingName,
                ["line_count"] = content.Split('\n').Length,
                ["character_count"] = content.Length,
                ["operation"] = append ? "append" : "write",
                ["created_backup"] = backupPath != null,
                ["last_modified"] = fileInfo.LastWriteTimeUtc
            };

            if (backupPath != null)
            {
                metadata["backup_path"] = backupPath;
            }

            var operation = append ? "appended to" : (fileExists ? "overwritten" : "created");
            return ToolResults.FileSuccess(
                safePath,
                $"Successfully {operation} file: {fileInfo.Name}",
                metadata
            );
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResults.AccessDenied(filePath, "write");
        }
        catch (DirectoryNotFoundException)
        {
            return ToolResults.DirectoryNotFound(Path.GetDirectoryName(filePath) ?? filePath);
        }
        catch (IOException ex) when (ex.Message.Contains("being used"))
        {
            return ToolResults.Failure(
                $"File is in use and cannot be written: {filePath}",
                "FILE_IN_USE",
                details: ex
            );
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Failed to write file: {ex.Message}", "WRITE_ERROR", details: ex);
        }
    }
}
