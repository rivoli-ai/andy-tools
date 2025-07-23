using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Examples;

public static class SystemInfoExamples
{
    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== System Information Examples ===\n");

        var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();

        // Example 1: Basic system info
        Console.WriteLine("1. Basic System Information:");
        await GetBasicSystemInfo(toolExecutor);

        // Example 2: Environment information
        Console.WriteLine("\n2. Environment Information:");
        await GetEnvironmentInfo(toolExecutor);

        // Example 3: Process information
        Console.WriteLine("\n3. Process Information:");
        await GetProcessInfo(toolExecutor);

        // Example 4: System metrics
        Console.WriteLine("\n4. System Metrics:");
        await GetSystemMetrics(toolExecutor);
    }

    private static async Task GetBasicSystemInfo(IToolExecutor toolExecutor)
    {
        var categories = new[] { "os", "hardware", "runtime" };

        foreach (var category in categories)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["category"] = category
            };

            var result = await toolExecutor.ExecuteAsync("system_info", parameters);
            
            if (result.IsSuccessful && result.Data is Dictionary<string, object?> info)
            {
                Console.WriteLine($"\n{category.ToUpper()} Information:");
                foreach (var kvp in info.OrderBy(k => k.Key))
                {
                    Console.WriteLine($"  {FormatKey(kvp.Key)}: {FormatValue(kvp.Value)}");
                }
            }
        }
    }

    private static async Task GetEnvironmentInfo(IToolExecutor toolExecutor)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["category"] = "environment"
        };

        var result = await toolExecutor.ExecuteAsync("system_info", parameters);
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> info)
        {
            // Show important environment variables
            var importantVars = new[] { "PATH", "HOME", "USER", "TEMP", "TMP" };
            
            Console.WriteLine("Key Environment Variables:");
            foreach (var varName in importantVars)
            {
                if (info.TryGetValue(varName, out var value) && value != null)
                {
                    var strValue = value.ToString() ?? "";
                    if (varName == "PATH" && strValue.Length > 100)
                    {
                        // Truncate long PATH for display
                        Console.WriteLine($"  {varName}: {strValue[..100]}...");
                    }
                    else
                    {
                        Console.WriteLine($"  {varName}: {strValue}");
                    }
                }
            }
        }
    }

    private static async Task GetProcessInfo(IToolExecutor toolExecutor)
    {
        // Get current process info
        var currentParams = new Dictionary<string, object?>
        {
            ["process_id"] = Environment.ProcessId
        };

        var result = await toolExecutor.ExecuteAsync("process_info", currentParams);
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> processInfo)
        {
            Console.WriteLine("Current Process Information:");
            Console.WriteLine($"  Process ID: {processInfo.GetValueOrDefault("process_id")}");
            Console.WriteLine($"  Name: {processInfo.GetValueOrDefault("process_name")}");
            Console.WriteLine($"  Memory: {FormatBytes(processInfo.GetValueOrDefault("working_set_memory"))}");
            Console.WriteLine($"  CPU Time: {processInfo.GetValueOrDefault("total_cpu_time")}");
            Console.WriteLine($"  Threads: {processInfo.GetValueOrDefault("thread_count")}");
            Console.WriteLine($"  Start Time: {processInfo.GetValueOrDefault("start_time")}");
        }

        // List all processes (top 5 by memory)
        var listParams = new Dictionary<string, object?>
        {
            ["sort_by"] = "memory",
            ["limit"] = 5
        };

        var listResult = await toolExecutor.ExecuteAsync("process_info", listParams);
        
        if (listResult.IsSuccessful && listResult.Data is List<object> processes)
        {
            Console.WriteLine("\nTop 5 Processes by Memory:");
            foreach (var proc in processes)
            {
                if (proc is Dictionary<string, object?> p)
                {
                    Console.WriteLine($"  - {p.GetValueOrDefault("process_name")} " +
                                    $"(PID: {p.GetValueOrDefault("process_id")}, " +
                                    $"Memory: {FormatBytes(p.GetValueOrDefault("working_set_memory"))})");
                }
            }
        }
    }

    private static async Task GetSystemMetrics(IToolExecutor toolExecutor)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["category"] = "metrics"
        };

        var result = await toolExecutor.ExecuteAsync("system_info", parameters);
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> metrics)
        {
            Console.WriteLine("System Metrics:");
            
            // Memory metrics
            if (metrics.TryGetValue("total_memory", out var totalMem))
            {
                Console.WriteLine($"  Total Memory: {FormatBytes(totalMem)}");
            }
            if (metrics.TryGetValue("available_memory", out var availMem))
            {
                Console.WriteLine($"  Available Memory: {FormatBytes(availMem)}");
            }
            
            // CPU metrics
            if (metrics.TryGetValue("processor_count", out var cpuCount))
            {
                Console.WriteLine($"  CPU Cores: {cpuCount}");
            }
            
            // Disk metrics
            if (metrics.TryGetValue("drives", out var drives) && drives is List<object> driveList)
            {
                Console.WriteLine("\n  Disk Drives:");
                foreach (var drive in driveList)
                {
                    if (drive is Dictionary<string, object?> d)
                    {
                        var name = d.GetValueOrDefault("name");
                        var totalSize = FormatBytes(d.GetValueOrDefault("total_size"));
                        var freeSpace = FormatBytes(d.GetValueOrDefault("available_space"));
                        Console.WriteLine($"    {name}: {freeSpace} free of {totalSize}");
                    }
                }
            }
        }
    }

    private static string FormatKey(string key)
    {
        // Convert snake_case to Title Case
        return string.Join(" ", key.Split('_'))
            .Replace(" ", " ")
            .Trim()
            .Split(' ')
            .Select(word => char.ToUpper(word[0]) + word[1..].ToLower())
            .Aggregate((a, b) => a + " " + b);
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return "N/A";
        
        if (value is bool b) return b ? "Yes" : "No";
        if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");
        if (value is TimeSpan ts) return ts.ToString(@"d\.hh\:mm\:ss");
        
        return value.ToString() ?? "N/A";
    }

    private static string FormatBytes(object? bytes)
    {
        if (bytes == null) return "N/A";
        
        if (!long.TryParse(bytes.ToString(), out var b))
            return bytes.ToString() ?? "N/A";

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = b;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}