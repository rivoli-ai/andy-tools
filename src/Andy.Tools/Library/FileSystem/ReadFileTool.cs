using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.FileSystem;

/// <summary>
/// Tool for reading file contents.
/// </summary>
public class ReadFileTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "read_file",
        Name = "Read File",
        Description = "Reads the contents of a text file and returns it as a string",
        Version = "1.0.0",
        Category = ToolCategory.FileSystem,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
        Parameters =
        [
            new()
            {
                Name = "file_path",
                Description = "The path to the file to read",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "encoding",
                Description = "The text encoding to use (utf-8, ascii, unicode, etc.). Auto-detected if not specified.",
                Type = "string",
                Required = false,
                AllowedValues = ["utf-8", "ascii", "unicode", "utf-16", "utf-32"]
            },
            new()
            {
                Name = "max_size_mb",
                Description = "Maximum file size to read in MB (default: 10MB)",
                Type = "number",
                Required = false,
                DefaultValue = 10,
                MinValue = 0.1,
                MaxValue = 100
            },
            new()
            {
                Name = "start_line",
                Description = "Starting line number (1-based) for partial read",
                Type = "integer",
                Required = false,
                MinValue = 1
            },
            new()
            {
                Name = "end_line",
                Description = "Ending line number (1-based) for partial read",
                Type = "integer",
                Required = false,
                MinValue = 1
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var filePath = GetParameter<string>(parameters, "file_path");
        var encodingName = GetParameter<string>(parameters, "encoding");
        var maxSizeMb = GetParameter<double>(parameters, "max_size_mb", 10);
        var startLine = GetParameter<int?>(parameters, "start_line");
        var endLine = GetParameter<int?>(parameters, "end_line");

        try
        {
            // Resolve and validate the file path
            var safePath = ToolHelpers.GetSafePath(filePath, context.WorkingDirectory);

            if (!File.Exists(safePath))
            {
                return ToolResults.FileNotFound(safePath);
            }

            // Check if path is within allowed paths
            if (!ToolHelpers.IsPathWithinAllowedPaths(safePath, context.Permissions))
            {
                return ToolResults.Failure($"Path '{safePath}' is not within allowed paths", "PATH_NOT_ALLOWED");
            }

            // Check file size
            var fileInfo = new FileInfo(safePath);
            var maxSizeBytes = (long)(maxSizeMb * 1024 * 1024);

            if (fileInfo.Length > maxSizeBytes)
            {
                return ToolResults.Failure(
                    $"File is too large ({ToolHelpers.FormatFileSize(fileInfo.Length)}). Maximum allowed size is {maxSizeMb}MB",
                    "FILE_TOO_LARGE",
                    new { file_size = fileInfo.Length, max_size = maxSizeBytes }
                );
            }

            ReportProgress(context, "Reading file...", 10);

            // Determine encoding
            Encoding encoding = !string.IsNullOrEmpty(encodingName)
                ? encodingName.ToLowerInvariant() switch
                {
                    "utf-8" => Encoding.UTF8,
                    "ascii" => Encoding.ASCII,
                    "unicode" or "utf-16" => Encoding.Unicode,
                    "utf-32" => Encoding.UTF32,
                    _ => throw new ArgumentException($"Unsupported encoding: {encodingName}")
                }
                : ToolHelpers.DetectEncoding(safePath);
            ReportProgress(context, "Reading file content...", 50);

            // Read the file
            string content;
            if (startLine.HasValue || endLine.HasValue)
            {
                // Read specific lines
                content = await ReadFileLinesAsync(safePath, encoding, startLine, endLine, context.CancellationToken);
            }
            else
            {
                // Read entire file
                content = await ToolHelpers.ReadTextFileAsync(safePath, encoding, context.CancellationToken);
            }

            ReportProgress(context, "File read successfully", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["file_size"] = fileInfo.Length,
                ["file_size_formatted"] = ToolHelpers.FormatFileSize(fileInfo.Length),
                ["encoding"] = encoding.EncodingName,
                ["line_count"] = content.Split('\n').Length,
                ["character_count"] = content.Length,
                ["last_modified"] = fileInfo.LastWriteTimeUtc
            };

            if (startLine.HasValue)
            {
                metadata["start_line"] = startLine.Value;
            }

            if (endLine.HasValue)
            {
                metadata["end_line"] = endLine.Value;
            }

            return ToolResults.TextSuccess(
                content,
                "text/plain",
                $"Successfully read file: {fileInfo.Name}",
                metadata
            );
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResults.AccessDenied(filePath, "read");
        }
        catch (DirectoryNotFoundException)
        {
            return ToolResults.DirectoryNotFound(Path.GetDirectoryName(filePath) ?? filePath);
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Failed to read file: {ex.Message}", "READ_ERROR", details: ex);
        }
    }

    private static async Task<string> ReadFileLinesAsync(string filePath, Encoding encoding, int? startLine, int? endLine, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        var currentLine = 1;
        var start = startLine ?? 1;
        var end = endLine ?? int.MaxValue;

        using var reader = new StreamReader(filePath, encoding);

        while (!reader.EndOfStream && currentLine <= end)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line != null && currentLine >= start)
            {
                lines.Add(line);
            }

            currentLine++;
        }

        return string.Join('\n', lines);
    }
}
