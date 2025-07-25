using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Examples;

public static class BasicUsageExamples
{
    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Basic Tool Usage Examples ===\n");

        var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();

        // Example 1: Simple tool execution
        Console.WriteLine("1. Simple Tool Execution - Get current date/time:");
        await ExecuteDateTimeTool(toolExecutor);

        // Example 2: Tool with parameters
        Console.WriteLine("\n2. Tool with Parameters - Encode text to Base64:");
        await ExecuteEncodingTool(toolExecutor);

        // Example 3: Handling tool errors
        Console.WriteLine("\n3. Handling Tool Errors:");
        await DemonstrateErrorHandling(toolExecutor);

        // Example 4: Using execution context
        Console.WriteLine("\n4. Using Execution Context:");
        await DemonstrateExecutionContext(toolExecutor);
    }

    private static async Task ExecuteDateTimeTool(IToolExecutor toolExecutor)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "now",
            ["format"] = "yyyy-MM-dd HH:mm:ss"
        };

        var result = await toolExecutor.ExecuteAsync("datetime_tool", parameters);

        if (result.IsSuccessful)
        {
            Console.WriteLine($"Current time: {result.Data}");
        }
        else
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }
    }

    private static async Task ExecuteEncodingTool(IToolExecutor toolExecutor)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "base64_encode",
            ["input_text"] = "Hello, Andy Tools!"
        };

        var result = await toolExecutor.ExecuteAsync("encoding_tool", parameters);

        if (result.IsSuccessful)
        {
            Console.WriteLine($"Original: Hello, Andy Tools!");
            Console.WriteLine($"Encoded: {result.Data}");
            
            // Decode it back
            var decodeParams = new Dictionary<string, object?>
            {
                ["operation"] = "base64_decode",
                ["input_text"] = result.Data
            };
            
            var decodeResult = await toolExecutor.ExecuteAsync("encoding_tool", decodeParams);
            Console.WriteLine($"Decoded: {decodeResult.Data}");
        }
        else
        {
            Console.WriteLine($"Encoding failed: {result.ErrorMessage}");
        }
    }

    private static async Task DemonstrateErrorHandling(IToolExecutor toolExecutor)
    {
        // Try to read a non-existent file
        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = "/this/file/does/not/exist.txt"
        };

        var result = await toolExecutor.ExecuteAsync("read_file", parameters);

        if (!result.IsSuccessful)
        {
            Console.WriteLine($"Expected error occurred: {result.ErrorMessage}");
        }
    }

    private static async Task DemonstrateExecutionContext(IToolExecutor toolExecutor)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "andy-tools-examples");
        Directory.CreateDirectory(tempDir);

        var context = new ToolExecutionContext
        {
            WorkingDirectory = tempDir,
            UserId = "example-user",
            SessionId = Guid.NewGuid().ToString(),
            Permissions = new ToolPermissions
            {
                FileSystemAccess = true,
                NetworkAccess = false,
                ProcessExecution = false
            },
            ResourceLimits = new ToolResourceLimits
            {
                MaxExecutionTimeMs = 10000,
                MaxFileSizeBytes = 1024 * 1024
            }
        };

        // Create a test file
        var testFile = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "This is a test file created with execution context.");

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = "test.txt" // Relative to working directory
        };

        var result = await toolExecutor.ExecuteAsync("read_file", parameters, context);

        if (result.IsSuccessful)
        {
            Console.WriteLine("File read successfully using relative path:");
            Console.WriteLine($"Content: {result.Data}");
            Console.WriteLine($"Execution time: {result.DurationMs:F2}ms");
        }

        // Cleanup
        try { File.Delete(testFile); } catch { }
        try { Directory.Delete(tempDir); } catch { }
    }
}