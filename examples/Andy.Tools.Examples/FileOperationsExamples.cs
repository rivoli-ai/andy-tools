using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Examples;

public static class FileOperationsExamples
{
    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== File Operations Examples ===\n");
        Console.WriteLine("⚠️  These examples create and delete files in a temporary directory.\n");

        var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();
        var tempDir = Path.Combine(Path.GetTempPath(), "andy-tools-file-examples");
        
        try
        {
            Directory.CreateDirectory(tempDir);
            var context = new ToolExecutionContext { WorkingDirectory = tempDir };

            // Example 1: Write and read a file
            Console.WriteLine("1. Write and Read File:");
            await WriteAndReadFile(toolExecutor, context);

            // Example 2: Copy file with progress
            Console.WriteLine("\n2. Copy File with Progress:");
            await CopyFileWithProgress(toolExecutor, context);

            // Example 3: List directory contents
            Console.WriteLine("\n3. List Directory Contents:");
            await ListDirectory(toolExecutor, context);

            // Example 4: Move/rename file
            Console.WriteLine("\n4. Move/Rename File:");
            await MoveFile(toolExecutor, context);

            // Example 5: Delete file safely
            Console.WriteLine("\n5. Delete File:");
            await DeleteFile(toolExecutor, context);
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static async Task WriteAndReadFile(IToolExecutor toolExecutor, ToolExecutionContext context)
    {
        var content = """
            This is a sample file created by Andy Tools.
            It demonstrates the write_file and read_file tools.
            
            Features:
            - Simple API
            - Error handling
            - Permission checks
            """;

        // Write file
        var writeParams = new Dictionary<string, object?>
        {
            ["file_path"] = "sample.txt",
            ["content"] = content,
            ["create_directories"] = true
        };

        var writeResult = await toolExecutor.ExecuteAsync("write_file", writeParams, context);
        Console.WriteLine($"Write result: {(writeResult.IsSuccessful ? "Success" : writeResult.ErrorMessage)}");

        // Read file
        var readParams = new Dictionary<string, object?>
        {
            ["file_path"] = "sample.txt"
        };

        var readResult = await toolExecutor.ExecuteAsync("read_file", readParams, context);
        if (readResult.IsSuccessful)
        {
            Console.WriteLine("File content (first 100 chars):");
            var preview = readResult.Data?.ToString() ?? "";
            Console.WriteLine(preview.Length > 100 ? preview[..100] + "..." : preview);
        }
    }

    private static async Task CopyFileWithProgress(IToolExecutor toolExecutor, ToolExecutionContext context)
    {
        // Create a larger file for progress demonstration
        var sourceFile = Path.Combine(context.WorkingDirectory!, "large-file.dat");
        var data = new byte[5 * 1024 * 1024]; // 5MB
        new Random().NextBytes(data);
        await File.WriteAllBytesAsync(sourceFile, data);

        var copyParams = new Dictionary<string, object?>
        {
            ["source_path"] = "large-file.dat",
            ["destination_path"] = "large-file-copy.dat",
            ["overwrite"] = true,
            ["buffer_size"] = 65536
        };

        // Set up progress reporting
        context.OnProgressWithPercentage = (percentage, message) =>
        {
            Console.Write($"\rCopying: {percentage:F1}% - {message}");
        };

        var result = await toolExecutor.ExecuteAsync("copy_file", copyParams, context);
        Console.WriteLine(); // New line after progress
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> copyStats)
        {
            Console.WriteLine($"Copy completed:");
            Console.WriteLine($"- Bytes copied: {copyStats.GetValueOrDefault("bytes_copied")}");
            Console.WriteLine($"- Duration: {copyStats.GetValueOrDefault("duration_ms")}ms");
            Console.WriteLine($"- Throughput: {copyStats.GetValueOrDefault("throughput_mb_per_sec")} MB/s");
        }
    }

    private static async Task ListDirectory(IToolExecutor toolExecutor, ToolExecutionContext context)
    {
        // Create some test files
        await File.WriteAllTextAsync(Path.Combine(context.WorkingDirectory!, "readme.txt"), "Readme");
        await File.WriteAllTextAsync(Path.Combine(context.WorkingDirectory!, "data.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(context.WorkingDirectory!, "script.sh"), "#!/bin/bash");
        
        var subDir = Path.Combine(context.WorkingDirectory!, "subdir");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "nested.txt"), "Nested file");

        var listParams = new Dictionary<string, object?>
        {
            ["directory_path"] = ".",
            ["recursive"] = true,
            ["include_details"] = true,
            ["sort_by"] = "name",
            ["pattern"] = "*.txt"
        };

        var result = await toolExecutor.ExecuteAsync("list_directory", listParams, context);
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> listData)
        {
            if (listData.TryGetValue("items", out var items) && items is List<object> fileList)
            {
                Console.WriteLine($"Found {fileList.Count} .txt files:");
                foreach (var item in fileList)
                {
                    if (item is Dictionary<string, object?> file)
                    {
                        Console.WriteLine($"- {file.GetValueOrDefault("name")} ({file.GetValueOrDefault("size")} bytes)");
                    }
                }
            }
        }
    }

    private static async Task MoveFile(IToolExecutor toolExecutor, ToolExecutionContext context)
    {
        // Create a file to move
        var originalPath = Path.Combine(context.WorkingDirectory!, "original.txt");
        await File.WriteAllTextAsync(originalPath, "File to be moved");

        var moveParams = new Dictionary<string, object?>
        {
            ["source_path"] = "original.txt",
            ["destination_path"] = "moved/renamed.txt",
            ["create_directories"] = true,
            ["overwrite"] = false
        };

        var result = await toolExecutor.ExecuteAsync("move_file", moveParams, context);
        
        if (result.IsSuccessful)
        {
            Console.WriteLine("File moved successfully");
            
            // Verify the move
            var oldExists = File.Exists(originalPath);
            var newExists = File.Exists(Path.Combine(context.WorkingDirectory!, "moved", "renamed.txt"));
            
            Console.WriteLine($"Original exists: {oldExists}");
            Console.WriteLine($"New location exists: {newExists}");
        }
        else
        {
            Console.WriteLine($"Move failed: {result.ErrorMessage}");
        }
    }

    private static async Task DeleteFile(IToolExecutor toolExecutor, ToolExecutionContext context)
    {
        // Create a file to delete
        var fileToDelete = Path.Combine(context.WorkingDirectory!, "delete-me.txt");
        await File.WriteAllTextAsync(fileToDelete, "This file will be deleted");

        Console.WriteLine($"File exists before delete: {File.Exists(fileToDelete)}");

        var deleteParams = new Dictionary<string, object?>
        {
            ["file_path"] = "delete-me.txt",
            ["recursive"] = false // For directories
        };

        var result = await toolExecutor.ExecuteAsync("delete_file", deleteParams, context);
        
        if (result.IsSuccessful)
        {
            Console.WriteLine("File deleted successfully");
            Console.WriteLine($"File exists after delete: {File.Exists(fileToDelete)}");
        }
        else
        {
            Console.WriteLine($"Delete failed: {result.ErrorMessage}");
        }
    }
}