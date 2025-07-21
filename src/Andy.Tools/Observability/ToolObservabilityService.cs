using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andy.Tools.Observability;

/// <summary>
/// Default implementation of tool observability service.
/// </summary>
public class ToolObservabilityService : IToolObservabilityService, IDisposable
{
    private readonly ILogger<ToolObservabilityService> _logger;
    private readonly ToolObservabilityOptions _options;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    private readonly ConcurrentDictionary<string, ToolExecutionRecord> _executionRecords = new();
    private readonly ConcurrentQueue<SecurityEvent> _securityEvents = new();
    private readonly Timer _metricsAggregationTimer;

    // Metrics instruments
    private readonly Counter<long> _executionCounter;
    private readonly Histogram<double> _executionDuration;
    private readonly Counter<long> _errorCounter;
    private readonly ObservableGauge<int> _activeExecutions;
    private readonly Histogram<long> _memoryUsage;
    private readonly Histogram<double> _cpuUsage;

    private int _activeExecutionCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolObservabilityService"/> class.
    /// </summary>
    /// <param name="options">The observability options.</param>
    /// <param name="logger">The logger instance.</param>
    public ToolObservabilityService(
        IOptions<ToolObservabilityOptions> options,
        ILogger<ToolObservabilityService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Initialize telemetry
        _activitySource = new ActivitySource("Andy.Tools", "1.0.0");
        _meter = new Meter("Andy.Tools", "1.0.0");

        // Create metrics instruments
        _executionCounter = _meter.CreateCounter<long>(
            "tool_executions_total",
            description: "Total number of tool executions");

        _executionDuration = _meter.CreateHistogram<double>(
            "tool_execution_duration_ms",
            unit: "ms",
            description: "Tool execution duration in milliseconds");

        _errorCounter = _meter.CreateCounter<long>(
            "tool_errors_total",
            description: "Total number of tool execution errors");

        _activeExecutions = _meter.CreateObservableGauge(
            "tool_active_executions",
            () => _activeExecutionCount,
            description: "Number of currently active tool executions");

        _memoryUsage = _meter.CreateHistogram<long>(
            "tool_memory_usage_bytes",
            unit: "bytes",
            description: "Memory usage during tool execution");

        _cpuUsage = _meter.CreateHistogram<double>(
            "tool_cpu_usage_percent",
            unit: "%",
            description: "CPU usage percentage during tool execution");

        // Start metrics aggregation timer
        _metricsAggregationTimer = new Timer(
            AggregateMetrics,
            null,
            _options.MetricsAggregationInterval,
            _options.MetricsAggregationInterval);

        _logger.LogInformation("Tool observability service initialized");
    }

    /// <inheritdoc />
    public Activity? StartToolExecution(string toolId, Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        Interlocked.Increment(ref _activeExecutionCount);

        var activity = _activitySource.StartActivity(
            $"ToolExecution.{toolId}",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("tool.id", toolId);
            activity.SetTag("tool.user", context.UserId);
            activity.SetTag("tool.session", context.SessionId);

            // Add parameter tags (limit to avoid too many tags)
            var paramCount = 0;
            foreach (var param in parameters.Take(10))
            {
                if (param.Value != null && IsSimpleType(param.Value.GetType()))
                {
                    activity.SetTag($"tool.param.{param.Key}", param.Value.ToString());
                    paramCount++;
                }
            }

            if (parameters.Count > paramCount)
            {
                activity.SetTag("tool.param.truncated", true);
            }

            // Log execution start
            _logger.LogDebug(
                "Started tool execution: {ToolId} by {User} in session {SessionId}",
                toolId, context.UserId, context.SessionId);
        }

        // Record execution start
        var record = new ToolExecutionRecord
        {
            ToolId = toolId,
            StartTime = DateTimeOffset.UtcNow,
            User = context.UserId ?? string.Empty,
            SessionId = context.SessionId ?? string.Empty,
            Parameters = parameters
        };

        _executionRecords[activity?.Id ?? Guid.NewGuid().ToString()] = record;

        return activity;
    }

