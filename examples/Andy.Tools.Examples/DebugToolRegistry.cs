using Andy.Tools.Core;
using Andy.Tools.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Examples;

public static class DebugToolRegistry
{
    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Debug Tool Registry ===\n");
        
        try
        {
            // Get the tool registry
            var toolRegistry = serviceProvider.GetService<IToolRegistry>();
            if (toolRegistry == null)
            {
                Console.WriteLine("ERROR: IToolRegistry service not found!");
                return;
            }
            
            Console.WriteLine("Tool Registry found. Listing registered tools:\n");
            
            // Get all registered tools
            var registeredTools = toolRegistry.Tools;
            
            if (!registeredTools.Any())
            {
                Console.WriteLine("WARNING: No tools are registered!");
                
                // Check if specific tools are in DI container
                Console.WriteLine("\nChecking DI container for specific tool types:");
                
                var datetimeTool = serviceProvider.GetService<Andy.Tools.Library.Utilities.DateTimeTool>();
                Console.WriteLine($"DateTimeTool in DI: {datetimeTool != null}");
                
                var encodingTool = serviceProvider.GetService<Andy.Tools.Library.Utilities.EncodingTool>();
                Console.WriteLine($"EncodingTool in DI: {encodingTool != null}");
                
                var readFileTool = serviceProvider.GetService<Andy.Tools.Library.FileSystem.ReadFileTool>();
                Console.WriteLine($"ReadFileTool in DI: {readFileTool != null}");
            }
            else
            {
                Console.WriteLine($"Found {registeredTools.Count} registered tools:");
                foreach (var registration in registeredTools)
                {
                    Console.WriteLine($"  - {registration.Metadata.Id}: {registration.Metadata.Name}");
                }
            }
            
            // Check tool discovery
            var toolDiscovery = serviceProvider.GetService<Andy.Tools.Discovery.IToolDiscovery>();
            if (toolDiscovery != null)
            {
                Console.WriteLine("\n\nTool Discovery service found. Discovering tools...");
                await toolDiscovery.DiscoverToolsAsync();
                
                // Check registry again
                registeredTools = toolRegistry.Tools;
                Console.WriteLine($"\nAfter discovery: {registeredTools.Count} tools registered");
            }
            else
            {
                Console.WriteLine("\n\nWARNING: IToolDiscovery service not found!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}