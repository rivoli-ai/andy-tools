using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Andy.Tools.Examples;

public static class TextProcessingExamples
{
    private static string ExtractFormattedText(object? data)
    {
        if (data == null) return "null";
        
        // If it's a dictionary, extract the content field
        if (data is Dictionary<string, object?> dict)
        {
            string? text = null;
            
            if (dict.TryGetValue("content", out var content))
                text = content?.ToString();
            else if (dict.TryGetValue("formatted_text", out var formatted))
                text = formatted?.ToString();
            else if (dict.TryGetValue("result", out var result))
                text = result?.ToString();
            else if (dict.TryGetValue("output", out var output))
                text = output?.ToString();
            
            if (text != null)
            {
                // Unescape JSON unicode sequences
                return System.Text.RegularExpressions.Regex.Unescape(text);
            }
            
            // Debug: show what keys are available
            return $"Dictionary with keys: {string.Join(", ", dict.Keys)}";
        }
        
        // Otherwise just convert to string
        return data.ToString() ?? "null";
    }
    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Text Processing Examples ===\n");

        var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();

        // Example 1: Format JSON
        Console.WriteLine("1. Format JSON:");
        await FormatJson(toolExecutor);

        // Example 2: Search and replace text
        Console.WriteLine("\n2. Search and Replace Text:");
        await SearchAndReplace(toolExecutor);

        // Example 3: Search text with regex
        Console.WriteLine("\n3. Search Text with Regex:");
        await SearchWithRegex(toolExecutor);