    /// <inheritdoc />
    public void CompleteToolExecution(Activity? activity, ToolExecutionResult result)
    {
        Interlocked.Decrement(ref _activeExecutionCount);

        if (activity != null)
        {
            activity.SetStatus(
                result.IsSuccessful ? ActivityStatusCode.Ok : ActivityStatusCode.Error,
                result.ErrorMessage);

            if (result.ResourceUsage != null)
            {
                activity.SetTag("tool.memory_usage", result.ResourceUsage.PeakMemoryBytes);
                activity.SetTag("tool.cpu_time_ms", result.ResourceUsage.CpuTimeMs);
                activity.SetTag("tool.file_operations", result.ResourceUsage.FilesAccessed);
                activity.SetTag("tool.network_operations", result.ResourceUsage.NetworkRequests);
            }

            activity.Stop();
        }

        // Update execution record
        var recordKey = activity?.Id ?? string.Empty;
        if (_executionRecords.TryGetValue(recordKey, out var record))
        {
            record.EndTime = DateTimeOffset.UtcNow;
            record.Success = result.IsSuccessful;
            record.ErrorMessage = result.ErrorMessage;
            record.ResourceUsage = result.ResourceUsage;
            record.Duration = record.EndTime.Value - record.StartTime;

            // Record metrics
            var tags = new TagList
            {
                { "tool_id", record.ToolId },
                { "success", result.IsSuccessful.ToString() }
            };

            _executionCounter.Add(1, tags);
            _executionDuration.Record(record.Duration.Value.TotalMilliseconds, tags);

            if (result.ResourceUsage != null)
            {
                _memoryUsage.Record(result.ResourceUsage.PeakMemoryBytes, tags);
                if (result.ResourceUsage.CpuTimeMs > 0 && record.Duration.Value.TotalMilliseconds > 0)
                {
                    var cpuPercent = (result.ResourceUsage.CpuTimeMs / record.Duration.Value.TotalMilliseconds) * 100;
                    _cpuUsage.Record(cpuPercent, tags);
                }
            }

            if (!result.IsSuccessful)
            {
                _errorCounter.Add(1, tags);
            }

            _logger.LogDebug(
                "Completed tool execution: {ToolId} in {Duration}ms - Success: {Success}",
                record.ToolId, record.Duration.Value.TotalMilliseconds, result.IsSuccessful);
        }
    }

    /// <inheritdoc />
    public void RecordToolError(Activity? activity, string toolId, Exception exception)
    {
        if (activity != null)
        {
            activity.SetTag("error", true);
            activity.SetTag("error.type", exception.GetType().Name);
            activity.SetTag("error.message", exception.Message);
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        }

        var tags = new TagList
        {
            { "tool_id", toolId },
            { "error_type", exception.GetType().Name }
        };

        _errorCounter.Add(1, tags);

        _logger.LogError(exception, "Tool execution error: {ToolId}", toolId);
    }

    /// <inheritdoc />
    public void RecordToolUsage(string toolId, TimeSpan duration, bool success, ToolResourceUsage? resourceUsage = null)
    {
        var tags = new TagList
        {
            { "tool_id", toolId },
            { "success", success.ToString() }
        };

        _executionCounter.Add(1, tags);
        _executionDuration.Record(duration.TotalMilliseconds, tags);

        if (resourceUsage != null)
        {
            _memoryUsage.Record(resourceUsage.PeakMemoryBytes, tags);
            if (resourceUsage.CpuTimeMs > 0 && duration.TotalMilliseconds > 0)
            {
                var cpuPercent = (resourceUsage.CpuTimeMs / duration.TotalMilliseconds) * 100;
                _cpuUsage.Record(cpuPercent, tags);
            }
        }

        if (!success)
        {
            _errorCounter.Add(1, tags);
        }
    }

    /// <inheritdoc />
    public void RecordSecurityEvent(string toolId, string eventType, Dictionary<string, object?> details)
    {
        var securityEvent = new SecurityEvent
        {
            ToolId = toolId,
            EventType = eventType,
            Details = details,
            Timestamp = DateTimeOffset.UtcNow
        };

        _securityEvents.Enqueue(securityEvent);

        // Trim old events if queue is too large
        while (_securityEvents.Count > _options.MaxSecurityEvents)
        {
            _securityEvents.TryDequeue(out _);
        }

        _logger.LogWarning(
            "Security event recorded: {EventType} for tool {ToolId}",
            eventType, toolId);
    }

