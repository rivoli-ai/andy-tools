using Andy.Tools;
using Andy.Tools.Advanced.Configuration;
using Andy.Tools.Examples;
using Andy.Tools.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// WARNING: This is ALPHA software - use at your own risk!
Console.WriteLine("===========================================");
Console.WriteLine("Andy Tools Examples - ALPHA RELEASE");
Console.WriteLine("===========================================");
Console.WriteLine("⚠️  WARNING: This software can perform destructive operations!");
Console.WriteLine("⚠️  Always use in a safe, isolated environment with backups!");
Console.WriteLine();

// Setup dependency injection
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Add Andy Tools framework
services.AddAndyTools(options =>
{
    options.RegisterBuiltInTools = true;
    options.EnableDetailedTracing = true;
});

// Add advanced features
services.AddAdvancedToolFeatures(options =>
{
    options.EnableCaching = true;
    options.EnableMetrics = true;
});

// Add example-specific tools
services.AddTool<RandomNumberTool>();

var serviceProvider = services.BuildServiceProvider();

// Main menu
while (true)
{
    Console.WriteLine("\nSelect an example to run:");
    Console.WriteLine("1. Basic Tool Usage");
    Console.WriteLine("2. File Operations");
    Console.WriteLine("3. Text Processing");
    Console.WriteLine("4. Tool Chains");
    Console.WriteLine("5. Custom Tools");
    Console.WriteLine("6. Security and Permissions");
    Console.WriteLine("7. Caching Examples");
    Console.WriteLine("8. Web Operations");
    Console.WriteLine("9. System Information");
    Console.WriteLine("0. Exit");
    Console.Write("\nEnter your choice: ");

    var choice = Console.ReadLine();
    Console.WriteLine();

    try
    {
        switch (choice)
        {
            case "1":
                await BasicUsageExamples.RunAsync(serviceProvider);
                break;
            case "2":
                await FileOperationsExamples.RunAsync(serviceProvider);
                break;
            case "3":
                await TextProcessingExamples.RunAsync(serviceProvider);
                break;
            case "4":
                await ToolChainExamples.RunAsync(serviceProvider);
                break;
            case "5":
                await CustomToolExamples.RunAsync(serviceProvider);
                break;
            case "6":
                await SecurityExamples.RunAsync(serviceProvider);
                break;
            case "7":
                await CachingExamples.RunAsync(serviceProvider);
                break;
            case "8":
                await WebOperationsExamples.RunAsync(serviceProvider);
                break;
            case "9":
                await SystemInfoExamples.RunAsync(serviceProvider);
                break;
            case "0":
                Console.WriteLine("Exiting...");
                return;
            default:
                Console.WriteLine("Invalid choice. Please try again.");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error running example: {ex.Message}");
    }

    Console.WriteLine("\nPress any key to continue...");
    Console.ReadKey();
}