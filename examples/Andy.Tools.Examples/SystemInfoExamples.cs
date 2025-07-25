using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

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
            
            if (result.IsSuccessful)
            {
                Console.WriteLine($"\n{category.ToUpper()} Information:");
                
                if (result.Data is Dictionary<string, object?> data)
                {
                    // The system_info tool returns all categories, so filter by requested category
                    if (data.TryGetValue(category, out var categoryData) && categoryData is Dictionary<string, object?> categoryDict)
                    {
                        foreach (var kvp in categoryDict.OrderBy(k => k.Key))
                        {
                            Console.WriteLine($"  {FormatKey(kvp.Key)}: {FormatValue(kvp.Value)}");
                        }
                    }
                    else
                    {
                        // If category not found, show what's available
                        Console.WriteLine($"  Category '{category}' not found. Available: {string.Join(", ", data.Keys)}");
                    }
                }
                else
                {
                    Console.WriteLine($"  Unexpected data type: {result.Data?.GetType().Name ?? "null"}");
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
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> data)
        {
            Console.WriteLine("Key Environment Variables:");
            
            // Check if environment data is nested
            Dictionary<string, object?>? envData = null;
            if (data.TryGetValue("environment", out var env) && env is Dictionary<string, object?> envDict)
            {
                envData = envDict;
                
                // Environment might have nested structure
                if (envDict.TryGetValue("environmentVariables", out var vars) && vars is Dictionary<string, object?> varsDict)
                {
                    envData = varsDict;
                }
                else if (envDict.TryGetValue("variables", out var vars2) && vars2 is Dictionary<string, object?> vars2Dict)
                {
                    envData = vars2Dict;
                }
            }
            else
            {
                // Debug: show what we got
                Console.WriteLine($"  Debug - Top level keys: {string.Join(", ", data.Keys.Take(10))}");
                envData = data;
            }
            
            // Show important environment variables
            var importantVars = new[] { "PATH", "HOME", "USER", "TEMP", "TMP", "USERPROFILE", "USERNAME" };
            int found = 0;
            
            foreach (var varName in importantVars)
            {
                if (envData.TryGetValue(varName, out var value) && value != null)
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
                    found++;
                }
            }
            
            if (found == 0)
            {
                // Show first few environment variables
                Console.WriteLine("  Sample environment variables:");
                foreach (var kvp in envData.Take(5))
                {
                    var value = kvp.Value?.ToString() ?? "";
                    
                    // Handle nested dictionaries (like safe_environment_variables)
                    if (kvp.Value is Dictionary<string, object?> nestedDict)
                    {
                        if (nestedDict.Count <= 3)
                        {
                            var items = nestedDict.Select(kv => $"{kv.Key}={kv.Value}");
                            value = string.Join(", ", items);
                        }
                        else
                        {
                            value = $"[{nestedDict.Count} environment variables]";
                        }
                    }
                    
                    if (value.Length > 100)
                        value = value[..100] + "...";
                    Console.WriteLine($"  {FormatKey(kvp.Key)}: {value}");
                }
            }
            
            // Show total count
            Console.WriteLine($"\n  Total environment variables: {envData.Count}");
        }
    }

    private static async Task GetProcessInfo(IToolExecutor toolExecutor)
    {
        // Get current process info
        var currentParams = new Dictionary<string, object?>
        {
            ["process_id"] = Environment.ProcessId,
            ["include_current"] = true,
            ["detailed"] = true
        };
        
        Console.WriteLine($"  (Requesting info for PID: {Environment.ProcessId})");

        var result = await toolExecutor.ExecuteAsync("process_info", currentParams);
        
        if (result.IsSuccessful)
        {
            Console.WriteLine("Current Process Information:");
            
            if (result.Data is Dictionary<string, object?> data)
            {
                // Check if it's a list response
                if (data.TryGetValue("items", out var items))
                {
                    var processList = items as List<object> ?? new List<object>();
                    if (items is IEnumerable<object> enumerable && !(items is List<object>))
                    {
                        processList = enumerable.ToList();
                    }
                    else if (items is object[] array)
                    {
                        processList = array.ToList();
                    }
                    
                    if (processList.Count == 0)
                    {
                        Console.WriteLine("  No process found with ID: " + Environment.ProcessId);
                        Console.WriteLine("  (Note: The process_info tool may have limited access to process information)");
                        Console.WriteLine("\n  Showing current process info from system_info instead:");
                        
                        // Try to get process info from system_info tool
                        var sysInfoParams = new Dictionary<string, object?>
                        {
                            ["category"] = "runtime"
                        };
                        var sysInfoResult = await toolExecutor.ExecuteAsync("system_info", sysInfoParams);
                        if (sysInfoResult.IsSuccessful && sysInfoResult.Data is Dictionary<string, object?> sysData)
                        {
                            if (sysData.TryGetValue("runtime", out var runtime) && runtime is Dictionary<string, object?> runtimeDict)
                            {
                                var pid = runtimeDict.GetValueOrDefault("process_id");
                                var name = runtimeDict.GetValueOrDefault("process_name");
                                var workingSet = runtimeDict.GetValueOrDefault("working_set");
                                var startTime = runtimeDict.GetValueOrDefault("start_time");
                                
                                Console.WriteLine($"  Process ID: {pid ?? "N/A"}");
                                Console.WriteLine($"  Name: {name ?? "N/A"}");
                                Console.WriteLine($"  Memory: {FormatBytes(workingSet)}");
                                Console.WriteLine($"  Start Time: {startTime ?? "N/A"}");
                            }
                        }
                    }
                    else
                    {
                        // If we get a list, take the first one (should be our requested process)
                        if (processList[0] is Dictionary<string, object?> processInfo)
                        {
                            DisplayProcessInfo(processInfo);
                        }
                        else
                        {
                            // Handle strongly-typed ProcessInfo objects
                            var firstItem = processList[0];
                            if (firstItem != null)
                            {
                                // Convert the object to a dictionary using reflection
                                var processDict = new Dictionary<string, object?>();
                                var type = firstItem.GetType();
                                foreach (var prop in type.GetProperties())
                                {
                                    var value = prop.GetValue(firstItem);
                                    processDict[prop.Name] = value;
                                }
                                DisplayProcessInfo(processDict);
                            }
                        }
                    }
                }
                else if (!data.ContainsKey("items") && !data.ContainsKey("count"))
                {
                    // Only try to display as single process info if it's not a list structure
                    DisplayProcessInfo(data);
                }
                else
                {
                    // We have a list structure but items is not a List<object>
                    Console.WriteLine($"\n  Debug - Response has list structure but unexpected format");
                    Console.WriteLine($"  Response keys: {string.Join(", ", data.Keys)}");
                }
            }
            else
            {
                Console.WriteLine($"  Unexpected data type: {result.Data?.GetType().Name ?? "null"}");
            }
        }

        // List all processes (top 5 by memory)
        var listParams = new Dictionary<string, object?>
        {
            ["sort_by"] = "memory",
            ["max_results"] = 5,
            ["detailed"] = false
        };

        var listResult = await toolExecutor.ExecuteAsync("process_info", listParams);
        
        if (listResult.IsSuccessful)
        {
            if (listResult.Data is Dictionary<string, object?> data && data.TryGetValue("items", out var items) && items is List<object> processes)
            {
                Console.WriteLine("\nTop 5 Processes by Memory:");
                foreach (var proc in processes.Take(5))
                {
                    Dictionary<string, object?>? p = null;
                    
                    if (proc is Dictionary<string, object?> dict)
                    {
                        p = dict;
                    }
                    else if (proc != null)
                    {
                        // Convert strongly-typed object to dictionary
                        p = new Dictionary<string, object?>();
                        var type = proc.GetType();
                        foreach (var prop in type.GetProperties())
                        {
                            p[prop.Name] = prop.GetValue(proc);
                        }
                    }
                    
                    if (p != null)
                    {
                        var name = p.GetValueOrDefault("process_name") ?? p.GetValueOrDefault("name") ?? p.GetValueOrDefault("processName") ?? p.GetValueOrDefault("Name");
                        var pid = p.GetValueOrDefault("process_id") ?? p.GetValueOrDefault("pid") ?? p.GetValueOrDefault("id") ?? p.GetValueOrDefault("Id");
                        var memory = p.GetValueOrDefault("working_set_memory") ?? p.GetValueOrDefault("workingSet") ?? p.GetValueOrDefault("memory") ?? p.GetValueOrDefault("WorkingSet");
                        
                        Console.WriteLine($"  - {name ?? "Unknown"} " +
                                        $"(PID: {pid ?? "?"}, " +
                                        $"Memory: {FormatBytes(memory)})");
                    }
                }
            }
            else if (listResult.Data is List<object> directProcesses)
            {
                Console.WriteLine("\nTop 5 Processes by Memory:");
                foreach (var proc in directProcesses.Take(5))
                {
                    if (proc is Dictionary<string, object?> p)
                    {
                        var name = p.GetValueOrDefault("process_name") ?? p.GetValueOrDefault("name");
                        var pid = p.GetValueOrDefault("process_id") ?? p.GetValueOrDefault("pid");
                        var memory = p.GetValueOrDefault("working_set_memory") ?? p.GetValueOrDefault("memory");
                        
                        Console.WriteLine($"  - {name ?? "Unknown"} " +
                                        $"(PID: {pid ?? "?"}, " +
                                        $"Memory: {FormatBytes(memory)})");
                    }
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
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> data)
        {
            Console.WriteLine("System Metrics:");
            
            // Since system_info returns all categories, we need to look at multiple sections
            // Memory info from the memory section
            if (data.TryGetValue("memory", out var mem) && mem is Dictionary<string, object?> memDict)
            {
                var workingSet = memDict.GetValueOrDefault("workingSet") ?? memDict.GetValueOrDefault("working_set");
                var gcMemory = memDict.GetValueOrDefault("gcTotalMemory") ?? memDict.GetValueOrDefault("gc_total_memory");
                
                if (workingSet != null)
                    Console.WriteLine($"  Working Set Memory: {FormatBytes(workingSet)}");
                if (gcMemory != null)
                    Console.WriteLine($"  GC Total Memory: {FormatBytes(gcMemory)}");
            }
            
            // CPU info from cpu section
            if (data.TryGetValue("cpu", out var cpu) && cpu is Dictionary<string, object?> cpuDict)
            {
                var procCount = cpuDict.GetValueOrDefault("processorCount") ?? cpuDict.GetValueOrDefault("processor_count");
                var arch = cpuDict.GetValueOrDefault("architecture");
                
                if (procCount != null)
                    Console.WriteLine($"  CPU Cores: {procCount}");
                if (arch != null)
                    Console.WriteLine($"  Architecture: {arch}");
            }
            
            // Storage info
            if (data.TryGetValue("storage", out var storage) && storage is Dictionary<string, object?> storageDict)
            {
                if (storageDict.TryGetValue("drives", out var drives) && drives is List<object> driveList)
                {
                    Console.WriteLine("\n  Disk Drives:");
                    foreach (var drive in driveList)
                    {
                        if (drive is Dictionary<string, object?> d)
                        {
                            var name = d.GetValueOrDefault("name");
                            var totalSize = FormatBytes(d.GetValueOrDefault("totalSize") ?? d.GetValueOrDefault("total_size") ?? d.GetValueOrDefault("total_size_formatted"));
                            var freeSpace = FormatBytes(d.GetValueOrDefault("availableSpace") ?? d.GetValueOrDefault("available_space") ?? d.GetValueOrDefault("available_free_space"));
                            Console.WriteLine($"    {name}: {freeSpace} free of {totalSize}");
                        }
                    }
                }
                else
                {
                    var driveCount = storageDict.GetValueOrDefault("driveCount") ?? storageDict.GetValueOrDefault("drive_count");
                    if (driveCount != null)
                        Console.WriteLine($"  Total Drives: {driveCount}");
                }
            }
            
            // OS info
            if (data.TryGetValue("os", out var os) && os is Dictionary<string, object?> osDict)
            {
                var version = osDict.GetValueOrDefault("versionString") ?? osDict.GetValueOrDefault("version_string");
                if (version != null)
                    Console.WriteLine($"\n  OS Version: {version}");
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
        
        // Handle nested dictionaries
        if (value is Dictionary<string, object?> dict)
        {
            // For simple dictionaries with few items, show inline
            if (dict.Count <= 3)
            {
                var items = dict.Select(kvp => $"{kvp.Key}={kvp.Value}");
                return string.Join(", ", items);
            }
            // For larger dictionaries, just show count
            return $"[{dict.Count} items]";
        }
        
        // Handle lists
        if (value is List<object> list)
        {
            return $"[{list.Count} items]";
        }
        
        return value.ToString() ?? "N/A";
    }

    private static void DisplayProcessInfo(Dictionary<string, object?> processInfo)
    {
        var pid = processInfo.GetValueOrDefault("process_id") ?? 
                 processInfo.GetValueOrDefault("pid") ?? 
                 processInfo.GetValueOrDefault("id") ?? 
                 processInfo.GetValueOrDefault("Id");
                 
        var name = processInfo.GetValueOrDefault("process_name") ?? 
                  processInfo.GetValueOrDefault("name") ?? 
                  processInfo.GetValueOrDefault("processName") ?? 
                  processInfo.GetValueOrDefault("Name");
                  
        var memory = processInfo.GetValueOrDefault("working_set_memory") ?? 
                    processInfo.GetValueOrDefault("workingSet") ?? 
                    processInfo.GetValueOrDefault("memory") ?? 
                    processInfo.GetValueOrDefault("WorkingSet");
                    
        var cpu = processInfo.GetValueOrDefault("total_cpu_time") ?? 
                 processInfo.GetValueOrDefault("cpuTime") ?? 
                 processInfo.GetValueOrDefault("cpu") ?? 
                 processInfo.GetValueOrDefault("TotalProcessorTime");
                 
        var threads = processInfo.GetValueOrDefault("thread_count") ?? 
                     processInfo.GetValueOrDefault("threads") ?? 
                     processInfo.GetValueOrDefault("ThreadCount");
                     
        var startTime = processInfo.GetValueOrDefault("start_time") ?? 
                       processInfo.GetValueOrDefault("startTime") ?? 
                       processInfo.GetValueOrDefault("StartTime");
        
        Console.WriteLine($"  Process ID: {pid ?? "N/A"}");
        Console.WriteLine($"  Name: {name ?? "N/A"}");
        Console.WriteLine($"  Memory: {FormatBytes(memory)}");
        Console.WriteLine($"  CPU Time: {cpu ?? "N/A"}");
        Console.WriteLine($"  Threads: {threads ?? "N/A"}");
        Console.WriteLine($"  Start Time: {startTime ?? "N/A"}");
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