    /// <inheritdoc />
    public async Task<ToolPerformanceStatistics> GetPerformanceStatisticsAsync(string? toolId = null, TimeSpan? timeRange = null)
    {
        var endTime = DateTimeOffset.UtcNow;
        var startTime = timeRange.HasValue ? endTime - timeRange.Value : endTime - TimeSpan.FromHours(24);

        var relevantRecords = _executionRecords.Values
            .Where(r => r.EndTime.HasValue &&
                       r.StartTime >= startTime &&
                       r.EndTime <= endTime &&
                       (toolId == null || r.ToolId == toolId))
            .ToList();

        if (relevantRecords.Count == 0)
        {
            return new ToolPerformanceStatistics
            {
                ToolId = toolId,
                StartTime = startTime,
                EndTime = endTime
            };
        }

        var durations = relevantRecords
            .Where(r => r.Duration.HasValue)
            .Select(r => r.Duration!.Value.TotalMilliseconds)
            .OrderBy(d => d)
            .ToList();

        var stats = new ToolPerformanceStatistics
        {
            ToolId = toolId,
            ExecutionCount = relevantRecords.Count,
            SuccessCount = relevantRecords.Count(r => r.Success),
            FailureCount = relevantRecords.Count(r => !r.Success),
            StartTime = startTime,
            EndTime = endTime
        };

        if (durations.Count > 0)
        {
            stats.AverageExecutionTime = TimeSpan.FromMilliseconds(durations.Average());
            stats.MinExecutionTime = TimeSpan.FromMilliseconds(durations.Min());
            stats.MaxExecutionTime = TimeSpan.FromMilliseconds(durations.Max());
            stats.P50ExecutionTime = TimeSpan.FromMilliseconds(GetPercentile(durations, 50));
            stats.P90ExecutionTime = TimeSpan.FromMilliseconds(GetPercentile(durations, 90));
            stats.P99ExecutionTime = TimeSpan.FromMilliseconds(GetPercentile(durations, 99));
        }

        // Calculate resource usage statistics
        var recordsWithResourceUsage = relevantRecords.Where(r => r.ResourceUsage != null).ToList();
        if (recordsWithResourceUsage.Count > 0)
        {
            stats.ResourceUsage = new ResourceUsageStatistics
            {
                AverageMemoryUsage = (long)recordsWithResourceUsage.Average(r => r.ResourceUsage!.PeakMemoryBytes),
                PeakMemoryUsage = recordsWithResourceUsage.Max(r => r.ResourceUsage!.PeakMemoryBytes),
                AverageCpuUsage = recordsWithResourceUsage.Average(r => CalculateCpuUsage(r)),
                PeakCpuUsage = recordsWithResourceUsage.Max(r => CalculateCpuUsage(r)),
                FileSystemOperations = recordsWithResourceUsage.Sum(r => r.ResourceUsage!.FilesAccessed),
                NetworkOperations = recordsWithResourceUsage.Sum(r => r.ResourceUsage!.NetworkRequests)
            };
        }

        // Error distribution
        var errors = relevantRecords
            .Where(r => !r.Success && !string.IsNullOrEmpty(r.ErrorMessage))
            .GroupBy(r => GetErrorCategory(r.ErrorMessage!))
            .ToDictionary(g => g.Key, g => (long)g.Count());

        stats.ErrorDistribution = errors;

        await Task.CompletedTask;
        return stats;
    }

    /// <inheritdoc />
    public async Task<ToolUsageAnalytics> GetUsageAnalyticsAsync(TimeSpan? timeRange = null)
    {
        var endTime = DateTimeOffset.UtcNow;
        var startTime = timeRange.HasValue ? endTime - timeRange.Value : endTime - TimeSpan.FromDays(7);

        var relevantRecords = _executionRecords.Values
            .Where(r => r.StartTime >= startTime && r.StartTime <= endTime)
            .ToList();

        var analytics = new ToolUsageAnalytics
        {
            StartTime = startTime,
            EndTime = endTime
        };

        // Tool usage by ID
        var toolGroups = relevantRecords.GroupBy(r => r.ToolId);
        foreach (var group in toolGroups)
        {
            var usageInfo = new ToolUsageInfo
            {
                ToolId = group.Key,
                ExecutionCount = group.Count(),
                UniqueUsers = group.Select(r => r.User).Distinct().Count()
            };

            // Calculate common parameters
            var allParams = group
                .SelectMany(r => r.Parameters)
                .GroupBy(p => p.Key)
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .OrderByDescending(p => p.Count)
                .Take(5);

            foreach (var param in allParams)
            {
                usageInfo.CommonParameters[param.Key] = param.Count;
            }

            analytics.ToolUsage[group.Key] = usageInfo;
        }

        // Find peak usage times (hourly buckets)
        var hourlyBuckets = relevantRecords
            .GroupBy(r => new DateTime(r.StartTime.Year, r.StartTime.Month, r.StartTime.Day, r.StartTime.Hour, 0, 0))
            .Select(g => new PeakUsageTime
            {
                Time = new DateTimeOffset(g.Key, TimeSpan.Zero),
                ExecutionCount = g.Count(),
                ConcurrentExecutions = CalculateMaxConcurrent(g.ToList())
            })
            .OrderByDescending(p => p.ExecutionCount)
            .Take(10)
            .ToList();

        analytics.PeakUsageTimes = hourlyBuckets;

        // Find frequent tool combinations (tools used within 5 minutes of each other)
        var combinations = FindFrequentCombinations(relevantRecords, TimeSpan.FromMinutes(5));
        analytics.FrequentCombinations = combinations;

        await Task.CompletedTask;
        return analytics;
    }

