using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.FileSystem;

/// <summary>
/// Tool for making a precise, in-place edit to a file by replacing an exact string.
/// Unlike <c>replace_text</c> (a bulk find/replace), this replaces a single unique
/// occurrence by default, failing if the target string is missing or ambiguous.
/// </summary>
public class EditFileTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "edit_file",
        Name = "Edit File",
        Description = "Makes a precise in-place edit to an existing file by replacing an exact, unique string. " +
            "By default the target string must occur exactly once; set replace_all to replace every occurrence. " +
            "Use this for surgical edits; use write_file to create/overwrite a whole file.",
        Version = "1.0.0",
        Category = ToolCategory.FileSystem,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead | ToolPermissionFlags.FileSystemWrite,
        Parameters =
        [
            new()
            {
                Name = "file_path",
                Description = "The path to the file to edit",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "old_string",
                Description = "The exact text to find and replace. Must be unique unless replace_all is true.",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "new_string",
                Description = "The text to replace old_string with",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "replace_all",
                Description = "Whether to replace every occurrence of old_string (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "create_backup",
                Description = "Whether to create a backup of the file before editing (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var filePath = GetParameter<string>(parameters, "file_path");
        var oldString = GetParameter<string>(parameters, "old_string");
        var newString = GetParameter<string>(parameters, "new_string");
        var replaceAll = GetParameter(parameters, "replace_all", false);
        var createBackup = GetParameter(parameters, "create_backup", true);

        if (string.IsNullOrEmpty(oldString))
        {
            return ToolResults.InvalidParameter("old_string", oldString, "must not be empty");
        }

        try
        {
            // Resolve and validate the file path
            var safePath = ToolHelpers.GetSafePath(filePath, context.WorkingDirectory);

            // Check if path is within allowed paths
            if (!ToolHelpers.IsPathWithinAllowedPaths(safePath, context.Permissions))
            {
                return ToolResults.Failure($"Path '{safePath}' is not within allowed paths", "PATH_NOT_ALLOWED");
            }

            if (!File.Exists(safePath))
            {
                return ToolResults.FileNotFound(safePath);
            }

            ReportProgress(context, "Reading file...", 10);

            // Preserve the file's existing encoding (and BOM/no-BOM) on write.
            var encoding = ToolHelpers.DetectEncoding(safePath);
            var content = await ToolHelpers.ReadTextFileAsync(safePath, encoding, context.CancellationToken);

            // old_string == new_string is a no-op; report success without touching the file.
            if (string.Equals(oldString, newString, StringComparison.Ordinal))
            {
                var noopMetadata = new Dictionary<string, object?>
                {
                    ["file_path"] = safePath,
                    ["replacements"] = 0,
                    ["created_backup"] = false
                };
                return ToolResults.Success(noopMetadata, "No change: old_string and new_string are identical", noopMetadata);
            }

            // Count occurrences (ordinal, exact match).
            var occurrences = CountOccurrences(content, oldString);

            if (occurrences == 0)
            {
                return ToolResults.Failure(
                    $"old_string not found in file: {safePath}",
                    "STRING_NOT_FOUND",
                    new { file_path = safePath }
                );
            }

            if (!replaceAll && occurrences > 1)
            {
                return ToolResults.Failure(
                    $"old_string found {occurrences} occurrences; not unique — set replace_all to replace all, or add more surrounding context to make it unique",
                    "STRING_NOT_UNIQUE",
                    new { file_path = safePath, occurrences }
                );
            }

            ReportProgress(context, "Applying edit...", 50);

            string updatedContent;
            int replacements;
            if (replaceAll)
            {
                updatedContent = content.Replace(oldString, newString, StringComparison.Ordinal);
                replacements = occurrences;
            }
            else
            {
                var index = content.IndexOf(oldString, StringComparison.Ordinal);
                updatedContent = string.Concat(
                    content.AsSpan(0, index),
                    newString,
                    content.AsSpan(index + oldString.Length));
                replacements = 1;
            }

            // Create backup if requested.
            string? backupPath = null;
            if (createBackup)
            {
                backupPath = ToolHelpers.GetBackupPath(safePath);
                File.Copy(safePath, backupPath, true);
                ReportProgress(context, "Created backup file", 70);
            }

            ReportProgress(context, "Writing file...", 85);
            await File.WriteAllTextAsync(safePath, updatedContent, encoding, context.CancellationToken);

            ReportProgress(context, "Edit complete", 100);

            var fileInfo = new FileInfo(safePath);
            var metadata = new Dictionary<string, object?>
            {
                ["file_path"] = safePath,
                ["file_name"] = fileInfo.Name,
                ["replacements"] = replacements,
                ["replace_all"] = replaceAll,
                ["created_backup"] = backupPath != null,
                ["file_size"] = fileInfo.Length,
                ["last_modified"] = fileInfo.LastWriteTimeUtc
            };

            if (backupPath != null)
            {
                metadata["backup_path"] = backupPath;
            }

            return ToolResults.Success(
                metadata,
                $"Successfully replaced {replacements} occurrence(s) in {fileInfo.Name}",
                metadata
            );
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResults.AccessDenied(filePath, "edit");
        }
        catch (DirectoryNotFoundException)
        {
            return ToolResults.DirectoryNotFound(Path.GetDirectoryName(filePath) ?? filePath);
        }
        catch (IOException ex) when (ex.Message.Contains("being used"))
        {
            return ToolResults.Failure(
                $"File is in use and cannot be edited: {filePath}",
                "FILE_IN_USE",
                details: ex
            );
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Failed to edit file: {ex.Message}", "EDIT_ERROR", details: ex);
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
