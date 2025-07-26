using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Examples;

public static class TestBasic
{
    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Test Basic Execution ===\n");
        
        // Test 1: Simple computation
        Console.WriteLine("1. Simple Math Test:");
        var result = 2 + 2;
        Console.WriteLine($"   2 + 2 = {result}");
        
        // Test 2: Custom tool
        Console.WriteLine("\n2. Testing Custom RandomNumberTool:");
        try
        {
            var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();
            var parameters = new Dictionary<string, object?>
            {
                ["min"] = 1,
                ["max"] = 100
            };
            
            var toolResult = await toolExecutor.ExecuteAsync("random_number", parameters);
            if (toolResult.IsSuccessful)
            {
                Console.WriteLine($"   Random number generated: {toolResult.Data}");
            }
            else
            {
                Console.WriteLine($"   Tool execution failed: {toolResult.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   Exception: {ex.Message}");
        }
        
        // Test 3: File operations without tools
        Console.WriteLine("\n3. Direct File Test:");
        var tempFile = Path.Combine(Path.GetTempPath(), "andy-test.txt");
        try
        {
            await File.WriteAllTextAsync(tempFile, "Hello from Andy Tools!");
            Console.WriteLine($"   Created file: {tempFile}");
            
            var content = await File.ReadAllTextAsync(tempFile);
            Console.WriteLine($"   Read content: {content}");
            
            File.Delete(tempFile);
            Console.WriteLine("   File deleted successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   File operation failed: {ex.Message}");
        }
        
        Console.WriteLine("\n✅ Test completed!");
    }
}