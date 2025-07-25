using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Andy.Tools.Examples;

public static class WebOperationsExamples
{
    private static string ExtractFormattedText(object? data)
    {
        if (data == null) return "null";
        
        // If it's a dictionary, extract the content field
        if (data is Dictionary<string, object?> dict)
        {
            if (dict.TryGetValue("content", out var content))
                return content?.ToString() ?? "";
            else if (dict.TryGetValue("formatted_text", out var formatted))
                return formatted?.ToString() ?? "";
            else if (dict.TryGetValue("result", out var result))
                return result?.ToString() ?? "";
        }
        
        return data.ToString() ?? "";
    }
    
    private static string ExtractJsonValue(object? data)
    {
        if (data == null) return "null";
        
        // If it's a dictionary, check for value or result
        if (data is Dictionary<string, object?> dict)
        {
            if (dict.TryGetValue("value", out var value))
                return value?.ToString() ?? "";
            else if (dict.TryGetValue("result", out var result))
                return result?.ToString() ?? "";
            else if (dict.TryGetValue("data", out var dataValue))
                return dataValue?.ToString() ?? "";
        }
        
        return data.ToString() ?? "";
    }
    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Web Operations Examples ===\n");
        Console.WriteLine("⚠️  These examples make HTTP requests. Ensure you have internet access.\n");

        var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();

        // Example 1: Simple GET request
        Console.WriteLine("1. Simple GET Request:");
        await SimpleGetRequest(toolExecutor);

        // Example 2: JSON processing
        Console.WriteLine("\n2. JSON Processing:");
        await JsonProcessing(toolExecutor);

        // Example 3: HTTP with headers
        Console.WriteLine("\n3. HTTP with Custom Headers:");
        await HttpWithHeaders(toolExecutor);

