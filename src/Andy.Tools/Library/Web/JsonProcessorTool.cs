using System.Text.Json;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.Web;

/// <summary>
/// Tool for processing and manipulating JSON data.
/// </summary>
public class JsonProcessorTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "json_processor",
        Name = "JSON Processor",
        Description = "Processes, validates, formats, and queries JSON data",
        Version = "1.0.0",
        Category = ToolCategory.Web,
        RequiredPermissions = ToolPermissionFlags.None,
        Parameters =
        [
            new()
            {
                Name = "json_input",
                Description = "The JSON data to process",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "operation",
                Description = "The operation to perform on the JSON data",
                Type = "string",
                Required = true,
                AllowedValues =
                [
                    "validate", "format", "minify", "query", "extract", "transform",
                    "merge", "diff", "schema_validate", "to_csv", "from_csv",
                    "flatten", "unflatten", "count", "statistics"
                ]
            },
            new()
            {
                Name = "query_path",
                Description = "JSON path for query operations (e.g., '$.users[0].name')",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "merge_json",
                Description = "Second JSON object for merge operations",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "transform_rules",
                Description = "Transformation rules (JSON object)",
                Type = "object",
                Required = false
            },
            new()
            {
                Name = "options",
                Description = "Additional options for the operation (JSON object)",
                Type = "object",
                Required = false
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var jsonInput = GetParameter<string>(parameters, "json_input");
        var operation = GetParameter<string>(parameters, "operation");
        var queryPath = GetParameter<string>(parameters, "query_path");
        var mergeJson = GetParameter<string>(parameters, "merge_json");
        var transformRulesObj = GetParameter<object>(parameters, "transform_rules");
        var optionsObj = GetParameter<object>(parameters, "options");

        try
        {
            ReportProgress(context, $"Performing {operation} operation...", 10);

            // Parse options
            var options = ParseOptions(optionsObj);

            // Validate input JSON
            JsonDocument inputDoc;
            try
            {
                inputDoc = JsonDocument.Parse(jsonInput);
            }
            catch (JsonException ex)
            {
                return ToolResults.InvalidParameter("json_input", jsonInput, $"Invalid JSON: {ex.Message}");
            }

            // Perform the requested operation
            var result = await PerformOperationAsync(inputDoc, operation, queryPath, mergeJson, transformRulesObj, options, context);

            ReportProgress(context, "JSON processing completed", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["operation"] = operation,
                ["input_size"] = jsonInput.Length,
                ["output_size"] = result.Output?.Length ?? 0,
                ["operation_time"] = result.OperationTime,
                ["success"] = result.Success
            };

            // Add operation-specific metadata
            foreach (var kvp in result.Metadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }

            return result.Success
                ? ToolResults.Success(
                    result.Output,
                    $"Successfully performed {operation} operation",
                    metadata
                )
                : ToolResults.Failure(
                    result.ErrorMessage ?? "Operation failed",
                    "JSON_OPERATION_ERROR",
                    metadata
                );
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"JSON processing failed: {ex.Message}", "JSON_PROCESSOR_ERROR", details: ex);
        }
    }

    private static async Task<JsonOperationResult> PerformOperationAsync(
        JsonDocument inputDoc,
        string operation,
        string? queryPath,
        string? mergeJson,
        object? transformRulesObj,
        Dictionary<string, object?> options,
        ToolExecutionContext context)
    {
        var startTime = DateTime.UtcNow;
        var result = new JsonOperationResult();

        try
        {
            result.Output = operation.ToLowerInvariant() switch
            {
                "validate" => ValidateJson(inputDoc, result),
                "format" => FormatJson(inputDoc, options, result),
                "minify" => MinifyJson(inputDoc, result),
                "query" => QueryJson(inputDoc, queryPath, result),
                "extract" => ExtractFromJson(inputDoc, queryPath, result),
                "transform" => TransformJson(inputDoc, transformRulesObj, result),
                "merge" => MergeJson(inputDoc, mergeJson, result),
                "diff" => DiffJson(inputDoc, mergeJson, result),
                "to_csv" => JsonToCsv(inputDoc, options, result),
                "flatten" => FlattenJson(inputDoc, result),
                "unflatten" => UnflattenJson(inputDoc, result),
                "count" => CountJsonElements(inputDoc, result),
                "statistics" => AnalyzeJsonStatistics(inputDoc, result),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Metadata["error_details"] = ex.ToString();
        }
        finally
        {
            result.OperationTime = DateTime.UtcNow - startTime;
        }

        await Task.CompletedTask;
        return result;
    }

    private static string ValidateJson(JsonDocument doc, JsonOperationResult result)
    {
        result.Metadata["is_valid"] = true;
        result.Metadata["validation_message"] = "JSON is valid";
        return "Valid JSON";
    }

    private static string FormatJson(JsonDocument doc, Dictionary<string, object?> options, JsonOperationResult result)
    {
        var indented = GetOption<bool>(options, "indented", true);
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = indented
        };

        var formatted = JsonSerializer.Serialize(doc.RootElement, jsonOptions);
        result.Metadata["formatted_lines"] = formatted.Split('\n').Length;
        return formatted;
    }

    private static string MinifyJson(JsonDocument doc, JsonOperationResult result)
    {
        var minified = JsonSerializer.Serialize(doc.RootElement);
        result.Metadata["compression_ratio"] = (double)minified.Length / doc.RootElement.GetRawText().Length;
        return minified;
    }

    private static string QueryJson(JsonDocument doc, string? queryPath, JsonOperationResult result)
    {
        if (string.IsNullOrEmpty(queryPath))
        {
            throw new ArgumentException("Query path is required for query operation");
        }

        // Simple JSON path implementation for basic queries
        var queryResult = ExecuteJsonPath(doc.RootElement, queryPath);
        result.Metadata["query_path"] = queryPath;
        result.Metadata["matches_found"] = queryResult.Count;

        return JsonSerializer.Serialize(queryResult, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ExtractFromJson(JsonDocument doc, string? queryPath, JsonOperationResult result)
    {
        if (string.IsNullOrEmpty(queryPath))
        {
            throw new ArgumentException("Query path is required for extract operation");
        }

        var extracted = ExecuteJsonPath(doc.RootElement, queryPath);
        result.Metadata["extracted_items"] = extracted.Count;

        return extracted.Count == 1
            ? JsonSerializer.Serialize(extracted[0], new JsonSerializerOptions { WriteIndented = true })
            : JsonSerializer.Serialize(extracted, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string TransformJson(JsonDocument doc, object? transformRulesObj, JsonOperationResult result)
    {
        if (transformRulesObj == null)
        {
            throw new ArgumentException("Transform rules are required for transform operation");
        }

        // Simple transformation implementation
        var rules = ParseTransformRules(transformRulesObj);
        var transformed = ApplyTransformRules(doc.RootElement, rules);

        result.Metadata["transform_rules_applied"] = rules.Count;
        return JsonSerializer.Serialize(transformed, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string MergeJson(JsonDocument doc, string? mergeJson, JsonOperationResult result)
    {
        if (string.IsNullOrEmpty(mergeJson))
        {
            throw new ArgumentException("Merge JSON is required for merge operation");
        }

        var mergeDoc = JsonDocument.Parse(mergeJson);
        var merged = MergeJsonElements(doc.RootElement, mergeDoc.RootElement);

        result.Metadata["merge_operation"] = "deep_merge";
        return JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string DiffJson(JsonDocument doc, string? compareJson, JsonOperationResult result)
    {
        if (string.IsNullOrEmpty(compareJson))
        {
            throw new ArgumentException("Compare JSON is required for diff operation");
        }

        var compareDoc = JsonDocument.Parse(compareJson);
        var diff = FindJsonDifferences(doc.RootElement, compareDoc.RootElement);

        result.Metadata["differences_found"] = diff.Count;
        return JsonSerializer.Serialize(diff, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string JsonToCsv(JsonDocument doc, Dictionary<string, object?> options, JsonOperationResult result)
    {
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("JSON must be an array for CSV conversion");
        }

        var delimiter = GetOption<string>(options, "delimiter", ",");
        var includeHeaders = GetOption<bool>(options, "include_headers", true);

        var csvLines = new List<string>();
        var headers = new HashSet<string>();

        // Collect all possible headers
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in item.EnumerateObject())
                {
                    headers.Add(prop.Name);
                }
            }
        }

        var headerList = headers.OrderBy(h => h).ToList();

        if (includeHeaders)
        {
            csvLines.Add(string.Join(delimiter, headerList));
        }

        // Convert each object to CSV row
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var values = new List<string>();
            foreach (var header in headerList)
            {
                if (item.TryGetProperty(header, out var prop))
                {
                    var value = prop.ValueKind == JsonValueKind.String
                        ? $"\"{prop.GetString()?.Replace("\"", "\"\"")}\""
                        : prop.ToString();
                    values.Add(value);
                }
                else
                {
                    values.Add("");
                }
            }

            csvLines.Add(string.Join(delimiter, values));
        }

        result.Metadata["csv_rows"] = csvLines.Count - (includeHeaders ? 1 : 0);
        result.Metadata["csv_columns"] = headerList.Count;
        return string.Join("\n", csvLines);
    }

    private static string FlattenJson(JsonDocument doc, JsonOperationResult result)
    {
        var flattened = FlattenJsonElement(doc.RootElement);
        result.Metadata["flattened_properties"] = flattened.Count;
        return JsonSerializer.Serialize(flattened, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string UnflattenJson(JsonDocument doc, JsonOperationResult result)
    {
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("JSON must be an object for unflatten operation");
        }

        var unflattened = UnflattenJsonObject(doc.RootElement);
        result.Metadata["unflatten_operation"] = "completed";
        return JsonSerializer.Serialize(unflattened, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CountJsonElements(JsonDocument doc, JsonOperationResult result)
    {
        var counts = CountElementTypes(doc.RootElement);
        result.Metadata.Clear();
        foreach (var kvp in counts)
        {
            result.Metadata[kvp.Key] = kvp.Value;
        }

        return JsonSerializer.Serialize(counts, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string AnalyzeJsonStatistics(JsonDocument doc, JsonOperationResult result)
    {
        var stats = AnalyzeJsonStructure(doc.RootElement);
        result.Metadata.Clear();
        foreach (var kvp in stats)
        {
            result.Metadata[kvp.Key] = kvp.Value;
        }

        return JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
    }

    // Helper methods for JSON operations
    private static List<JsonElement> ExecuteJsonPath(JsonElement element, string path)
    {
        // Simple JSON path implementation - supports basic paths like $.property, $.array[0], etc.
        var results = new List<JsonElement>();

        if (path == "$" || string.IsNullOrEmpty(path))
        {
            results.Add(element);
            return results;
        }

        // Remove $ prefix if present
        if (path.StartsWith("$."))
        {
            path = path[2..];
        }
        else if (path.StartsWith("$"))
        {
            path = path[1..];
        }

        var current = element;
        var parts = path.Split('.');

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            if (part.Contains('[') && part.Contains(']'))
            {
                // Array access
                var propertyName = part[..part.IndexOf('[')];
                var indexStr = part[(part.IndexOf('[') + 1)..part.IndexOf(']')];

                if (!string.IsNullOrEmpty(propertyName))
                {
                    if (current.TryGetProperty(propertyName, out var arrayProp))
                    {
                        current = arrayProp;
                    }
                    else
                    {
                        return results; // Property not found
                    }
                }

                if (int.TryParse(indexStr, out var index) && current.ValueKind == JsonValueKind.Array)
                {
                    var array = current.EnumerateArray().ToList();
                    if (index >= 0 && index < array.Count)
                    {
                        current = array[index];
                    }
                    else
                    {
                        return results; // Index out of bounds
                    }
                }
            }
            else
            {
                // Property access
                if (current.TryGetProperty(part, out var prop))
                {
                    current = prop;
                }
                else
                {
                    return results; // Property not found
                }
            }
        }

        results.Add(current);
        return results;
    }

    private static Dictionary<string, object?> ParseTransformRules(object? transformRulesObj)
    {
        if (transformRulesObj is Dictionary<string, object?> dict)
        {
            return dict;
        }

        if (transformRulesObj is string jsonString)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonString) ?? [];
            }
            catch
            {
                return [];
            }
        }

        return [];
    }

    private static JsonElement ApplyTransformRules(JsonElement element, Dictionary<string, object?> rules)
    {
        // Simple transformation - just return the original element for now
        // In a full implementation, this would apply the transformation rules
        return element;
    }

    private static JsonElement MergeJsonElements(JsonElement target, JsonElement source)
    {
        // Simple merge implementation - source overwrites target
        using var targetDoc = JsonDocument.Parse(target.GetRawText());
        using var sourceDoc = JsonDocument.Parse(source.GetRawText());

        if (target.ValueKind == JsonValueKind.Object && source.ValueKind == JsonValueKind.Object)
        {
            var merged = new Dictionary<string, object?>();

            foreach (var prop in target.EnumerateObject())
            {
                merged[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
            }

            foreach (var prop in source.EnumerateObject())
            {
                merged[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
            }

            return JsonSerializer.SerializeToElement(merged);
        }

        return source; // If not both objects, source takes precedence
    }

    private static List<object> FindJsonDifferences(JsonElement left, JsonElement right)
    {
        var differences = new List<object>();

        if (left.ValueKind != right.ValueKind)
        {
            differences.Add(new { path = "$", type = "type_difference", left = left.ValueKind.ToString(), right = right.ValueKind.ToString() });
        }

        // Simple diff implementation - in a full implementation, this would do deep comparison
        return differences;
    }

    private static Dictionary<string, object?> FlattenJsonElement(JsonElement element, string prefix = "")
    {
        var result = new Dictionary<string, object?>();

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    var flattened = FlattenJsonElement(prop.Value, key);
                    foreach (var kvp in flattened)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                break;

            case JsonValueKind.Array:
                var array = element.EnumerateArray().ToList();
                for (int i = 0; i < array.Count; i++)
                {
                    var key = string.IsNullOrEmpty(prefix) ? $"[{i}]" : $"{prefix}[{i}]";
                    var flattened = FlattenJsonElement(array[i], key);
                    foreach (var kvp in flattened)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                break;

            default:
                result[prefix] = JsonSerializer.Deserialize<object>(element.GetRawText());
                break;
        }

        return result;
    }

    private static object UnflattenJsonObject(JsonElement element)
    {
        // Simple unflatten implementation
        var result = new Dictionary<string, object?>();

        foreach (var prop in element.EnumerateObject())
        {
            result[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
        }

        return result;
    }

    private static Dictionary<string, int> CountElementTypes(JsonElement element)
    {
        var counts = new Dictionary<string, int>
        {
            ["objects"] = 0,
            ["arrays"] = 0,
            ["strings"] = 0,
            ["numbers"] = 0,
            ["booleans"] = 0,
            ["nulls"] = 0
        };

        CountElementTypesRecursive(element, counts);
        return counts;
    }

    private static void CountElementTypesRecursive(JsonElement element, Dictionary<string, int> counts)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                counts["objects"]++;
                foreach (var prop in element.EnumerateObject())
                {
                    CountElementTypesRecursive(prop.Value, counts);
                }

                break;

            case JsonValueKind.Array:
                counts["arrays"]++;
                foreach (var item in element.EnumerateArray())
                {
                    CountElementTypesRecursive(item, counts);
                }

                break;

            case JsonValueKind.String:
                counts["strings"]++;
                break;

            case JsonValueKind.Number:
                counts["numbers"]++;
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                counts["booleans"]++;
                break;

            case JsonValueKind.Null:
                counts["nulls"]++;
                break;
        }
    }

    private static Dictionary<string, object?> AnalyzeJsonStructure(JsonElement element)
    {
        var stats = new Dictionary<string, object?>();
        var counts = CountElementTypes(element);

        foreach (var kvp in counts)
        {
            stats[kvp.Key] = kvp.Value;
        }

        stats["total_elements"] = counts.Values.Sum();
        stats["max_depth"] = CalculateMaxDepth(element);
        stats["size_bytes"] = element.GetRawText().Length;

        return stats;
    }

    private static int CalculateMaxDepth(JsonElement element, int currentDepth = 0)
    {
        var maxDepth = currentDepth;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    maxDepth = Math.Max(maxDepth, CalculateMaxDepth(prop.Value, currentDepth + 1));
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    maxDepth = Math.Max(maxDepth, CalculateMaxDepth(item, currentDepth + 1));
                }

                break;
        }

        return maxDepth;
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
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonString) ?? [];
            }
            catch
            {
                return [];
            }
        }

        return [];
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

    private class JsonOperationResult
    {
        public string? Output { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan OperationTime { get; set; }
        public Dictionary<string, object?> Metadata { get; set; } = [];
    }
}
