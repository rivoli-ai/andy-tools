using System.Diagnostics;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.System;

/// <summary>
/// Tool for retrieving process information.
/// </summary>
public class ProcessInfoTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "process_info",
        Name = "Process Information",
        Description = "Retrieves information about running processes on the system",
        Version = "1.0.0",
        Category = ToolCategory.System,
        RequiredPermissions = ToolPermissionFlags.SystemInformation,
        Parameters =
        [
            new()
            {
                Name = "process_name",
                Description = "Name of specific process to get info for (optional)",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "process_id",
                Description = "ID of specific process to get info for (optional)",
                Type = "integer",
                Required = false
            },
            new()
            {
                Name = "include_current",
                Description = "Whether to include current process information (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "include_system",
                Description = "Whether to include system processes (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            },
            new()
            {
                Name = "sort_by",
                Description = "How to sort the process list",
                Type = "string",
                Required = false,
                DefaultValue = "name",
                AllowedValues = ["name", "id", "memory", "cpu_time", "start_time"]
            },
            new()
            {
                Name = "max_results",
                Description = "Maximum number of processes to return (default: 50)",
                Type = "integer",
                Required = false,
                DefaultValue = 50,
                MinValue = 1,
                MaxValue = 500
            },
            new()
            {
                Name = "detailed",
                Description = "Whether to include detailed process information (default: false)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var processName = GetParameter<string>(parameters, "process_name");
        var processId = GetParameter<int?>(parameters, "process_id");
        var includeCurrent = GetParameter(parameters, "include_current", true);
        var includeSystem = GetParameter(parameters, "include_system", false);
        var sortBy = GetParameter(parameters, "sort_by", "name");
        var maxResults = GetParameter(parameters, "max_results", 50);
        var detailed = GetParameter(parameters, "detailed", false);

        try
        {
            ReportProgress(context, "Retrieving process information...", 10);

            var processes = new List<ProcessInfo>();

            if (processId.HasValue)
            {
                // Get specific process by ID
                var process = await GetProcessByIdAsync(processId.Value, detailed, context);
                if (process != null)
                {
                    processes.Add(process);
                }
            }
            else if (!string.IsNullOrEmpty(processName))
            {
                // Get processes by name
                var processesArray = await GetProcessesByNameAsync(processName, detailed, context);
                processes.AddRange(processesArray);
            }
            else
            {
                // Get all processes
                var allProcesses = await GetAllProcessesAsync(includeCurrent, includeSystem, detailed, context);
                processes.AddRange(allProcesses);
            }

            ReportProgress(context, "Sorting and filtering results...", 70);

            // Sort processes
            processes = SortProcesses(processes, sortBy);

            // Limit results
            var limitedProcesses = processes.Take(maxResults).ToList();

            ReportProgress(context, "Process information retrieved", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["total_processes_found"] = processes.Count,
                ["processes_returned"] = limitedProcesses.Count,
                ["sort_by"] = sortBy,
                ["include_current"] = includeCurrent,
                ["include_system"] = includeSystem,
                ["detailed"] = detailed,
                ["retrieved_at"] = DateTime.UtcNow
            };

            if (!string.IsNullOrEmpty(processName))
            {
                metadata["process_name_filter"] = processName;
            }

            if (processId.HasValue)
            {
                metadata["process_id_filter"] = processId.Value;
            }

            var message = processId.HasValue || !string.IsNullOrEmpty(processName)
                ? $"Found {limitedProcesses.Count} matching processes"
                : $"Retrieved {limitedProcesses.Count} processes (of {processes.Count} total)";

            return ToolResults.ListSuccess(
                limitedProcesses,
                message,
                processes.Count
            );
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Failed to retrieve process information: {ex.Message}", "PROCESS_INFO_ERROR", details: ex);
        }
    }

    private async Task<ProcessInfo?> GetProcessByIdAsync(int processId, bool detailed, ToolExecutionContext context)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return await CreateProcessInfoAsync(process, detailed, context);
        }
        catch (ArgumentException)
        {
            // Process not found
            return null;
        }
        catch (Exception)
        {
            // Access denied or other error
            return null;
        }
    }

    private async Task<List<ProcessInfo>> GetProcessesByNameAsync(string processName, bool detailed, ToolExecutionContext context)
    {
        var processes = new List<ProcessInfo>();

        try
        {
            var processArray = Process.GetProcessesByName(processName);

            foreach (var process in processArray)
            {
                try
                {
                    var processInfo = await CreateProcessInfoAsync(process, detailed, context);
                    if (processInfo != null)
                    {
                        processes.Add(processInfo);
                    }
                }
                catch (Exception)
                {
                    // Skip processes we can't access
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception)
        {
            // Handle cases where we can't enumerate processes
        }

        return processes;
    }

    private async Task<List<ProcessInfo>> GetAllProcessesAsync(bool includeCurrent, bool includeSystem, bool detailed, ToolExecutionContext context)
    {
        var processes = new List<ProcessInfo>();
        var currentProcessId = Environment.ProcessId;

        try
        {
            var allProcesses = Process.GetProcesses();
            var processedCount = 0;
            var totalCount = allProcesses.Length;

            foreach (var process in allProcesses)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Skip current process if not requested
                    if (!includeCurrent && process.Id == currentProcessId)
                    {
                        continue;
                    }

                    // Skip system processes if not requested
                    if (!includeSystem && IsSystemProcess(process))
                    {
                        continue;
                    }

                    var processInfo = await CreateProcessInfoAsync(process, detailed, context);
                    if (processInfo != null)
                    {
                        processes.Add(processInfo);
                    }

                    processedCount++;
                    if (processedCount % 10 == 0)
                    {
                        var progressPercent = 10 + (processedCount * 60 / totalCount);
                        ReportProgress(context, $"Processed {processedCount}/{totalCount} processes", progressPercent);
                    }
                }
                catch (Exception)
                {
                    // Skip processes we can't access
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception)
        {
            // Handle cases where we can't enumerate processes
        }

        return processes;
    }

    private static async Task<ProcessInfo?> CreateProcessInfoAsync(Process process, bool detailed, ToolExecutionContext context)
    {
        try
        {
            var processInfo = new ProcessInfo
            {
                Id = process.Id,
                Name = process.ProcessName
            };

            // Basic information that's usually accessible
            try
            {
                processInfo.StartTime = process.StartTime;
                processInfo.HasExited = process.HasExited;
            }
            catch (Exception)
            {
                // Some processes don't allow access to start time
            }

            if (detailed)
            {
                // Detailed information that might require elevated permissions
                try
                {
                    processInfo.MainWindowTitle = process.MainWindowTitle;
                    processInfo.WorkingSet = process.WorkingSet64;
                    processInfo.VirtualMemorySize = process.VirtualMemorySize64;
                    processInfo.PagedMemorySize = process.PagedMemorySize64;
                    processInfo.TotalProcessorTime = process.TotalProcessorTime;
                    processInfo.UserProcessorTime = process.UserProcessorTime;
                    processInfo.PrivilegedProcessorTime = process.PrivilegedProcessorTime;
                    processInfo.ThreadCount = process.Threads.Count;
                    processInfo.HandleCount = process.HandleCount;
                    processInfo.BasePriority = process.BasePriority;
                    processInfo.Responding = process.Responding;

                    if (process.MainModule != null)
                    {
                        processInfo.MainModulePath = process.MainModule.FileName;
                        processInfo.MainModuleVersion = process.MainModule.FileVersionInfo.FileVersion;
                    }
                }
                catch (Exception ex)
                {
                    processInfo.AccessError = ex.Message;
                }
            }

            await Task.CompletedTask;
            return processInfo;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsSystemProcess(Process process)
    {
        try
        {
            // System processes typically have these characteristics
            if (process.SessionId == 0 && process.Id <= 4)
            {
                return true;
            }

            // Common system process names
            var systemProcessNames = new[]
            {
                "System", "Registry", "smss", "csrss", "wininit", "winlogon",
                "services", "lsass", "lsm", "svchost", "dwm", "explorer"
            };

            return systemProcessNames.Any(name =>
                string.Equals(process.ProcessName, name, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static List<ProcessInfo> SortProcesses(List<ProcessInfo> processes, string sortBy)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "id" => [.. processes.OrderBy(p => p.Id)],
            "memory" => [.. processes.OrderByDescending(p => p.WorkingSet ?? 0)],
            "cpu_time" => [.. processes.OrderByDescending(p => p.TotalProcessorTime?.TotalMilliseconds ?? 0)],
            "start_time" => [.. processes.OrderByDescending(p => p.StartTime ?? DateTime.MinValue)],
            _ => [.. processes.OrderBy(p => p.Name)] // name
        };
    }

    private class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime? StartTime { get; set; }
        public bool HasExited { get; set; }
        public string? MainWindowTitle { get; set; }
        public long? WorkingSet { get; set; }
        public long? VirtualMemorySize { get; set; }
        public long? PagedMemorySize { get; set; }
        public TimeSpan? TotalProcessorTime { get; set; }
        public TimeSpan? UserProcessorTime { get; set; }
        public TimeSpan? PrivilegedProcessorTime { get; set; }
        public int? ThreadCount { get; set; }
        public int? HandleCount { get; set; }
        public int? BasePriority { get; set; }
        public bool? Responding { get; set; }
        public string? MainModulePath { get; set; }
        public string? MainModuleVersion { get; set; }
        public string? AccessError { get; set; }
    }
}