    /// <inheritdoc />
    public async Task<string> ExportObservabilityDataAsync(string format, ExportOptions? options = null)
    {
        options ??= new ExportOptions();

        var exportData = new ObservabilityExportData
        {
            ExportTime = DateTimeOffset.UtcNow,
            StartTime = options.StartTime,
            EndTime = options.EndTime
        };

        // Filter execution records
        var records = _executionRecords.Values.AsEnumerable();

        if (options.StartTime.HasValue)
        {
            records = records.Where(r => r.StartTime >= options.StartTime.Value);
        }

        if (options.EndTime.HasValue)
        {
            records = records.Where(r => r.EndTime <= options.EndTime.Value);
        }

        if (options.ToolIds?.Count > 0)
        {
            records = records.Where(r => options.ToolIds.Contains(r.ToolId));
        }

        var recordsList = records.ToList();

        if (options.IncludeRawData)
        {
            exportData.ExecutionRecords = recordsList;
        }

        if (options.IncludeStatistics)
        {
            var stats = await GetPerformanceStatisticsAsync(
                null,
                options.EndTime - options.StartTime);
            exportData.PerformanceStatistics = stats;

            var analytics = await GetUsageAnalyticsAsync(
                options.EndTime - options.StartTime);
            exportData.UsageAnalytics = analytics;
        }

        if (options.IncludeSecurityEvents)
        {
            exportData.SecurityEvents = _securityEvents
                .Where(e => (!options.StartTime.HasValue || e.Timestamp >= options.StartTime.Value) &&
                           (!options.EndTime.HasValue || e.Timestamp <= options.EndTime.Value) &&
                           (options.ToolIds == null || options.ToolIds.Contains(e.ToolId)))
                .ToList();
        }

        return format.ToLowerInvariant() switch
        {
            "json" => JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true }),
            "csv" => ExportToCsv(exportData),
            _ => throw new NotSupportedException($"Export format '{format}' is not supported")
        };
    }

    private void AggregateMetrics(object? state)
    {
        try
        {
            // Clean up old execution records
            var cutoffTime = DateTimeOffset.UtcNow - _options.RetentionPeriod;
            var keysToRemove = _executionRecords
                .Where(kvp => kvp.Value.EndTime < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _executionRecords.TryRemove(key, out _);
            }

            _logger.LogDebug("Aggregated metrics and cleaned up {Count} old records", keysToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during metrics aggregation");
        }
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(Guid);
    }

    private static double GetPercentile(List<double> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        var index = (percentile / 100.0) * (sortedValues.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper)
        {
            return sortedValues[lower];
        }

        var weight = index - lower;
        return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
    }

    private static double CalculateCpuUsage(ToolExecutionRecord record)
    {
        if (record.ResourceUsage == null || !record.Duration.HasValue || record.Duration.Value.TotalMilliseconds <= 0)
        {
            return 0;
        }

        return (record.ResourceUsage.CpuTimeMs / record.Duration.Value.TotalMilliseconds) * 100;
    }

    private static string GetErrorCategory(string errorMessage)
    {
        if (errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "Timeout";
        }

        if (errorMessage.Contains("permission", StringComparison.OrdinalIgnoreCase))
        {
            return "Permission";
        }

        if (errorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return "NotFound";
        }

        if (errorMessage.Contains("validation", StringComparison.OrdinalIgnoreCase))
        {
            return "Validation";
        }

        return "Other";
    }

    private static int CalculateMaxConcurrent(List<ToolExecutionRecord> records)
    {
        if (records.Count == 0)
        {
            return 0;
        }

        var events = new List<(DateTimeOffset time, int delta)>();

        foreach (var record in records)
        {
            events.Add((record.StartTime, 1));
            if (record.EndTime.HasValue)
            {
                events.Add((record.EndTime.Value, -1));
            }
        }

        events.Sort((a, b) => a.time.CompareTo(b.time));

        var maxConcurrent = 0;
        var currentConcurrent = 0;

        foreach (var evt in events)
        {
            currentConcurrent += evt.delta;
            maxConcurrent = Math.Max(maxConcurrent, currentConcurrent);
        }

        return maxConcurrent;
    }

    private static List<ToolCombination> FindFrequentCombinations(
        List<ToolExecutionRecord> records,
        TimeSpan windowSize)
    {
        var combinations = new Dictionary<string, int>();
        var sortedRecords = records.OrderBy(r => r.StartTime).ToList();

        for (int i = 0; i < sortedRecords.Count; i++)
        {
            var currentRecord = sortedRecords[i];
            var relatedTools = new HashSet<string> { currentRecord.ToolId };

            // Look for tools executed within the window
            for (int j = i + 1; j < sortedRecords.Count; j++)
            {
                var nextRecord = sortedRecords[j];
                if (nextRecord.StartTime - currentRecord.StartTime > windowSize)
                {
                    break;
                }

                relatedTools.Add(nextRecord.ToolId);
            }

            if (relatedTools.Count > 1)
            {
                var key = string.Join(",", relatedTools.OrderBy(t => t));
                combinations.TryGetValue(key, out var count);
                combinations[key] = count + 1;
            }
        }

        return combinations
            .Where(kvp => kvp.Value >= 3) // At least 3 occurrences
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .Select(kvp => new ToolCombination
            {
                ToolIds = kvp.Key.Split(',').ToList(),
                Frequency = kvp.Value
            })
            .ToList();
    }

    private static string ExportToCsv(ObservabilityExportData data)
    {
        var sb = new StringBuilder();

        // Export execution records
        if (data.ExecutionRecords?.Count > 0)
        {
            sb.AppendLine("Tool Executions");
            sb.AppendLine("ToolId,User,StartTime,EndTime,Duration(ms),Success,ErrorMessage,MemoryUsed,CpuTime(ms)");

            foreach (var record in data.ExecutionRecords)
            {
                sb.AppendLine($"{record.ToolId},{record.User},{record.StartTime:O}," +
                             $"{record.EndTime:O},{record.Duration?.TotalMilliseconds}," +
                             $"{record.Success},{EscapeCsv(record.ErrorMessage)}," +
                             $"{record.ResourceUsage?.PeakMemoryBytes},{record.ResourceUsage?.CpuTimeMs}");
            }

            sb.AppendLine();
        }

        // Export statistics
        if (data.PerformanceStatistics != null)
        {
            var stats = data.PerformanceStatistics;
            sb.AppendLine("Performance Statistics");
            sb.AppendLine($"Total Executions,{stats.ExecutionCount}");
            sb.AppendLine($"Success Count,{stats.SuccessCount}");
            sb.AppendLine($"Failure Count,{stats.FailureCount}");
            sb.AppendLine($"Success Rate,{stats.SuccessRate:P2}");
            sb.AppendLine($"Average Duration (ms),{stats.AverageExecutionTime.TotalMilliseconds}");
            sb.AppendLine($"P50 Duration (ms),{stats.P50ExecutionTime.TotalMilliseconds}");
            sb.AppendLine($"P90 Duration (ms),{stats.P90ExecutionTime.TotalMilliseconds}");
            sb.AppendLine($"P99 Duration (ms),{stats.P99ExecutionTime.TotalMilliseconds}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _metricsAggregationTimer?.Dispose();
        _activitySource?.Dispose();
        _meter?.Dispose();
    }

    private class ToolExecutionRecord
    {
        public string ToolId { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object?> Parameters { get; set; } = new();
        public ToolResourceUsage? ResourceUsage { get; set; }
    }

    private class SecurityEvent
    {
        public string ToolId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public Dictionary<string, object?> Details { get; set; } = new();
        public DateTimeOffset Timestamp { get; set; }
    }

    private class ObservabilityExportData
    {
        public DateTimeOffset ExportTime { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public List<ToolExecutionRecord>? ExecutionRecords { get; set; }
        public ToolPerformanceStatistics? PerformanceStatistics { get; set; }
        public ToolUsageAnalytics? UsageAnalytics { get; set; }
        public List<SecurityEvent>? SecurityEvents { get; set; }
    }
}

/// <summary>
/// Options for tool observability service.
/// </summary>
public class ToolObservabilityOptions
{
    /// <summary>
    /// Gets or sets the metrics aggregation interval.
    /// </summary>
    public TimeSpan MetricsAggregationInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the retention period for execution records.
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the maximum number of security events to retain.
    /// </summary>
    public int MaxSecurityEvents { get; set; } = 10000;

    /// <summary>
    /// Gets or sets whether to enable detailed activity tracing.
    /// </summary>
    public bool EnableDetailedTracing { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to export metrics to external systems.
    /// </summary>
    public bool EnableMetricsExport { get; set; } = true;
}
