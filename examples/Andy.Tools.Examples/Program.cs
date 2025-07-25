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
services.AddTool<WordCountTool>();
services.AddTool<CsvProcessorTool>();
services.AddTool<PasswordGeneratorTool>();

var serviceProvider = services.BuildServiceProvider();

// Initialize the tool framework (needed for console apps)
var lifecycleManager = serviceProvider.GetRequiredService<IToolLifecycleManager>();
await lifecycleManager.InitializeAsync();

// Check command line arguments
var cmdArgs = Environment.GetCommandLineArgs();
var exampleToRun = cmdArgs.Length > 1 ? cmdArgs[1] : "all";

Console.WriteLine($"Running example: {exampleToRun}\n");

try
{
    switch (exampleToRun.ToLowerInvariant())
    {
        case "test":
            await TestBasic.RunAsync(serviceProvider);
            break;
        case "debug":
            await DebugToolRegistry.RunAsync(serviceProvider);
            break;
        case "1":
        case "basic":
            await BasicUsageExamples.RunAsync(serviceProvider);
            break;
        case "2":
        case "file":
            await FileOperationsExamples.RunAsync(serviceProvider);
            break;
        case "3":
        case "text":
            await TextProcessingExamples.RunAsync(serviceProvider);
            break;
        case "4":
        case "chain":
            await ToolChainExamples.RunAsync(serviceProvider);
            break;
        case "5":
        case "custom":
            await CustomToolExamples.RunAsync(serviceProvider);
            break;
        case "6":
        case "security":
            await SecurityExamples.RunAsync(serviceProvider);
            break;
        case "7":
        case "cache":
            await CachingExamples.RunAsync(serviceProvider);
            break;
        case "8":
        case "web":
            await WebOperationsExamples.RunAsync(serviceProvider);
            break;
        case "9":
        case "system":
            await SystemInfoExamples.RunAsync(serviceProvider);
            break;
        case "all":
            Console.WriteLine("Running all examples...\n");
            
            Console.WriteLine("\n=== BASIC USAGE ===");
            await BasicUsageExamples.RunAsync(serviceProvider);
            
            Console.WriteLine("\n\n=== FILE OPERATIONS ===");
            await FileOperationsExamples.RunAsync(serviceProvider);
            
            Console.WriteLine("\n\n=== TEXT PROCESSING ===");
            await TextProcessingExamples.RunAsync(serviceProvider);
            
            Console.WriteLine("\n\n=== TOOL CHAINS ===");
            await ToolChainExamples.RunAsync(serviceProvider);
            
            Console.WriteLine("\n\n=== CUSTOM TOOLS ===");
            await CustomToolExamples.RunAsync(serviceProvider);
            
            Console.WriteLine("\n\n=== SECURITY ===");
            await SecurityExamples.RunAsync(serviceProvider);
            
            Console.WriteLine("\n\n=== CACHING ===");
            await CachingExamples.RunAsync(serviceProvider);
            
            Console.WriteLine("\n\n=== WEB OPERATIONS ===");
            await WebOperationsExamples.RunAsync(serviceProvider);
            
            Console.WriteLine("\n\n=== SYSTEM INFO ===");
            await SystemInfoExamples.RunAsync(serviceProvider);
            break;
        default:
            Console.WriteLine($"Unknown example: {exampleToRun}");
            Console.WriteLine("Usage: dotnet run [example]");
            Console.WriteLine("Where [example] is one of:");
            Console.WriteLine("  1 or basic    - Basic Tool Usage");
            Console.WriteLine("  2 or file     - File Operations");
            Console.WriteLine("  3 or text     - Text Processing");
            Console.WriteLine("  4 or chain    - Tool Chains");
            Console.WriteLine("  5 or custom   - Custom Tools");
            Console.WriteLine("  6 or security - Security and Permissions");
            Console.WriteLine("  7 or cache    - Caching Examples");
            Console.WriteLine("  8 or web      - Web Operations");
            Console.WriteLine("  9 or system   - System Information");
            Console.WriteLine("  all           - Run all examples (default)");
            Environment.Exit(1);
            break;
    }
    
    Console.WriteLine("\n\n✅ Examples completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Error running examples: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}