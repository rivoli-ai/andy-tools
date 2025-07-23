using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;
using SystemNet = System.Net;

namespace Andy.Tools.Library.System;

/// <summary>
/// Tool for retrieving system information.
/// </summary>
public class SystemInfoTool : Andy.Tools.Library.ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "system_info",
        Name = "System Information",
        Description = "Retrieves detailed system information including OS, hardware, and runtime details",
        Version = "1.0.0",
        Category = ToolCategory.System,
        RequiredPermissions = ToolPermissionFlags.SystemInformation,
        Parameters =
        [
            new()
            {
                Name = "categories",
                Description = "Array of information categories to include (default: all). Valid values: os, hardware, runtime, environment, network, storage, memory, cpu",
                Type = "array",
                Required = false,
                ItemType = new ToolParameter
                {
                    Type = "string",
                    Description = "Information category (e.g., 'os', 'hardware')"
                }
            },
            new()
            {
                Name = "include_sensitive",
                Description = "Whether to include potentially sensitive information (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "detailed",
                Description = "Whether to include detailed information (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var categoriesParam = GetParameterAsStringList(parameters, "categories") ?? [];
        var includeSensitive = GetParameter(parameters, "include_sensitive", false);
        var detailed = GetParameter(parameters, "detailed", false);

        try
        {
            ReportProgress(context, "Gathering system information...", 10);

            var systemInfo = new Dictionary<string, object?>();
            var validCategories = new List<string> { "os", "hardware", "runtime", "environment", "network", "storage", "memory", "cpu" };
            var categories = categoriesParam.Count > 0 ? categoriesParam : validCategories;

            // Validate categories
            var invalidCategories = categories.Where(c => !validCategories.Contains(c.ToLowerInvariant())).ToList();
            if (invalidCategories.Count > 0)
            {
                return ToolResults.InvalidParameter(
                    "categories", 
                    string.Join(", ", invalidCategories),
                    $"Parameter 'categories' must be one of: {string.Join(", ", validCategories)}"
                );
            }

            var totalCategories = categories.Count;
            var processedCategories = 0;

            foreach (var category in categories)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var categoryInfo = await GatherCategoryInfoAsync(category.ToLowerInvariant(), includeSensitive, detailed, context);
                    if (categoryInfo != null)
                    {
                        systemInfo[category] = categoryInfo;
                    }

                    processedCategories++;
                    var progressPercent = 10 + (processedCategories * 80 / totalCategories);
                    ReportProgress(context, $"Gathered {category} information", progressPercent);
                }
                catch (Exception ex)
                {
                    systemInfo[$"{category}_error"] = $"Failed to gather {category} information: {ex.Message}";
                }
            }

            ReportProgress(context, "System information gathered", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["categories_requested"] = categories,
                ["include_sensitive"] = includeSensitive,
                ["detailed"] = detailed,
                ["gathered_at"] = DateTime.UtcNow,
                ["categories_count"] = systemInfo.Count
            };

            return ToolResults.Success(
                systemInfo,
                $"Successfully gathered system information for {systemInfo.Count} categories",
                metadata
            );
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Failed to gather system information: {ex.Message}", "SYSTEM_INFO_ERROR", details: ex);
        }
    }

    private async Task<object?> GatherCategoryInfoAsync(string category, bool includeSensitive, bool detailed, ToolExecutionContext context)
    {
        return category switch
        {
            "os" => await GatherOSInfoAsync(includeSensitive, detailed, context),
            "hardware" => await GatherHardwareInfoAsync(detailed, context),
            "runtime" => await GatherRuntimeInfoAsync(detailed, context),
            "environment" => await GatherEnvironmentInfoAsync(includeSensitive, context),
            "network" => await GatherNetworkInfoAsync(detailed, context),
            "storage" => await GatherStorageInfoAsync(detailed, context),
            "memory" => await GatherMemoryInfoAsync(detailed, context),
            "cpu" => await GatherCPUInfoAsync(detailed, context),
            _ => null
        };
    }

    private static async Task<object> GatherOSInfoAsync(bool includeSensitive, bool detailed, ToolExecutionContext context)
    {
        await Task.CompletedTask;

        var osInfo = new Dictionary<string, object?>
        {
            ["platform"] = Environment.OSVersion.Platform.ToString(),
            ["version"] = Environment.OSVersion.Version.ToString(),
            ["version_string"] = Environment.OSVersion.VersionString,
            ["is_64_bit"] = Environment.Is64BitOperatingSystem,
            ["machine_name"] = Environment.MachineName,
            ["user_domain"] = Environment.UserDomainName,
            ["system_directory"] = Environment.SystemDirectory,
            ["current_directory"] = Environment.CurrentDirectory
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            osInfo["os_family"] = "Windows";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            osInfo["os_family"] = "Linux";
        }
        else
        {
            osInfo["os_family"] = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" : "Unknown";
        }

        osInfo["architecture"] = RuntimeInformation.OSArchitecture.ToString();
        osInfo["description"] = RuntimeInformation.OSDescription;

        if (includeSensitive)
        {
            osInfo["user_name"] = Environment.UserName;
        }

        if (detailed)
        {
            try
            {
                osInfo["process_architecture"] = RuntimeInformation.ProcessArchitecture.ToString();
                osInfo["framework_description"] = RuntimeInformation.FrameworkDescription;
                osInfo["uptime_ticks"] = Environment.TickCount64;
                osInfo["uptime_days"] = TimeSpan.FromMilliseconds(Environment.TickCount64).TotalDays;
            }
            catch (Exception ex)
            {
                osInfo["detailed_info_error"] = ex.Message;
            }
        }

        return osInfo;
    }

    private static async Task<object> GatherHardwareInfoAsync(bool detailed, ToolExecutionContext context)
    {
        await Task.CompletedTask;

        var hardwareInfo = new Dictionary<string, object?>
        {
            ["processor_count"] = Environment.ProcessorCount,
            ["is_64_bit_process"] = Environment.Is64BitProcess,
            ["page_size"] = Environment.SystemPageSize
        };

        if (detailed)
        {
            try
            {
                // Get memory information
                var workingSet = Environment.WorkingSet;
                hardwareInfo["working_set_bytes"] = workingSet;
                hardwareInfo["working_set_formatted"] = ToolHelpers.FormatFileSize(workingSet);

                // PerformanceCounter is not available in .NET Core/5+
                // CPU usage would require platform-specific implementation or a NuGet package
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    hardwareInfo["cpu_usage_note"] = "CPU usage monitoring requires additional packages in .NET Core/5+";
                }
            }
            catch (Exception ex)
            {
                hardwareInfo["detailed_info_error"] = ex.Message;
            }
        }

        return hardwareInfo;
    }

    private static async Task<object> GatherRuntimeInfoAsync(bool detailed, ToolExecutionContext context)
    {
        await Task.CompletedTask;

        var runtimeInfo = new Dictionary<string, object?>
        {
            ["clr_version"] = Environment.Version.ToString(),
            ["framework_description"] = RuntimeInformation.FrameworkDescription,
            ["runtime_identifier"] = RuntimeInformation.RuntimeIdentifier,
            ["current_managed_thread_id"] = Environment.CurrentManagedThreadId,
            ["has_shutdown_started"] = Environment.HasShutdownStarted
        };

        if (detailed)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                runtimeInfo["process_id"] = currentProcess.Id;
                runtimeInfo["process_name"] = currentProcess.ProcessName;
                runtimeInfo["start_time"] = currentProcess.StartTime;
                runtimeInfo["total_processor_time"] = currentProcess.TotalProcessorTime.TotalMilliseconds;
                runtimeInfo["user_processor_time"] = currentProcess.UserProcessorTime.TotalMilliseconds;
                runtimeInfo["privileged_processor_time"] = currentProcess.PrivilegedProcessorTime.TotalMilliseconds;
                runtimeInfo["virtual_memory_size"] = currentProcess.VirtualMemorySize64;
                runtimeInfo["working_set"] = currentProcess.WorkingSet64;
                runtimeInfo["paged_memory_size"] = currentProcess.PagedMemorySize64;

                // GC Information
                runtimeInfo["gc_total_memory"] = GC.GetTotalMemory(false);
                runtimeInfo["gc_max_generation"] = GC.MaxGeneration;

                for (int i = 0; i <= GC.MaxGeneration; i++)
                {
                    runtimeInfo[$"gc_gen{i}_collections"] = GC.CollectionCount(i);
                }
            }
            catch (Exception ex)
            {
                runtimeInfo["detailed_info_error"] = ex.Message;
            }
        }

        return runtimeInfo;
    }

    private static async Task<object> GatherEnvironmentInfoAsync(bool includeSensitive, ToolExecutionContext context)
    {
        await Task.CompletedTask;

        var envInfo = new Dictionary<string, object?>
        {
            ["command_line"] = Environment.CommandLine.Split(' ').FirstOrDefault(),
            ["current_directory"] = Environment.CurrentDirectory,
            ["system_directory"] = Environment.SystemDirectory,
            ["temp_path"] = Path.GetTempPath()
        };

        if (includeSensitive)
        {
            var envVars = new Dictionary<string, object?>();
            foreach (DictionaryEntry envVar in Environment.GetEnvironmentVariables())
            {
                if (envVar.Key is string key && envVar.Value is string value)
                {
                    // Filter out potentially sensitive variables unless explicitly requested
                    if (!IsSensitiveEnvironmentVariable(key))
                    {
                        envVars[key] = value;
                    }
                }
            }

            envInfo["environment_variables"] = envVars;
            envInfo["environment_variables_count"] = envVars.Count;
        }
        else
        {
            // Only include safe environment variables
            var safeVars = new Dictionary<string, object?>();
            var commonSafeVars = new[] { "PATH", "TEMP", "TMP", "OS", "PROCESSOR_ARCHITECTURE", "NUMBER_OF_PROCESSORS" };

            foreach (var varName in commonSafeVars)
            {
                var value = Environment.GetEnvironmentVariable(varName);
                if (value != null)
                {
                    safeVars[varName] = value;
                }
            }

            envInfo["safe_environment_variables"] = safeVars;
        }

        return envInfo;
    }

    private static async Task<object> GatherNetworkInfoAsync(bool detailed, ToolExecutionContext context)
    {
        var networkInfo = new Dictionary<string, object?>();

        try
        {
            var interfaces = SystemNet.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            var interfaceList = new List<object>();

            foreach (var iface in interfaces)
            {
                var ifaceInfo = new Dictionary<string, object?>
                {
                    ["name"] = iface.Name,
                    ["description"] = iface.Description,
                    ["type"] = iface.NetworkInterfaceType.ToString(),
                    ["operational_status"] = iface.OperationalStatus.ToString(),
                    ["speed"] = iface.Speed >= 0 && iface.Speed < long.MaxValue ? iface.Speed : -1,
                    ["supports_multicast"] = iface.SupportsMulticast
                };

                if (detailed)
                {
                    try
                    {
                        var properties = iface.GetIPProperties();
                        var addresses = properties.UnicastAddresses.Select(addr => addr.Address.ToString()).ToList();
                        ifaceInfo["ip_addresses"] = addresses;
                        ifaceInfo["dns_addresses"] = properties.DnsAddresses.Select(dns => dns.ToString()).ToList();
                        ifaceInfo["gateway_addresses"] = properties.GatewayAddresses.Select(gw => gw.Address.ToString()).ToList();
                    }
                    catch (Exception ex)
                    {
                        ifaceInfo["details_error"] = ex.Message;
                    }
                }

                interfaceList.Add(ifaceInfo);
            }

            networkInfo["interfaces"] = interfaceList;
            networkInfo["interface_count"] = interfaceList.Count;
        }
        catch (Exception ex)
        {
            networkInfo["error"] = ex.Message;
        }

        await Task.CompletedTask;
        return networkInfo;
    }

    private static async Task<object> GatherStorageInfoAsync(bool detailed, ToolExecutionContext context)
    {
        var storageInfo = new Dictionary<string, object?>();

        try
        {
            var drives = DriveInfo.GetDrives();
            var driveList = new List<object>();

            foreach (var drive in drives)
            {
                try
                {
                    var driveInfo = new Dictionary<string, object?>
                    {
                        ["name"] = drive.Name,
                        ["drive_type"] = drive.DriveType.ToString(),
                        ["is_ready"] = drive.IsReady
                    };

                    if (drive.IsReady)
                    {
                        driveInfo["total_size"] = drive.TotalSize;
                        driveInfo["total_size_formatted"] = ToolHelpers.FormatFileSize(drive.TotalSize);
                        driveInfo["available_free_space"] = drive.AvailableFreeSpace;
                        driveInfo["available_free_space_formatted"] = ToolHelpers.FormatFileSize(drive.AvailableFreeSpace);
                        driveInfo["total_free_space"] = drive.TotalFreeSpace;
                        driveInfo["used_space"] = drive.TotalSize - drive.TotalFreeSpace;
                        driveInfo["used_space_formatted"] = ToolHelpers.FormatFileSize(drive.TotalSize - drive.TotalFreeSpace);
                        driveInfo["usage_percentage"] = drive.TotalSize > 0
                            ? Math.Round((double)(drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize * 100, 2)
                            : 0;

                        if (detailed)
                        {
                            driveInfo["drive_format"] = drive.DriveFormat;
                            driveInfo["volume_label"] = drive.VolumeLabel;
                            driveInfo["root_directory"] = drive.RootDirectory.FullName;
                        }
                    }

                    driveList.Add(driveInfo);
                }
                catch (Exception ex)
                {
                    driveList.Add(new Dictionary<string, object?>
                    {
                        ["name"] = drive.Name,
                        ["error"] = ex.Message
                    });
                }
            }

            storageInfo["drives"] = driveList;
            storageInfo["drive_count"] = driveList.Count;
        }
        catch (Exception ex)
        {
            storageInfo["error"] = ex.Message;
        }

        await Task.CompletedTask;
        return storageInfo;
    }

    private static async Task<object> GatherMemoryInfoAsync(bool detailed, ToolExecutionContext context)
    {
        await Task.CompletedTask;

        var memoryInfo = new Dictionary<string, object?>
        {
            ["working_set"] = Environment.WorkingSet,
            ["working_set_formatted"] = ToolHelpers.FormatFileSize(Environment.WorkingSet),
            ["gc_total_memory"] = GC.GetTotalMemory(false),
            ["gc_total_memory_formatted"] = ToolHelpers.FormatFileSize(GC.GetTotalMemory(false))
        };

        if (detailed)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                memoryInfo["virtual_memory_size"] = currentProcess.VirtualMemorySize64;
                memoryInfo["virtual_memory_size_formatted"] = ToolHelpers.FormatFileSize(currentProcess.VirtualMemorySize64);
                memoryInfo["paged_memory_size"] = currentProcess.PagedMemorySize64;
                memoryInfo["paged_memory_size_formatted"] = ToolHelpers.FormatFileSize(currentProcess.PagedMemorySize64);
                memoryInfo["nonpaged_system_memory_size"] = currentProcess.NonpagedSystemMemorySize64;
                memoryInfo["paged_system_memory_size"] = currentProcess.PagedSystemMemorySize64;
                memoryInfo["peak_working_set"] = currentProcess.PeakWorkingSet64;
                memoryInfo["peak_virtual_memory_size"] = currentProcess.PeakVirtualMemorySize64;
                memoryInfo["peak_paged_memory_size"] = currentProcess.PeakPagedMemorySize64;

                // GC detailed info
                memoryInfo["gc_max_generation"] = GC.MaxGeneration;
                for (int i = 0; i <= GC.MaxGeneration; i++)
                {
                    memoryInfo[$"gc_gen{i}_collections"] = GC.CollectionCount(i);
                }
            }
            catch (Exception ex)
            {
                memoryInfo["detailed_info_error"] = ex.Message;
            }
        }

        return memoryInfo;
    }

    private static async Task<object> GatherCPUInfoAsync(bool detailed, ToolExecutionContext context)
    {
        await Task.CompletedTask;

        var cpuInfo = new Dictionary<string, object?>
        {
            ["processor_count"] = Environment.ProcessorCount,
            ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString()
        };

        if (detailed)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                cpuInfo["total_processor_time"] = currentProcess.TotalProcessorTime.TotalMilliseconds;
                cpuInfo["user_processor_time"] = currentProcess.UserProcessorTime.TotalMilliseconds;
                cpuInfo["privileged_processor_time"] = currentProcess.PrivilegedProcessorTime.TotalMilliseconds;

                // PerformanceCounter is not available in .NET Core/5+
                // CPU usage would require platform-specific implementation or a NuGet package
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    cpuInfo["cpu_usage_note"] = "CPU usage monitoring requires additional packages in .NET Core/5+";
                }
            }
            catch (Exception ex)
            {
                cpuInfo["detailed_info_error"] = ex.Message;
            }
        }

        return cpuInfo;
    }

    private static bool IsSensitiveEnvironmentVariable(string name)
    {
        var sensitivePatterns = new[]
        {
            "PASSWORD", "SECRET", "KEY", "TOKEN", "CREDENTIAL", "AUTH",
            "API_KEY", "PRIVATE", "CERT", "SSL", "TLS", "OAUTH"
        };

        return sensitivePatterns.Any(pattern =>
            name.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string>? GetParameterAsStringList(Dictionary<string, object?> parameters, string name)
    {
        if (!parameters.TryGetValue(name, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            List<string> list => list,
            string[] array => array.ToList(),
            IEnumerable<string> enumerable => enumerable.ToList(),
            _ => null
        };
    }
}
