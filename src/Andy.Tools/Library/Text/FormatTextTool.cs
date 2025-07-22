using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.Text;

/// <summary>
/// Tool for formatting and transforming text content.
/// </summary>
public partial class FormatTextTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "format_text",
        Name = "Format Text",
        Description = "Formats and transforms text content using various formatting operations",
        Version = "1.0.0",
        Category = ToolCategory.TextProcessing,
        RequiredPermissions = ToolPermissionFlags.None,
        Parameters =
        [
            new()
            {
                Name = "input_text",
                Description = "The text content to format",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "operation",
                Description = "The formatting operation to perform",
                Type = "string",
                Required = true,
                AllowedValues =
                [
                    "trim", "upper", "lower", "title", "camel", "pascal", "snake", "kebab",
                    "reverse", "sort_lines", "remove_duplicates", "remove_empty_lines",
                    "normalize_whitespace", "word_wrap", "indent", "unindent",
                    "encode_base64", "decode_base64", "encode_url", "decode_url",
                    "format_json", "minify_json", "format_xml", "extract_numbers",
                    "extract_emails", "extract_urls", "count_words", "count_chars"
                ]
            },
            new()
            {
                Name = "options",
                Description = "Additional options for the formatting operation (JSON object or string)",
                Type = "object",
                Required = false
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var inputText = GetParameter<string>(parameters, "input_text");
        var operation = GetParameter<string>(parameters, "operation");
        var optionsObj = GetParameter<object>(parameters, "options");

        try
        {
            ReportProgress(context, $"Performing {operation} operation...", 20);

            // Parse options
            var options = ParseOptions(optionsObj);

            // Perform the formatting operation
            var result = await PerformOperationAsync(inputText, operation, options, context);

            ReportProgress(context, "Formatting completed", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["operation"] = operation,
                ["input_length"] = inputText.Length,
                ["output_length"] = result.FormattedText?.Length ?? 0,
                ["operation_time"] = result.OperationTime,
                ["options_used"] = options
            };

            // Add operation-specific metadata
            foreach (var kvp in result.Metadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }

            return ToolResults.TextSuccess(
                result.FormattedText ?? "",
                "text/plain",
                $"Successfully performed {operation} operation",
                metadata
            );
        }
        catch (ArgumentException ex) when (ex.Message.StartsWith("Unknown operation"))
        {
            return ToolResults.Failure(ex.Message, "UNKNOWN_OPERATION");
        }
        catch (ArgumentException ex)
        {
            return ToolResults.InvalidParameter("operation", operation, ex.Message);
        }
        catch (JsonException ex)
        {
            return ToolResults.InvalidParameter("input_text", inputText, $"JSON parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Text formatting failed: {ex.Message}", "FORMAT_ERROR", details: ex);
        }
    }

    private static Dictionary<string, object?> ParseOptions(object? optionsObj)
    {
        if (optionsObj == null)
        {
            return [];
        }

        if (optionsObj is Dictionary<string, object?> dict)
        {
            return dict;
        }

        if (optionsObj is string jsonString)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonString);
                var result = new Dictionary<string, object?>();
                
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    result[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number => property.Value.TryGetInt32(out var intValue) ? intValue : property.Value.GetDouble(),
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Null => null,
                        _ => property.Value.GetRawText()
                    };
                }
                
                return result;
            }
            catch
            {
                return [];
            }
        }

        return [];
    }

    private static Task<FormatResult> PerformOperationAsync(string inputText, string operation, Dictionary<string, object?> options, ToolExecutionContext context)
    {
        var result = new FormatResult();
        var startTime = DateTime.UtcNow;

        try
        {
            result.FormattedText = operation.ToLowerInvariant() switch
            {
                "trim" => inputText.Trim(),
                "upper" => inputText.ToUpperInvariant(),
                "lower" => inputText.ToLowerInvariant(),
                "title" => ToTitleCase(inputText),
                "camel" => ToCamelCase(inputText),
                "pascal" => ToPascalCase(inputText),
                "snake" => ToSnakeCase(inputText),
                "kebab" => ToKebabCase(inputText),
                "reverse" => new string([.. inputText.Reverse()]),
                "sort_lines" => SortLines(inputText, options),
                "remove_duplicates" => RemoveDuplicateLines(inputText),
                "remove_empty_lines" => RemoveEmptyLines(inputText),
                "normalize_whitespace" => NormalizeWhitespace(inputText),
                "word_wrap" => WordWrap(inputText, options),
                "indent" => IndentText(inputText, options),
                "unindent" => UnindentText(inputText),
                "encode_base64" => Convert.ToBase64String(Encoding.UTF8.GetBytes(inputText)),
                "decode_base64" => DecodeBase64(inputText),
                "encode_url" => Uri.EscapeDataString(inputText),
                "decode_url" => Uri.UnescapeDataString(inputText),
                "format_json" => FormatJson(inputText, options),
                "minify_json" => MinifyJson(inputText),
                "format_xml" => FormatXml(inputText),
                "extract_numbers" => ExtractNumbers(inputText, result),
                "extract_emails" => ExtractEmails(inputText, result),
                "extract_urls" => ExtractUrls(inputText, result),
                "count_words" => CountWords(inputText, result),
                "count_chars" => CountCharacters(inputText, result),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };

            result.OperationTime = DateTime.UtcNow - startTime;
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            result.OperationTime = DateTime.UtcNow - startTime;
            throw new InvalidOperationException($"Operation '{operation}' failed: {ex.Message}", ex);
        }
    }

    private static string ToTitleCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..].ToLowerInvariant();
            }
        }

        return string.Join(" ", words);
    }

    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var words = input.Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();

        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                if (i == 0)
                {
                    result.Append(char.ToLowerInvariant(words[i][0]) + words[i][1..].ToLowerInvariant());
                }
                else
                {
                    result.Append(char.ToUpperInvariant(words[i][0]) + words[i][1..].ToLowerInvariant());
                }
            }
        }

        return result.ToString();
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var words = input.Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();

        foreach (var word in words)
        {
            if (word.Length > 0)
            {
                result.Append(char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());
            }
        }

        return result.ToString();
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]) && i > 0 && !char.IsWhiteSpace(input[i - 1]))
            {
                result.Append('_');
            }

            if (char.IsWhiteSpace(input[i]) || input[i] == '-')
            {
                result.Append('_');
            }
            else
            {
                result.Append(char.ToLowerInvariant(input[i]));
            }
        }

        return result.ToString().Replace("__", "_").Trim('_');
    }

    private static string ToKebabCase(string input)
    {
        return ToSnakeCase(input).Replace('_', '-');
    }

    private static string SortLines(string input, Dictionary<string, object?> options)
    {
        var lines = input.Split('\n');
        var descending = GetOption<bool>(options, "descending", false);
        var ignoreCase = GetOption<bool>(options, "ignore_case", true);

        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var sortedLines = descending ? lines.OrderByDescending(l => l, comparer) : lines.OrderBy(l => l, comparer);

        return string.Join('\n', sortedLines);
    }

    private static string RemoveDuplicateLines(string input)
    {
        var lines = input.Split('\n');
        var uniqueLines = lines.Distinct().ToArray();
        return string.Join('\n', uniqueLines);
    }

    private static string RemoveEmptyLines(string input)
    {
        var lines = input.Split('\n');
        var nonEmptyLines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        return string.Join('\n', nonEmptyLines);
    }

    private static string NormalizeWhitespace(string input)
    {
        // Replace multiple whitespace characters with single spaces
        var normalized = MyRegex().Replace(input, " ");
        return normalized.Trim();
    }

    private static string WordWrap(string input, Dictionary<string, object?> options)
    {
        var lineWidth = GetOption<int>(options, "width", 80);
        if (lineWidth <= 0)
        {
            lineWidth = 80;
        }

        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();
        var currentLineLength = 0;

        foreach (var word in words)
        {
            if (currentLineLength + word.Length + 1 > lineWidth && currentLineLength > 0)
            {
                result.AppendLine();
                currentLineLength = 0;
            }

            if (currentLineLength > 0)
            {
                result.Append(' ');
                currentLineLength++;
            }

            result.Append(word);
            currentLineLength += word.Length;
        }

        return result.ToString();
    }

    private static string IndentText(string input, Dictionary<string, object?> options)
    {
        var indentSize = GetOption<int>(options, "size", 4);
        var useSpaces = GetOption<bool>(options, "use_spaces", true);

        var indentString = useSpaces ? new string(' ', indentSize) : "\t";
        var lines = input.Split('\n');

        return string.Join('\n', lines.Select(line => string.IsNullOrWhiteSpace(line) ? line : indentString + line));
    }

    private static string UnindentText(string input)
    {
        var lines = input.Split('\n');
        if (lines.Length == 0)
        {
            return input;
        }

        // Find minimum indentation
        var minIndent = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.TakeWhile(c => char.IsWhiteSpace(c)).Count())
            .DefaultIfEmpty(0)
            .Min();

        return minIndent == 0
            ? input
            : string.Join('\n', lines.Select(line =>
            string.IsNullOrWhiteSpace(line) ? line :
            line.Length > minIndent ? line[minIndent..] : ""));
    }

    private static string DecodeBase64(string input)
    {
        try
        {
            var bytes = Convert.FromBase64String(input);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            throw new ArgumentException("Invalid base64 string");
        }
    }

    private static string FormatJson(string input, Dictionary<string, object?> options)
    {
        try
        {
            var indented = GetOption<bool>(options, "indented", true);

            var jsonDoc = JsonDocument.Parse(input);
            var options_json = new JsonSerializerOptions
            {
                WriteIndented = indented
            };

            return JsonSerializer.Serialize(jsonDoc, options_json);
        }
        catch (JsonException ex)
        {
            throw new JsonException($"JSON parsing error: {ex.Message}", ex);
        }
    }

    private static string MinifyJson(string input)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(input);
            return JsonSerializer.Serialize(jsonDoc);
        }
        catch (JsonException ex)
        {
            throw new JsonException($"JSON parsing error: {ex.Message}", ex);
        }
    }

    private static string FormatXml(string input)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(input);

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n"
            };

            using var stringWriter = new StringWriter();
            using var xmlWriter = XmlWriter.Create(stringWriter, settings);
            xmlDoc.WriteContentTo(xmlWriter);
            xmlWriter.Flush();

            return stringWriter.ToString();
        }
        catch
        {
            throw new ArgumentException("Invalid XML content");
        }
    }

    private static string ExtractNumbers(string input, FormatResult result)
    {
        var numbers = Regex.Matches(input, @"-?\d+(?:\.\d+)?")
            .Cast<Match>()
            .Select(m => m.Value)
            .ToList();

        result.Metadata["numbers_found"] = numbers.Count;
        return string.Join("\n", numbers);
    }

    private static string ExtractEmails(string input, FormatResult result)
    {
        var emails = Regex.Matches(input, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b")
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct()
            .ToList();

        result.Metadata["emails_found"] = emails.Count;
        return string.Join("\n", emails);
    }

    private static string ExtractUrls(string input, FormatResult result)
    {
        var urls = Regex.Matches(input, @"https?://[^\s]+")
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct()
            .ToList();

        result.Metadata["urls_found"] = urls.Count;
        return string.Join("\n", urls);
    }

    private static string CountWords(string input, FormatResult result)
    {
        var words = input.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var wordCount = words.Length;
        var uniqueWords = words.Distinct(StringComparer.OrdinalIgnoreCase).Count();

        result.Metadata["word_count"] = wordCount;
        result.Metadata["unique_words"] = uniqueWords;
        result.Metadata["average_word_length"] = words.Length > 0 ? words.Average(w => w.Length) : 0;

        return $"Words: {wordCount}\nUnique words: {uniqueWords}\nAverage word length: {result.Metadata["average_word_length"]:F1}";
    }

    private static string CountCharacters(string input, FormatResult result)
    {
        var totalChars = input.Length;
        var letters = input.Count(char.IsLetter);
        var digits = input.Count(char.IsDigit);
        var whitespace = input.Count(char.IsWhiteSpace);
        var punctuation = input.Count(char.IsPunctuation);
        var lines = input.Split('\n').Length;

        result.Metadata["total_characters"] = totalChars;
        result.Metadata["letters"] = letters;
        result.Metadata["digits"] = digits;
        result.Metadata["whitespace"] = whitespace;
        result.Metadata["punctuation"] = punctuation;
        result.Metadata["lines"] = lines;

        return $"Total characters: {totalChars}\nLetters: {letters}\nDigits: {digits}\nWhitespace: {whitespace}\nPunctuation: {punctuation}\nLines: {lines}";
    }

    private static T GetOption<T>(Dictionary<string, object?> options, string key, T defaultValue)
    {
        if (!options.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        if (value is T directValue)
        {
            return directValue;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    private class FormatResult
    {
        public string? FormattedText { get; set; }
        public TimeSpan OperationTime { get; set; }
        public Dictionary<string, object?> Metadata { get; set; } = [];
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MyRegex();
}