        // Example 4: Error handling
        Console.WriteLine("\n4. HTTP Error Handling:");
        await HttpErrorHandling(toolExecutor);
    }

    private static async Task SimpleGetRequest(IToolExecutor toolExecutor)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["url"] = "https://api.github.com/zen",
            ["method"] = "GET",
            ["headers"] = new Dictionary<string, object?>
            {
                ["User-Agent"] = "Andy-Tools-Examples/1.0"
            }
        };

        var result = await toolExecutor.ExecuteAsync("http_request", parameters);
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> response)
        {
            Console.WriteLine($"Status: {response.GetValueOrDefault("status_code")}");
            var content = response.GetValueOrDefault("content") ?? response.GetValueOrDefault("body");
            Console.WriteLine($"GitHub Zen: {content}");
            
            if (response.TryGetValue("headers", out var headers) && headers is Dictionary<string, object?> headerDict)
            {
                Console.WriteLine($"Content-Type: {headerDict.GetValueOrDefault("content-type") ?? headerDict.GetValueOrDefault("Content-Type")}");
            }
        }
        else
        {
            Console.WriteLine($"Request failed: {result.ErrorMessage}");
        }
    }

    private static async Task JsonProcessing(IToolExecutor toolExecutor)
    {
        // First, get some JSON data
        var httpParams = new Dictionary<string, object?>
        {
            ["url"] = "https://api.github.com/users/github",
            ["method"] = "GET",
            ["headers"] = new Dictionary<string, object?>
            {
                ["User-Agent"] = "Andy-Tools-Examples/1.0"
            }
        };

        var httpResult = await toolExecutor.ExecuteAsync("http_request", httpParams);
        
        if (httpResult.IsSuccessful && httpResult.Data is Dictionary<string, object?> response)
        {
            var statusCode = response.GetValueOrDefault("status_code");
            Console.WriteLine($"HTTP Status: {statusCode}");
            
            var jsonBody = (response.GetValueOrDefault("content") ?? response.GetValueOrDefault("body"))?.ToString() ?? "{}";
            
            // Show a preview of the JSON
            if (jsonBody.Length > 100)
            {
                Console.WriteLine($"Received JSON (preview): {jsonBody.Substring(0, 100)}...");
            }
            
            // Process the JSON
            var jsonParams = new Dictionary<string, object?>
            {
                ["json_input"] = jsonBody,
                ["operation"] = "query",
                ["query_path"] = "$.name"
            };

            var jsonResult = await toolExecutor.ExecuteAsync("json_processor", jsonParams);
            
            if (jsonResult.IsSuccessful)
            {
                var extractedName = ExtractJsonValue(jsonResult.Data);
                Console.WriteLine($"GitHub user name: {extractedName}");
            }
            else
            {
                Console.WriteLine($"JSON query failed: {jsonResult.ErrorMessage}");
            }

            // Extract another value
            var extractParams = new Dictionary<string, object?>
            {
                ["json_input"] = jsonBody,
                ["operation"] = "extract",
                ["query_path"] = "$.public_repos"
            };

            var extractResult = await toolExecutor.ExecuteAsync("json_processor", extractParams);
            
            if (extractResult.IsSuccessful)
            {
                var repos = ExtractJsonValue(extractResult.Data);
                Console.WriteLine($"Public repositories: {repos}");
            }
            else
            {
                Console.WriteLine($"Extract failed: {extractResult.ErrorMessage}");
            }
        }
        else
        {
            Console.WriteLine($"HTTP request failed: {httpResult.ErrorMessage}");
        }
    }

    private static async Task HttpWithHeaders(IToolExecutor toolExecutor)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["url"] = "https://httpbin.org/headers",
            ["method"] = "GET",
            ["headers"] = new Dictionary<string, object?>
            {
                ["User-Agent"] = "Andy-Tools-Examples/1.0",
                ["Accept"] = "application/json",
                ["X-Custom-Header"] = "Hello from Andy Tools!"
            }
        };

        var result = await toolExecutor.ExecuteAsync("http_request", parameters);
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> response)
        {
            var body = (response.GetValueOrDefault("content") ?? response.GetValueOrDefault("body"))?.ToString() ?? "";
            
            // Parse and format the response
            var formatParams = new Dictionary<string, object?>
            {
                ["input_text"] = body,
                ["operation"] = "format_json"
            };

            var formatResult = await toolExecutor.ExecuteAsync("format_text", formatParams);
            
            if (formatResult.IsSuccessful)
            {
                Console.WriteLine("Request headers echoed by server:");
                var formattedText = ExtractFormattedText(formatResult.Data);
                Console.WriteLine(formattedText);
            }
        }
    }

    private static async Task HttpErrorHandling(IToolExecutor toolExecutor)
    {
        // Try different error scenarios
        var scenarios = new[]
        {
            new { Url = "https://httpbin.org/status/404", Description = "404 Not Found" },
            new { Url = "https://httpbin.org/status/500", Description = "500 Internal Server Error" },
            new { Url = "https://invalid-domain-that-does-not-exist.com", Description = "Invalid domain" }
        };

        foreach (var scenario in scenarios)
        {
            Console.WriteLine($"\nTesting: {scenario.Description}");
            
            var parameters = new Dictionary<string, object?>
            {
                ["url"] = scenario.Url,
                ["method"] = "GET",
                ["timeout"] = 5000 // 5 seconds timeout
            };

            var result = await toolExecutor.ExecuteAsync("http_request", parameters);
            
            if (result.IsSuccessful && result.Data is Dictionary<string, object?> response)
            {
                var statusCode = response.GetValueOrDefault("status_code");
                Console.WriteLine($"Status code: {statusCode}");
                
                if (statusCode is int code && code >= 400)
                {
                    Console.WriteLine("Request completed but returned error status");
                }
            }
            else
            {
                Console.WriteLine($"Request failed: {result.ErrorMessage}");
                // Error details are in ErrorMessage
            }
        }
    }
}