        // Example 4: Format different types
        Console.WriteLine("\n4. Format Different Text Types:");
        await FormatDifferentTypes(toolExecutor);
    }

    private static async Task FormatJson(IToolExecutor toolExecutor)
    {
        var uglyJson = """{"name":"John Doe","age":30,"address":{"street":"123 Main St","city":"New York"},"hobbies":["reading","gaming","coding"]}""";

        var formatParams = new Dictionary<string, object?>
        {
            ["input_text"] = uglyJson,
            ["operation"] = "format_json"
        };

        var result = await toolExecutor.ExecuteAsync("format_text", formatParams);
        
        if (result.IsSuccessful)
        {
            Console.WriteLine("Formatted JSON:");
            Console.WriteLine(ExtractFormattedText(result.Data));
        }
        else
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }
    }

    private static async Task SearchAndReplace(IToolExecutor toolExecutor)
    {
        // First, create a sample file
        var tempFile = Path.Combine(Path.GetTempPath(), "sample_text.txt");
        var sampleText = """
            The quick brown fox jumps over the lazy dog.
            The lazy dog was sleeping under the tree.
            The quick brown fox ran away quickly.
            """;
        
        await File.WriteAllTextAsync(tempFile, sampleText);

        // Simple replacement in file
        var simpleParams = new Dictionary<string, object?>
        {
            ["search_pattern"] = "quick",
            ["replacement_text"] = "swift",
            ["target_path"] = tempFile,
            ["search_type"] = "contains"
        };

        var simpleResult = await toolExecutor.ExecuteAsync("replace_text", simpleParams);
        
        if (simpleResult.IsSuccessful)
        {
            Console.WriteLine("Simple replacement completed");
            if (simpleResult.Data is Dictionary<string, object?> resultData)
            {
                var filesProcessed = resultData.GetValueOrDefault("count");
                var totalReplacements = resultData.GetValueOrDefault("total_count");
                
                Console.WriteLine($"Files processed: {filesProcessed}");
                Console.WriteLine($"Total replacements: {totalReplacements}");
                
                // Check if there are detailed results
                if (resultData.TryGetValue("items", out var items) && items is List<object> itemList && itemList.Count > 0)
                {
                    if (itemList[0] is Dictionary<string, object?> firstItem)
                    {
                        var replacements = firstItem.GetValueOrDefault("replacement_count") ?? firstItem.GetValueOrDefault("replacements");
                        if (replacements != null)
                        {
                            Console.WriteLine($"Replacements in this file: {replacements}");
                        }
                    }
                }
            }
            
            // Read the modified content
            var modifiedContent = await File.ReadAllTextAsync(tempFile);
            Console.WriteLine("Result preview:");
            Console.WriteLine(modifiedContent.Split('\n')[0]); // First line
        }
        else
        {
            Console.WriteLine($"Replace text failed: {simpleResult.ErrorMessage}");
        }

        // Clean up
        try { File.Delete(tempFile); } catch { }
    }

    private static async Task SearchWithRegex(IToolExecutor toolExecutor)
    {
        // Create a sample code file
        var tempFile = Path.Combine(Path.GetTempPath(), "sample_code.cs");
        var codeSnippet = """
            public class UserService
            {
                private readonly ILogger<UserService> _logger;
                private readonly IUserRepository _repository;
                
                public async Task<User> GetUserAsync(int id)
                {
                    _logger.LogInformation("Getting user {UserId}", id);
                    return await _repository.GetByIdAsync(id);
                }
                
                public async Task<List<User>> GetAllUsersAsync()
                {
                    _logger.LogInformation("Getting all users");
                    return await _repository.GetAllAsync();
                }
            }
            """;
        
        await File.WriteAllTextAsync(tempFile, codeSnippet);

        // Search for async method definitions
        var searchParams = new Dictionary<string, object?>
        {
            ["search_pattern"] = @"public\s+async\s+Task<.*?>\s+(\w+)",
            ["target_path"] = tempFile,
            ["search_type"] = "regex",
            ["include_line_numbers"] = true,
            ["context_lines"] = 1
        };

        var result = await toolExecutor.ExecuteAsync("search_text", searchParams);
        
        if (result.IsSuccessful)
        {
            Console.WriteLine("Search results:");
            if (result.Data is Dictionary<string, object?> searchData)
            {
                Console.WriteLine($"Total files: {searchData.GetValueOrDefault("count")}");
                Console.WriteLine($"Total matches: {searchData.GetValueOrDefault("total_count")}");
                
                if (searchData.TryGetValue("items", out var results) && results is List<object> resultList)
                {
                    Console.WriteLine($"\nFound {resultList.Count} matches:");
                    foreach (var item in resultList)
                    {
                        if (item is Dictionary<string, object?> match)
                        {
                            var lineNumber = match.GetValueOrDefault("line_number");
                            var lineContent = match.GetValueOrDefault("line_content") ?? match.GetValueOrDefault("line");
                            var matchText = match.GetValueOrDefault("match") ?? match.GetValueOrDefault("text");
                            
                            if (lineContent != null)
                            {
                                Console.WriteLine($"- Line {lineNumber}: {lineContent}");
                            }
                            else if (matchText != null)
                            {
                                Console.WriteLine($"- Match: {matchText}");
                            }
                            else
                            {
                                // Debug: show available keys
                                Console.WriteLine($"  Match keys: {string.Join(", ", match.Keys)}");
                            }
                        }
                    }
                }
                else
                {
                    // Debug: show what keys are available
                    Console.WriteLine($"Available keys in searchData: {string.Join(", ", searchData.Keys)}");
                }
            }
        }
        else
        {
            Console.WriteLine($"Search failed: {result.ErrorMessage}");
        }
        
        // Clean up
        try { File.Delete(tempFile); } catch { }
    }

    private static async Task FormatDifferentTypes(IToolExecutor toolExecutor)
    {
        // Format XML
        var uglyXml = """<root><person><name>John</name><age>30</age></person><person><name>Jane</name><age>25</age></person></root>""";
        
        var xmlParams = new Dictionary<string, object?>
        {
            ["input_text"] = uglyXml,
            ["operation"] = "format_xml"
        };

        var xmlResult = await toolExecutor.ExecuteAsync("format_text", xmlParams);
        
        if (xmlResult.IsSuccessful)
        {
            Console.WriteLine("Formatted XML:");
            Console.WriteLine(ExtractFormattedText(xmlResult.Data));
        }

        // Format with custom options
        var customJson = new
        {
            users = new[] 
            {
                new { id = 1, name = "Alice", active = true },
                new { id = 2, name = "Bob", active = false }
            },
            timestamp = DateTime.Now
        };

        var customParams = new Dictionary<string, object?>
        {
            ["input_text"] = JsonSerializer.Serialize(customJson),
            ["operation"] = "format_json"
        };

        var customResult = await toolExecutor.ExecuteAsync("format_text", customParams);
        
        if (customResult.IsSuccessful)
        {
            Console.WriteLine("\nFormatted JSON with custom options:");
            Console.WriteLine(ExtractFormattedText(customResult.Data));
        }
    }
}