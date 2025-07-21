using System.Text;
using System.Text.RegularExpressions;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.Text;

/// <summary>
/// Tool for replacing text within files or content.
/// </summary>
public class ReplaceTextTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "replace_text",
        Name = "Replace Text",
        Description = "Replaces text patterns within files or directories with new text",
        Version = "1.0.0",
        Category = ToolCategory.TextProcessing,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead | ToolPermissionFlags.FileSystemWrite,
        Parameters =
        [
            new()
            {
                Name = "search_pattern",
                Description = "The text pattern to search for",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "replacement_text",
                Description = "The text to replace matches with",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "target_path",
                Description = "The file or directory path to process (default: current directory)",
                Type = "string",
                Required = false,
                DefaultValue = "."
            },
            new()
            {
                Name = "search_type",
                Description = "Type of search to perform",
                Type = "string",
                Required = false,
                DefaultValue = "contains",
                AllowedValues = ["contains", "regex", "exact", "starts_with", "ends_with"]
            },
            new()
            {
                Name = "case_sensitive",
                Description = "Whether the search should be case sensitive (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "whole_words_only",
                Description = "Whether to match whole words only (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "file_patterns",
                Description = "Array of file patterns to include (e.g., ['*.txt', '*.cs'])",
                Type = "array",
                Required = false,
                ItemType = new ToolParameter
                {
                    Type = "string",
                    Description = "File pattern (e.g., '*.txt')"
                }
            },
            new()
            {
                Name = "exclude_patterns",
                Description = "Array of file patterns to exclude (e.g., ['*.log', 'bin/*'])",
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
                Name = "recursive",
                Description = "Whether to process files recursively in subdirectories (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "create_backup",
                Description = "Whether to create backup files before modification (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "dry_run",
                Description = "Whether to perform a dry run without making changes (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "max_file_size_mb",
                Description = "Maximum file size to process in MB (default: 10MB)",
                Type = "number",
                Required = false,
                DefaultValue = 10,
                MinValue = 0.1,
                MaxValue = 100
            },
            new()
            {
                Name = "encoding",
                Description = "File encoding to use (default: auto-detect)",
                Type = "string",
                Required = false,
                AllowedValues = ["utf-8", "ascii", "unicode", "utf-16", "utf-32"]
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var searchPattern = GetParameter<string>(parameters, "search_pattern");
        var replacementText = GetParameter<string>(parameters, "replacement_text");
        var targetPath = GetParameter(parameters, "target_path", ".");
        var searchType = GetParameter(parameters, "search_type", "contains");
        var caseSensitive = GetParameter(parameters, "case_sensitive", false);
        var wholeWordsOnly = GetParameter(parameters, "whole_words_only", false);
        var filePatterns = GetParameter<List<string>>(parameters, "file_patterns", []);
        var excludePatterns = GetParameter<List<string>>(parameters, "exclude_patterns", []);
        var recursive = GetParameter(parameters, "recursive", true);
        var createBackup = GetParameter(parameters, "create_backup", true);
        var dryRun = GetParameter(parameters, "dry_run", false);
        var maxFileSizeMb = GetParameter<double>(parameters, "max_file_size_mb", 10);
        var encodingName = GetParameter<string>(parameters, "encoding");

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

            ReportProgress(context, "Preparing replacement operation...", 5);

            var replaceResults = new List<FileReplaceResult>();
            var replaceStats = new ReplaceStatistics();
            var regex = CreateSearchRegex(searchPattern, searchType, caseSensitive, wholeWordsOnly);
            var maxFileSizeBytes = (long)(maxFileSizeMb * 1024 * 1024);

            if (File.Exists(safeTargetPath))
            {
                // Process a single file
                await ProcessFileAsync(safeTargetPath, regex, replacementText, replaceResults, replaceStats, createBackup, dryRun, maxFileSizeBytes, encodingName, context);
            }
            else
            {
                // Process directory
                await ProcessDirectoryAsync(safeTargetPath, regex, replacementText, filePatterns, excludePatterns, recursive, replaceResults, replaceStats, createBackup, dryRun, maxFileSizeBytes, encodingName, context);
            }

            ReportProgress(context, "Replacement operation completed", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["search_pattern"] = searchPattern,
                ["replacement_text"] = replacementText,
                ["target_path"] = safeTargetPath,
                ["search_type"] = searchType,
                ["case_sensitive"] = caseSensitive,
                ["whole_words_only"] = wholeWordsOnly,
                ["recursive"] = recursive,
                ["dry_run"] = dryRun,
                ["create_backup"] = createBackup,
                ["files_processed"] = replaceStats.FilesProcessed,
                ["files_modified"] = replaceStats.FilesModified,
                ["total_replacements"] = replaceStats.TotalReplacements,
                ["operation_duration"] = replaceStats.OperationDuration,
                ["errors"] = replaceStats.Errors
            };

            var message = dryRun
                ? $"Dry run: Would replace {replaceStats.TotalReplacements} occurrences in {replaceStats.FilesModified} files"
                : $"Replaced {replaceStats.TotalReplacements} occurrences in {replaceStats.FilesModified} files";

            return ToolResults.ListSuccess(
                replaceResults,
                message,
                replaceStats.TotalReplacements
            );
        }
        catch (ArgumentException ex) when (ex.Message.Contains("parsing"))
        {
            return ToolResults.InvalidParameter("search_pattern", searchPattern, $"Invalid regex pattern: {ex.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResults.AccessDenied(targetPath, "replace text in");
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Text replacement failed: {ex.Message}", "REPLACE_ERROR", details: ex);
        }
    }

    private static Regex CreateSearchRegex(string pattern, string searchType, bool caseSensitive, bool wholeWordsOnly)
    {
        var regexPattern = searchType.ToLowerInvariant() switch
        {
            "regex" => pattern,
            "exact" => Regex.Escape(pattern),
            "starts_with" => "^" + Regex.Escape(pattern),
            "ends_with" => Regex.Escape(pattern) + "$",
            _ => Regex.Escape(pattern) // contains
        };

        if (wholeWordsOnly && searchType != "regex")
        {
            regexPattern = @"\b" + regexPattern + @"\b";
        }

        var options = RegexOptions.Compiled;
        if (!caseSensitive)
        {
            options |= RegexOptions.IgnoreCase;
        }

        return new Regex(regexPattern, options);
    }

    private static async Task ProcessFileAsync(
        string filePath,
        Regex regex,
        string replacementText,
        List<FileReplaceResult> results,
        ReplaceStatistics stats,
        bool createBackup,
        bool dryRun,
        long maxFileSizeBytes,
        string? encodingName,
        ToolExecutionContext context)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);

            // Check file size
            if (fileInfo.Length > maxFileSizeBytes)
            {
                stats.Errors.Add($"File {filePath} is too large ({ToolHelpers.FormatFileSize(fileInfo.Length)})");
                return;
            }

            // Skip binary files
            if (!IsTextFile(filePath))
            {
                stats.Errors.Add($"Skipping binary file: {filePath}");
                return;
            }

            stats.FilesProcessed++;

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
                : ToolHelpers.DetectEncoding(filePath);

            // Read file content
            var originalContent = await File.ReadAllTextAsync(filePath, encoding, context.CancellationToken);
            var matches = regex.Matches(originalContent);

            if (matches.Count > 0)
            {
                var newContent = regex.Replace(originalContent, replacementText);
                var replacementCount = matches.Count;

                var result = new FileReplaceResult
                {
                    FilePath = filePath,
                    ReplacementCount = replacementCount,
                    OriginalSize = originalContent.Length,
                    NewSize = newContent.Length,
                    Encoding = encoding.EncodingName,
                    BackupCreated = false,
                    Modified = !dryRun,
                    // Collect sample matches for preview
                    SampleMatches = [.. matches.Cast<Match>()
                        .Take(5)
                        .Select(m => new MatchPreview
                        {
                            OriginalText = m.Value,
                            ReplacementText = regex.Replace(m.Value, replacementText),
                            Position = m.Index,
                            LineNumber = GetLineNumber(originalContent, m.Index)
                        })]
                };

                if (!dryRun)
                {
                    // Create backup if requested
                    if (createBackup)
                    {
                        var backupPath = $"{filePath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
                        File.Copy(filePath, backupPath, true);
                        result.BackupPath = backupPath;
                        result.BackupCreated = true;
                    }

                    // Write the modified content
                    await File.WriteAllTextAsync(filePath, newContent, encoding, context.CancellationToken);
                }

                results.Add(result);
                stats.FilesModified++;
                stats.TotalReplacements += replacementCount;
            }
        }
        catch (Exception ex)
        {
            stats.Errors.Add($"Error processing file {filePath}: {ex.Message}");
        }
    }

    private async Task ProcessDirectoryAsync(
        string directoryPath,
        Regex regex,
        string replacementText,
        List<string> filePatterns,
        List<string> excludePatterns,
        bool recursive,
        List<FileReplaceResult> results,
        ReplaceStatistics stats,
        bool createBackup,
        bool dryRun,
        long maxFileSizeBytes,
        string? encodingName,
        ToolExecutionContext context)
    {
        try
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var directory = new DirectoryInfo(directoryPath);

            // Get all files to process
            var allFiles = new List<FileInfo>();

            if (filePatterns.Count > 0)
            {
                foreach (var pattern in filePatterns)
                {
                    allFiles.AddRange(directory.GetFiles(pattern, searchOption));
                }
            }
            else
            {
                allFiles.AddRange(directory.GetFiles("*", searchOption));
            }

            // Filter out excluded files
            var filesToProcess = allFiles.Where(f => !ShouldExcludeFile(f.Name, f.FullName, excludePatterns)).ToList();

            var totalFiles = filesToProcess.Count;
            var processedFiles = 0;

            foreach (var file in filesToProcess)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                await ProcessFileAsync(file.FullName, regex, replacementText, results, stats, createBackup, dryRun, maxFileSizeBytes, encodingName, context);

                processedFiles++;
                var progressPercent = Math.Min(95, 10 + (processedFiles * 80 / Math.Max(1, totalFiles)));
                ReportProgress(context, $"Processing: {file.Name} ({processedFiles}/{totalFiles})", progressPercent);
            }
        }
        catch (Exception ex)
        {
            stats.Errors.Add($"Error processing directory {directoryPath}: {ex.Message}");
        }
    }

    private static bool ShouldExcludeFile(string fileName, string fullPath, List<string> excludePatterns)
    {
        return excludePatterns.Any(pattern =>
        {
            // Check against file name
            if (pattern.Contains('*'))
            {
                var regexPattern = "^" + pattern.Replace("*", ".*") + "$";
                if (Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check against full path for directory patterns
            if (pattern.Contains('/') || pattern.Contains('\\'))
            {
                var normalizedPath = fullPath.Replace('\\', '/');
                var normalizedPattern = pattern.Replace('\\', '/');

                if (normalizedPattern.Contains('*'))
                {
                    var regexPattern = normalizedPattern.Replace("*", ".*");
                    return Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase);
                }
                else
                {
                    return normalizedPath.Contains(normalizedPattern, StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        });
    }

    private static bool IsTextFile(string filePath)
    {
        try
        {
            var buffer = new byte[1024];
            using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var bytesRead = file.Read(buffer, 0, buffer.Length);

            // Check for null bytes (common in binary files)
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0)
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int GetLineNumber(string content, int position)
    {
        return content.Take(position).Count(c => c == '\n') + 1;
    }

    private class FileReplaceResult
    {
        public string FilePath { get; set; } = "";
        public int ReplacementCount { get; set; }
        public int OriginalSize { get; set; }
        public int NewSize { get; set; }
        public string Encoding { get; set; } = "";
        public bool BackupCreated { get; set; }
        public string? BackupPath { get; set; }
        public bool Modified { get; set; }
        public List<MatchPreview> SampleMatches { get; set; } = [];
    }

    private class MatchPreview
    {
        public string OriginalText { get; set; } = "";
        public string ReplacementText { get; set; } = "";
        public int Position { get; set; }
        public int LineNumber { get; set; }
    }

    private class ReplaceStatistics
    {
        public int FilesProcessed { get; set; }
        public int FilesModified { get; set; }
        public int TotalReplacements { get; set; }
        public List<string> Errors { get; set; } = [];
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public TimeSpan OperationDuration => DateTime.UtcNow - StartTime;
    }
}
