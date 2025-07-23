using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Andy.Tools.Examples;

public static class TextProcessingExamples
{
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
            ["text"] = uglyJson,
            ["format"] = "json",
            ["indent_size"] = 2
        };

        var result = await toolExecutor.ExecuteAsync("format_text", formatParams);
        
        if (result.IsSuccessful)
        {
            Console.WriteLine("Formatted JSON:");
            Console.WriteLine(result.Data);
        }
    }

    private static async Task SearchAndReplace(IToolExecutor toolExecutor)
    {
        var sampleText = """
            The quick brown fox jumps over the lazy dog.
            The lazy dog was sleeping under the tree.
            The quick brown fox ran away quickly.
            """;

        // Simple replacement
        var simpleParams = new Dictionary<string, object?>
        {
            ["text"] = sampleText,
            ["search_pattern"] = "quick",
            ["replacement"] = "swift",
            ["case_sensitive"] = true
        };

        var simpleResult = await toolExecutor.ExecuteAsync("replace_text", simpleParams);
        
        if (simpleResult.IsSuccessful && simpleResult.Data is Dictionary<string, object?> simpleData)
        {
            Console.WriteLine("Simple replacement:");
            Console.WriteLine($"Replacements made: {simpleData.GetValueOrDefault("replacement_count")}");
            Console.WriteLine("Result preview:");
            var resultText = simpleData.GetValueOrDefault("result")?.ToString() ?? "";
            Console.WriteLine(resultText.Split('\n')[0]); // First line
        }

        // Regex replacement
        var regexParams = new Dictionary<string, object?>
        {
            ["text"] = sampleText,
            ["search_pattern"] = @"\b(\w+)\s+\1\b", // Find repeated words
            ["replacement"] = "$1", // Keep only one instance
            ["use_regex"] = true
        };

        var regexResult = await toolExecutor.ExecuteAsync("replace_text", regexParams);
        
        if (regexResult.IsSuccessful && regexResult.Data is Dictionary<string, object?> regexData)
        {
            Console.WriteLine("\nRegex replacement (remove repeated words):");
            Console.WriteLine($"Replacements made: {regexData.GetValueOrDefault("replacement_count")}");
        }
    }

    private static async Task SearchWithRegex(IToolExecutor toolExecutor)
    {
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

        // Search for method definitions
        var searchParams = new Dictionary<string, object?>
        {
            ["text"] = codeSnippet,
            ["patterns"] = new[] { @"public\s+async\s+Task<.*?>\s+(\w+)" },
            ["use_regex"] = true,
            ["include_line_numbers"] = true,
            ["context_lines"] = 1
        };

        var result = await toolExecutor.ExecuteAsync("search_text", searchParams);
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> searchData)
        {
            if (searchData.TryGetValue("results", out var results) && results is List<object> resultList)
            {
                Console.WriteLine($"Found {resultList.Count} async methods:");
                foreach (var item in resultList)
                {
                    if (item is Dictionary<string, object?> match)
                    {
                        Console.WriteLine($"- Line {match.GetValueOrDefault("line_number")}: {match.GetValueOrDefault("matched_text")}");
                    }
                }
            }
        }
    }

    private static async Task FormatDifferentTypes(IToolExecutor toolExecutor)
    {
        // Format XML
        var uglyXml = """<root><person><name>John</name><age>30</age></person><person><name>Jane</name><age>25</age></person></root>""";
        
        var xmlParams = new Dictionary<string, object?>
        {
            ["text"] = uglyXml,
            ["format"] = "xml",
            ["indent_size"] = 4
        };

        var xmlResult = await toolExecutor.ExecuteAsync("format_text", xmlParams);
        
        if (xmlResult.IsSuccessful)
        {
            Console.WriteLine("Formatted XML:");
            Console.WriteLine(xmlResult.Data);
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
            ["text"] = JsonSerializer.Serialize(customJson),
            ["format"] = "json",
            ["options"] = new Dictionary<string, object?>
            {
                ["sort_keys"] = true,
                ["ensure_ascii"] = false,
                ["indent"] = 2
            }
        };

        var customResult = await toolExecutor.ExecuteAsync("format_text", customParams);
        
        if (customResult.IsSuccessful)
        {
            Console.WriteLine("\nFormatted JSON with custom options:");
            Console.WriteLine(customResult.Data);
        }
    }
}