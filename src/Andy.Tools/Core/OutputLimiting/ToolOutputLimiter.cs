using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Andy.Tools.Core.OutputLimiting;

/// <summary>
/// Default implementation of tool output limiter.
/// </summary>
public class ToolOutputLimiter : IToolOutputLimiter
{
    private readonly ToolOutputLimiterOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolOutputLimiter"/> class.
    /// </summary>
    /// <param name="options">The output limiter options.</param>
    public ToolOutputLimiter(IOptions<ToolOutputLimiterOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public LimitedOutput LimitOutput(object output, OutputType outputType, OutputLimitContext? context = null)
    {
        context ??= GetDefaultContext(outputType);

        var originalSize = EstimateSize(output);

        if (!NeedsLimiting(output, outputType))
        {
            return new LimitedOutput
            {
                Content = output,
                WasTruncated = false,
                OriginalSize = originalSize,
                TruncatedSize = originalSize
            };
        }

        return outputType switch
        {
            OutputType.FileList => LimitFileList(output, context),
            OutputType.FileContent => LimitFileContent(output, context),
            OutputType.DirectoryTree => LimitDirectoryTree(output, context),
            OutputType.StructuredData => LimitStructuredData(output, context),
            OutputType.Logs => LimitLogs(output, context),
            _ => LimitGenericText(output, context)
        };
    }

    /// <inheritdoc />
    public bool NeedsLimiting(object output, OutputType outputType)
    {
        var size = EstimateSize(output);
        var maxSize = GetMaxSize(outputType);

        // Check if output is a list
        if (output is IList<object> list)
        {
            return list.Count > GetMaxItems(outputType) || size > maxSize;
        }

        // Check if output is a dictionary with "items" key (from ListSuccess)
        if (output is Dictionary<string, object?> dict && dict.TryGetValue("items", out var itemsValue) && itemsValue is System.Collections.IList itemsList)
        {
            return itemsList.Count > GetMaxItems(outputType) || size > maxSize;
        }

        return size > maxSize;
    }

    private long EstimateSize(object? output)
    {
        return output switch
        {
            null => 0,
            string str => Encoding.UTF8.GetByteCount(str),
            IList<object> list => EstimateListSize(list),
            _ => EstimateJsonSize(output)
        };
    }

    private long EstimateListSize(IList<object> list)
    {
        long totalSize = 0;
        foreach (var item in list.Take(1000)) // Sample first 1000 items
        {
            totalSize += EstimateSize(item);
        }

        if (list.Count > 1000)
        {
            // Estimate the rest based on average
            var avgSize = totalSize / 1000;
            totalSize += avgSize * (list.Count - 1000);
        }

        return totalSize;
    }

    private long EstimateJsonSize(object obj)
    {
        try
        {
            var json = JsonSerializer.Serialize(obj);
            return Encoding.UTF8.GetByteCount(json);
        }
        catch
        {
            return 0;
        }
    }

    private OutputLimitContext GetDefaultContext(OutputType outputType)
    {
        return outputType switch
        {
            OutputType.FileList => new OutputLimitContext
            {
                MaxCharacters = _options.MaxFileListCharacters,
                MaxItems = _options.MaxFileListEntries,
                IncludeSummary = true,
                ProvideSuggestions = true
            },
            OutputType.FileContent => new OutputLimitContext
            {
                MaxCharacters = _options.MaxFileContentCharacters,
                MaxLines = _options.MaxLinesPerFile,
                IncludeSummary = false,
                ProvideSuggestions = false
            },
            _ => new OutputLimitContext
            {
                MaxCharacters = _options.MaxOutputCharacters,
                IncludeSummary = _options.EnableSmartSummaries,
                ProvideSuggestions = _options.EnableSmartSummaries
            }
        };
    }

    private long GetMaxSize(OutputType outputType)
    {
        return outputType switch
        {
            OutputType.FileList => _options.MaxFileListCharacters,
            OutputType.FileContent => _options.MaxFileContentCharacters,
            _ => _options.MaxOutputCharacters
        };
    }

    private int GetMaxItems(OutputType outputType)
    {
        return outputType switch
        {
            OutputType.FileList => _options.MaxFileListEntries,
            _ => int.MaxValue
        };
    }

    private LimitedOutput LimitFileList(object output, OutputLimitContext context)
    {
        IList<object>? fileList = null;
        bool isDictionary = false;

        // Check if output is a direct list
        if (output is IList<object> directList)
        {
            fileList = directList;
        }
        // Check if output is a dictionary with "items" key (from ListSuccess)
        else if (output is Dictionary<string, object?> outputDict && outputDict.TryGetValue("items", out var itemsValue))
        {
            isDictionary = true;
            // Convert any IList to IList<object>
            if (itemsValue is System.Collections.IList items)
            {
                fileList = new List<object>();
                foreach (var item in items)
                {
                    fileList.Add(item);
                }
            }
        }

        if (fileList == null)
        {
            return LimitGenericText(output, context);
        }

        var result = new LimitedOutput
        {
            OriginalSize = EstimateSize(output),
            WasTruncated = true,
            TruncationReason = $"File list exceeded {context.MaxItems} items or {context.MaxCharacters} characters"
        };

        var maxItems = context.MaxItems ?? _options.MaxFileListEntries;
        var limitedList = new List<object>();
        var currentSize = 0L;
        var maxSize = context.MaxCharacters ?? _options.MaxFileListCharacters;

        // Group files by directory
        var fileGroups = GroupFilesByDirectory(fileList);

        // Create summary
        if (context.IncludeSummary)
        {
            result.Summary = CreateFileListSummary(fileList, fileGroups);
        }

        // Add files up to limit
        foreach (var item in fileList.Take(maxItems))
        {
            var itemSize = EstimateSize(item);
            if (currentSize + itemSize > maxSize)
            {
                break;
            }

            limitedList.Add(item);
            currentSize += itemSize;
        }

        // Return in the same format as the input
        if (isDictionary && output is Dictionary<string, object?> dict)
        {
            // Create a new dictionary with the limited items
            var limitedDict = new Dictionary<string, object?>
            {
                ["items"] = limitedList,
                ["count"] = limitedList.Count,
                ["total_count"] = fileList.Count
            };
            
            // Preserve other keys from the original dictionary
            foreach (var kvp in dict)
            {
                if (kvp.Key != "items" && kvp.Key != "count" && kvp.Key != "total_count")
                {
                    limitedDict[kvp.Key] = kvp.Value;
                }
            }
            
            result.Content = limitedDict;
        }
        else
        {
            result.Content = limitedList;
        }
        
        result.TruncatedSize = currentSize;

        // Add suggestions
        if (context.ProvideSuggestions)
        {
            result.Suggestions.AddRange(GenerateFileListSuggestions(fileGroups));
        }

        return result;
    }

    private Dictionary<string, List<object>> GroupFilesByDirectory(IList<object> fileList)
    {
        var groups = new Dictionary<string, List<object>>();

        foreach (var file in fileList)
        {
            var path = ExtractPath(file);
            var directory = Path.GetDirectoryName(path) ?? "/";

            if (!groups.ContainsKey(directory))
            {
                groups[directory] = new List<object>();
            }

            groups[directory].Add(file);
        }

        return groups;
    }

    private string ExtractPath(object fileItem)
    {
        // Try to extract path from various possible formats
        if (fileItem is string str)
        {
            return str;
        }

        var type = fileItem.GetType();
        var pathProperty = type.GetProperty("full_path") ??
                          type.GetProperty("FullPath") ??
                          type.GetProperty("path") ??
                          type.GetProperty("Path");

        if (pathProperty != null)
        {
            return pathProperty.GetValue(fileItem)?.ToString() ?? "";
        }

        return fileItem.ToString() ?? "";
    }

    private OutputSummary CreateFileListSummary(IList<object> fileList, Dictionary<string, List<object>> fileGroups)
    {
        var summary = new OutputSummary
        {
            TotalCount = fileList.Count,
            ShownCount = Math.Min(fileList.Count, _options.MaxFileListEntries)
        };

        // File type statistics
        var extensions = new Dictionary<string, int>();
        var fileCount = 0;
        var dirCount = 0;

        foreach (var item in fileList)
        {
            if (IsDirectory(item))
            {
                dirCount++;
            }
            else
            {
                fileCount++;
                var ext = Path.GetExtension(ExtractPath(item)).ToLowerInvariant();
                if (!string.IsNullOrEmpty(ext))
                {
                    extensions[ext] = extensions.GetValueOrDefault(ext) + 1;
                }
            }
        }

        summary.Statistics["file_count"] = fileCount;
        summary.Statistics["directory_count"] = dirCount;
        summary.Statistics["unique_extensions"] = extensions.Count;

        // Top extensions
        var topExtensions = extensions
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .Select(kvp => $"{kvp.Key}: {kvp.Value}")
            .ToList();
        summary.Statistics["top_extensions"] = topExtensions;

        // Directory groups
        summary.Groups = fileGroups
            .OrderByDescending(kvp => kvp.Value.Count)
            .Take(10)
            .Select(kvp => new OutputGroup
            {
                Name = kvp.Key,
                Count = kvp.Value.Count,
                SampleItems = kvp.Value.Take(3).Select(f => Path.GetFileName(ExtractPath(f))).ToList()
            })
            .ToList();

        return summary;
    }

    private bool IsDirectory(object item)
    {
        var type = item.GetType();
        var typeProperty = type.GetProperty("type") ?? type.GetProperty("Type");
        if (typeProperty != null)
        {
            var value = typeProperty.GetValue(item)?.ToString();
            return value?.Equals("directory", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        var isFileProperty = type.GetProperty("IsFile") ?? type.GetProperty("is_file");
        if (isFileProperty != null && isFileProperty.PropertyType == typeof(bool))
        {
            return !(bool)(isFileProperty.GetValue(item) ?? true);
        }

        return false;
    }

    private List<string> GenerateFileListSuggestions(Dictionary<string, List<object>> fileGroups)
    {
        var suggestions = new List<string>();

        if (fileGroups.Count > 1)
        {
            var largestDir = fileGroups.OrderByDescending(kvp => kvp.Value.Count).First();
            suggestions.Add($"List files in specific directory: '{largestDir.Key}'");
        }

        suggestions.Add("Use pattern parameter to filter by file type (e.g., '*.cs')");
        suggestions.Add("Set recursive=false to list only the top-level directory");
        suggestions.Add("Use max_depth parameter to limit recursion depth");

        return suggestions;
    }

    private LimitedOutput LimitFileContent(object output, OutputLimitContext context)
    {
        var content = output?.ToString() ?? "";
        var lines = content.Split('\n');
        var maxLines = context.MaxLines ?? _options.MaxLinesPerFile;

        if (lines.Length <= maxLines)
        {
            return new LimitedOutput
            {
                Content = output!,
                WasTruncated = false,
                OriginalSize = EstimateSize(output),
                TruncatedSize = EstimateSize(output)
            };
        }

        var truncatedLines = lines.Take(maxLines).ToArray();
        var truncatedContent = string.Join('\n', truncatedLines);

        return new LimitedOutput
        {
            Content = truncatedContent + $"\n\n... ({lines.Length - maxLines} more lines)",
            WasTruncated = true,
            OriginalSize = EstimateSize(output),
            TruncatedSize = EstimateSize(truncatedContent),
            TruncationReason = $"File content exceeded {maxLines} lines"
        };
    }

    private LimitedOutput LimitDirectoryTree(object output, OutputLimitContext context)
    {
        // Similar to file list but with tree structure preservation
        return LimitFileList(output, context);
    }

    private LimitedOutput LimitStructuredData(object output, OutputLimitContext context)
    {
        var json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        if (json.Length <= (context.MaxCharacters ?? _options.MaxOutputCharacters))
        {
            return new LimitedOutput
            {
                Content = output,
                WasTruncated = false,
                OriginalSize = json.Length,
                TruncatedSize = json.Length
            };
        }

        // For structured data, try to preserve structure
        try
        {
            var truncatedJson = json.Substring(0, context.MaxCharacters ?? _options.MaxOutputCharacters);
            var lastCompleteIndex = FindLastCompleteJsonElement(truncatedJson);
            truncatedJson = truncatedJson.Substring(0, lastCompleteIndex) + "\n... (truncated)";

            return new LimitedOutput
            {
                Content = truncatedJson,
                WasTruncated = true,
                OriginalSize = json.Length,
                TruncatedSize = truncatedJson.Length,
                TruncationReason = "Structured data exceeded size limit"
            };
        }
        catch
        {
            return LimitGenericText(json, context);
        }
    }

    private int FindLastCompleteJsonElement(string json)
    {
        var depth = 0;
        var inString = false;
        var lastCompleteIndex = 0;

        for (int i = 0; i < json.Length; i++)
        {
            if (json[i] == '"' && (i == 0 || json[i - 1] != '\\'))
            {
                inString = !inString;
            }

            if (!inString)
            {
                switch (json[i])
                {
                    case '{':
                    case '[':
                        depth++;
                        break;
                    case '}':
                    case ']':
                        depth--;
                        if (depth == 0)
                        {
                            lastCompleteIndex = i + 1;
                        }

                        break;
                    case ',':
                        if (depth == 1)
                        {
                            lastCompleteIndex = i;
                        }

                        break;
                }
            }
        }

        return lastCompleteIndex > 0 ? lastCompleteIndex : json.Length;
    }

    private LimitedOutput LimitLogs(object output, OutputLimitContext context)
    {
        var content = output?.ToString() ?? "";
        var lines = content.Split('\n');
        var maxLines = context.MaxLines ?? _options.MaxLinesPerFile;

        if (lines.Length <= maxLines)
        {
            return new LimitedOutput
            {
                Content = output!,
                WasTruncated = false,
                OriginalSize = EstimateSize(output),
                TruncatedSize = EstimateSize(output)
            };
        }

        // For logs, show both beginning and end
        var headLines = maxLines / 2;
        var tailLines = maxLines - headLines;

        var truncatedLines = lines.Take(headLines)
            .Concat(new[] { $"\n... ({lines.Length - maxLines} lines omitted) ...\n" })
            .Concat(lines.Skip(lines.Length - tailLines));

        var truncatedContent = string.Join('\n', truncatedLines);

        return new LimitedOutput
        {
            Content = truncatedContent,
            WasTruncated = true,
            OriginalSize = EstimateSize(output),
            TruncatedSize = EstimateSize(truncatedContent),
            TruncationReason = $"Log output exceeded {maxLines} lines"
        };
    }

    private LimitedOutput LimitGenericText(object output, OutputLimitContext context)
    {
        var text = output?.ToString() ?? "";
        var maxChars = context.MaxCharacters ?? _options.MaxOutputCharacters;

        if (text.Length <= maxChars)
        {
            return new LimitedOutput
            {
                Content = output!,
                WasTruncated = false,
                OriginalSize = text.Length,
                TruncatedSize = text.Length
            };
        }

        var truncated = text.Substring(0, maxChars - 20) + "\n... (truncated)";

        return new LimitedOutput
        {
            Content = truncated,
            WasTruncated = true,
            OriginalSize = text.Length,
            TruncatedSize = truncated.Length,
            TruncationReason = $"Output exceeded {maxChars} characters"
        };
    }
}
