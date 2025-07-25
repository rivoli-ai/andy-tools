using Andy.Tools.Advanced.Configuration;
using Andy.Tools.Advanced.ToolChains;
using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Examples;

public static class ToolChainExamples
{
    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Tool Chain Examples ===\n");
        Console.WriteLine("Tool chains allow you to orchestrate multiple tools in sequence.\n");

        var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();
        var tempDir = Path.Combine(Path.GetTempPath(), "andy-tools-chain-examples");
        
        try
        {
            Directory.CreateDirectory(tempDir);
            var context = new ToolExecutionContext { WorkingDirectory = tempDir };

            // Example 1: Simple sequential execution
            Console.WriteLine("1. Simple Sequential Execution:");
            await RunSimpleSequence(toolExecutor, context);

            // Example 2: Process multiple files
            Console.WriteLine("\n2. Process Multiple Files:");
            await ProcessMultipleFiles(toolExecutor, context);

            // Example 3: Conditional processing
            Console.WriteLine("\n3. Conditional Processing:");
            await ConditionalProcessing(toolExecutor, context);

            // Example 4: Data transformation
            Console.WriteLine("\n4. Data Transformation:");
            await DataTransformation(toolExecutor, context);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static async Task RunSimpleSequence(IToolExecutor toolExecutor, ToolExecutionContext context)
    {
        Console.WriteLine("Creating a file, reading it, and transforming the content...");

        // Step 1: Create a file
        var createParams = new Dictionary<string, object?>
        {
            ["file_path"] = "input.txt",
            ["content"] = "Hello World! This is a TEST message."
        };
        
        var createResult = await toolExecutor.ExecuteAsync("write_file", createParams, context);
        if (!createResult.IsSuccessful)
        {
            Console.WriteLine($"Failed to create file: {createResult.ErrorMessage}");
            return;
        }
        Console.WriteLine("✓ File created");

        // Step 2: Read the file
        var readParams = new Dictionary<string, object?>
        {
            ["file_path"] = "input.txt"
        };
        
        var readResult = await toolExecutor.ExecuteAsync("read_file", readParams, context);
        if (!readResult.IsSuccessful)
        {
            Console.WriteLine($"Failed to read file: {readResult.ErrorMessage}");
            return;
        }
        Console.WriteLine("✓ File read");
        
        // Extract the content from read result
        var fileContent = readResult.Data is Dictionary<string, object?> readDict 
            ? readDict.GetValueOrDefault("content")?.ToString() ?? ""
            : readResult.Data?.ToString() ?? "";

        // Step 3: Transform to uppercase using simple string manipulation
        // (replace_text tool is for files, not in-memory strings)
        var transformedContent = fileContent.ToUpper();
        Console.WriteLine("✓ Text transformed");

        // Step 4: Save the result
            
        var saveParams = new Dictionary<string, object?>
        {
            ["file_path"] = "output.txt",
            ["content"] = transformedContent
        };
        
        var saveResult = await toolExecutor.ExecuteAsync("write_file", saveParams, context);
        if (saveResult.IsSuccessful)
        {
            Console.WriteLine("✓ Result saved");
            Console.WriteLine($"Final output: {transformedContent}");
        }
    }

    private static async Task ProcessMultipleFiles(IToolExecutor toolExecutor, ToolExecutionContext context)
    {
        // Create multiple files
        var files = new[] { "file1.txt", "file2.txt", "file3.txt" };
        var contents = new[] { "Content from file 1", "Content from file 2", "Content from file 3" };
        
        for (int i = 0; i < files.Length; i++)
        {
            var writeParams = new Dictionary<string, object?>
            {
                ["file_path"] = files[i],
                ["content"] = contents[i]
            };
            
            await toolExecutor.ExecuteAsync("write_file", writeParams, context);
        }
        Console.WriteLine($"Created {files.Length} files");

        // Read and combine all files
        var allContent = new List<string>();
        foreach (var file in files)
        {
            var readParams = new Dictionary<string, object?>
            {
                ["file_path"] = file
            };
            
            var result = await toolExecutor.ExecuteAsync("read_file", readParams, context);
            if (result.IsSuccessful)
            {
                var content = result.Data is Dictionary<string, object?> dict 
                    ? dict.GetValueOrDefault("content")?.ToString() ?? ""
                    : result.Data?.ToString() ?? "";
                allContent.Add(content);
            }
        }

        // Combine and save
        var combined = string.Join("\n---\n", allContent);
        var combineParams = new Dictionary<string, object?>
        {
            ["file_path"] = "combined.txt",
            ["content"] = combined
        };
        
        var combineResult = await toolExecutor.ExecuteAsync("write_file", combineParams, context);
        if (combineResult.IsSuccessful)
        {
            Console.WriteLine($"Combined {files.Length} files into combined.txt");
            Console.WriteLine($"Combined content length: {combined.Length} characters");
        }
    }

    private static async Task ConditionalProcessing(IToolExecutor toolExecutor, ToolExecutionContext context)
    {
        // Check if config exists
        var listParams = new Dictionary<string, object?>
        {
            ["directory_path"] = ".",
            ["pattern"] = "config.json"
        };
        
        var listResult = await toolExecutor.ExecuteAsync("list_directory", listParams, context);
        
        bool configExists = false;
        if (listResult.IsSuccessful && listResult.Data is Dictionary<string, object?> data)
        {
            if (data.TryGetValue("items", out var items) && items is List<object> list)
            {
                configExists = list.Count > 0;
            }
        }

        if (!configExists)
        {
            Console.WriteLine("Config not found, creating default...");
            
            var createParams = new Dictionary<string, object?>
            {
                ["file_path"] = "config.json",
                ["content"] = """{"version": "1.0", "settings": {"debug": true}}"""
            };
            
            await toolExecutor.ExecuteAsync("write_file", createParams, context);
            Console.WriteLine("Default config created");
        }
        else
        {
            Console.WriteLine("Config already exists");
        }
    }

    private static async Task DataTransformation(IToolExecutor toolExecutor, ToolExecutionContext context)
    {
        // Create sample CSV data
        var csvData = """
            Name,Age,City
            John Doe,30,New York
            Jane Smith,25,Los Angeles
            Bob Johnson,35,Chicago
            """;

        // Save CSV
        var csvParams = new Dictionary<string, object?>
        {
            ["file_path"] = "data.csv",
            ["content"] = csvData
        };
        
        await toolExecutor.ExecuteAsync("write_file", csvParams, context);
        Console.WriteLine("CSV file created");

        // Simple CSV to JSON conversion
        var lines = csvData.Trim().Split('\n');
        var headers = lines[0].Split(',');
        var items = new List<Dictionary<string, string>>();
        
        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            var item = new Dictionary<string, string>();
            for (int j = 0; j < headers.Length; j++)
            {
                item[headers[j]] = values[j];
            }
            items.Add(item);
        }
        
        // Format as JSON
        var json = System.Text.Json.JsonSerializer.Serialize(items);
        var formatParams = new Dictionary<string, object?>
        {
            ["text"] = json,
            ["format"] = "json",
            ["indent_size"] = 2
        };
        
        var formatResult = await toolExecutor.ExecuteAsync("format_text", formatParams, context);
        
        if (formatResult.IsSuccessful)
        {
            // Save formatted JSON
            var jsonParams = new Dictionary<string, object?>
            {
                ["file_path"] = "data.json",
                ["content"] = formatResult.Data
            };
            
            await toolExecutor.ExecuteAsync("write_file", jsonParams, context);
            
            Console.WriteLine("Data transformed from CSV to JSON:");
            Console.WriteLine(formatResult.Data);
        }
    }
}