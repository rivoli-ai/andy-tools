using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Andy.Tools.Examples;

public static class WebOperationsExamples
{
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
            ["method"] = "GET"
        };

        var result = await toolExecutor.ExecuteAsync("http_request", parameters);
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> response)
        {
            Console.WriteLine($"Status: {response.GetValueOrDefault("status_code")}");
            Console.WriteLine($"GitHub Zen: {response.GetValueOrDefault("body")}");
            
            if (response.TryGetValue("headers", out var headers) && headers is Dictionary<string, object?> headerDict)
            {
                Console.WriteLine($"Content-Type: {headerDict.GetValueOrDefault("content-type")}");
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
            ["method"] = "GET"
        };

        var httpResult = await toolExecutor.ExecuteAsync("http_request", httpParams);
        
        if (httpResult.IsSuccessful && httpResult.Data is Dictionary<string, object?> response)
        {
            var jsonBody = response.GetValueOrDefault("body")?.ToString() ?? "{}";
            
            // Process the JSON
            var jsonParams = new Dictionary<string, object?>
            {
                ["json_data"] = jsonBody,
                ["operation"] = "query",
                ["json_path"] = "$.name"
            };

            var jsonResult = await toolExecutor.ExecuteAsync("json_processor", jsonParams);
            
            if (jsonResult.IsSuccessful)
            {
                Console.WriteLine($"GitHub user name: {jsonResult.Data}");
            }

            // Extract multiple values
            var multiParams = new Dictionary<string, object?>
            {
                ["json_data"] = jsonBody,
                ["operation"] = "extract",
                ["paths"] = new[] { "$.login", "$.public_repos", "$.created_at" }
            };

            var multiResult = await toolExecutor.ExecuteAsync("json_processor", multiParams);
            
            if (multiResult.IsSuccessful && multiResult.Data is Dictionary<string, object?> extracted)
            {
                Console.WriteLine("Extracted values:");
                foreach (var kvp in extracted)
                {
                    Console.WriteLine($"- {kvp.Key}: {kvp.Value}");
                }
            }
        }
    }

    private static async Task HttpWithHeaders(IToolExecutor toolExecutor)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["url"] = "https://httpbin.org/headers",
            ["method"] = "GET",
            ["headers"] = new Dictionary<string, string>
            {
                ["User-Agent"] = "Andy-Tools-Examples/1.0",
                ["Accept"] = "application/json",
                ["X-Custom-Header"] = "Hello from Andy Tools!"
            }
        };

        var result = await toolExecutor.ExecuteAsync("http_request", parameters);
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> response)
        {
            var body = response.GetValueOrDefault("body")?.ToString() ?? "";
            
            // Parse and format the response
            var formatParams = new Dictionary<string, object?>
            {
                ["text"] = body,
                ["format"] = "json",
                ["indent_size"] = 2
            };

            var formatResult = await toolExecutor.ExecuteAsync("format_text", formatParams);
            
            if (formatResult.IsSuccessful)
            {
                Console.WriteLine("Request headers echoed by server:");
                Console.WriteLine(formatResult.Data);
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