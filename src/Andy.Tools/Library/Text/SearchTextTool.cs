using System.Text.RegularExpressions;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.Text;

/// <summary>
/// Tool for searching text within files or content.
/// </summary>
public class SearchTextTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "search_text",
        Name = "Search Text",
        Description = "Searches for text patterns within files or directories using various search methods",
        Version = "1.0.0",
        Category = ToolCategory.TextProcessing,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
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
                Name = "target_path",
                Description = "The file or directory path to search in (default: current directory)",
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
                Description = "Whether to search recursively in subdirectories (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "max_results",
                Description = "Maximum number of results to return (default: 100)",
                Type = "integer",
                Required = false,
                DefaultValue = 100,
                MinValue = 1,
                MaxValue = 1000
            },
            new()
            {
                Name = "context_lines",
                Description = "Number of context lines to include around matches (default: 2)",
                Type = "integer",
                Required = false,
                DefaultValue = 2,
                MinValue = 0,
                MaxValue = 10
            },
            new()
            {
                Name = "include_line_numbers",
                Description = "Whether to include line numbers in results (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var searchPattern = GetParameter<string>(parameters, "search_pattern");
        var targetPath = GetParameter(parameters, "target_path", ".");
        var searchType = GetParameter(parameters, "search_type", "contains");
        var caseSensitive = GetParameter(parameters, "case_sensitive", false);
        var wholeWordsOnly = GetParameter(parameters, "whole_words_only", false);
        var filePatterns = GetParameter<List<string>>(parameters, "file_patterns", []);
        var excludePatterns = GetParameter<List<string>>(parameters, "exclude_patterns", []);
        var recursive = GetParameter(parameters, "recursive", true);
        var maxResults = GetParameter(parameters, "max_results", 100);
        var contextLines = GetParameter(parameters, "context_lines", 2);
        var includeLineNumbers = GetParameter(parameters, "include_line_numbers", true);

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

            ReportProgress(context, "Preparing search...", 5);

            var searchResults = new List<SearchMatch>();
            var searchStats = new SearchStatistics();
            var regex = CreateSearchRegex(searchPattern, searchType, caseSensitive, wholeWordsOnly);

            if (File.Exists(safeTargetPath))
            {
                // Search in a single file
                await SearchFileAsync(safeTargetPath, regex, searchResults, searchStats, maxResults, contextLines, includeLineNumbers, context);
            }
            else
            {
                // Search in directory
                await SearchDirectoryAsync(safeTargetPath, regex, filePatterns, excludePatterns, recursive, searchResults, searchStats, maxResults, contextLines, includeLineNumbers, context);
            }

            ReportProgress(context, "Search completed", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["search_pattern"] = searchPattern,
                ["target_path"] = safeTargetPath,
                ["search_type"] = searchType,
                ["case_sensitive"] = caseSensitive,
                ["whole_words_only"] = wholeWordsOnly,
                ["recursive"] = recursive,
                ["files_searched"] = searchStats.FilesSearched,
                ["files_with_matches"] = searchStats.FilesWithMatches,
                ["total_matches"] = searchStats.TotalMatches,
                ["search_duration"] = searchStats.SearchDuration,
                ["results_truncated"] = searchResults.Count >= maxResults
            };

            var message = $"Found {searchStats.TotalMatches} matches in {searchStats.FilesWithMatches} files (searched {searchStats.FilesSearched} files)";

            return ToolResults.ListSuccess(
                searchResults.Take(maxResults).ToList(),
                message,
                searchStats.TotalMatches
            );
        }
        catch (ArgumentException ex) when (ex.Message.Contains("parsing"))
        {
            return ToolResults.InvalidParameter("search_pattern", searchPattern, $"Invalid regex pattern: {ex.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResults.AccessDenied(targetPath, "search");
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Search failed: {ex.Message}", "SEARCH_ERROR", details: ex);
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

    private static async Task SearchFileAsync(
        string filePath,
        Regex regex,
        List<SearchMatch> results,
        SearchStatistics stats,
        int maxResults,
        int contextLines,
        bool includeLineNumbers,
        ToolExecutionContext context)
    {
        try
        {
            stats.FilesSearched++;
            var lines = await File.ReadAllLinesAsync(filePath, context.CancellationToken);
            var fileHasMatches = false;

            for (int lineIndex = 0; lineIndex < lines.Length && results.Count < maxResults; lineIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var line = lines[lineIndex];
                var matches = regex.Matches(line);

                if (matches.Count > 0)
                {
                    fileHasMatches = true;
                    stats.TotalMatches += matches.Count;

                    foreach (Match match in matches)
                    {
                        if (results.Count >= maxResults)
                        {
                            break;
                        }

                        var searchMatch = new SearchMatch
                        {
                            FilePath = filePath,
                            LineNumber = includeLineNumbers ? lineIndex + 1 : null,
                            MatchText = match.Value,
                            FullLine = line,
                            StartPosition = match.Index,
                            EndPosition = match.Index + match.Length
                        };

                        // Add context lines
                        if (contextLines > 0)
                        {
                            var contextStart = Math.Max(0, lineIndex - contextLines);
                            var contextEnd = Math.Min(lines.Length - 1, lineIndex + contextLines);

                            for (int i = contextStart; i <= contextEnd; i++)
                            {
                                searchMatch.ContextLines.Add(new ContextLine
                                {
                                    LineNumber = includeLineNumbers ? i + 1 : null,
                                    Content = lines[i],
                                    IsMatch = i == lineIndex
                                });
                            }
                        }

                        results.Add(searchMatch);
                    }
                }
            }

            if (fileHasMatches)
            {
                stats.FilesWithMatches++;
            }
        }
        catch (Exception ex)
        {
            stats.Errors.Add($"Error searching file {filePath}: {ex.Message}");
        }
    }

    private async Task SearchDirectoryAsync(
        string directoryPath,
        Regex regex,
        List<string> filePatterns,
        List<string> excludePatterns,
        bool recursive,
        List<SearchMatch> results,
        SearchStatistics stats,
        int maxResults,
        int contextLines,
        bool includeLineNumbers,
        ToolExecutionContext context)
    {
        try
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var directory = new DirectoryInfo(directoryPath);

            // Get all files to search
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
            var filesToSearch = allFiles.Where(f => !ShouldExcludeFile(f.Name, f.FullName, excludePatterns)).ToList();

            var totalFiles = filesToSearch.Count;
            var processedFiles = 0;

            foreach (var file in filesToSearch)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (results.Count >= maxResults)
                {
                    break;
                }

                try
                {
                    // Skip binary files
                    if (!IsTextFile(file.FullName))
                    {
                        continue;
                    }

                    await SearchFileAsync(file.FullName, regex, results, stats, maxResults, contextLines, includeLineNumbers, context);

                    processedFiles++;
                    var progressPercent = Math.Min(95, 10 + (processedFiles * 80 / Math.Max(1, totalFiles)));
                    ReportProgress(context, $"Searching: {file.Name} ({processedFiles}/{totalFiles})", progressPercent);
                }
                catch (Exception ex)
                {
                    stats.Errors.Add($"Error processing file {file.FullName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            stats.Errors.Add($"Error searching directory {directoryPath}: {ex.Message}");
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

    private class SearchMatch
    {
        public string FilePath { get; set; } = "";
        public int? LineNumber { get; set; }
        public string MatchText { get; set; } = "";
        public string FullLine { get; set; } = "";
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        public List<ContextLine> ContextLines { get; set; } = [];
    }

    private class ContextLine
    {
        public int? LineNumber { get; set; }
        public string Content { get; set; } = "";
        public bool IsMatch { get; set; }
    }

    private class SearchStatistics
    {
        public int FilesSearched { get; set; }
        public int FilesWithMatches { get; set; }
        public int TotalMatches { get; set; }
        public List<string> Errors { get; set; } = [];
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public TimeSpan SearchDuration => DateTime.UtcNow - StartTime;
    }
}
