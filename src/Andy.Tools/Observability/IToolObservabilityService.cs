using System.Diagnostics;
using Andy.Tools.Core;

namespace Andy.Tools.Observability;

/// <summary>
/// Provides observability services for tool execution including metrics, tracing, and logging.
/// </summary>
public interface IToolObservabilityService
{
    /// <summary>
    /// Records the start of a tool execution.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="parameters">The execution parameters.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>An activity representing the tool execution.</returns>
    public Activity? StartToolExecution(string toolId, Dictionary<string, object?> parameters, ToolExecutionContext context);

    /// <summary>
    /// Records the completion of a tool execution.
    /// </summary>
    /// <param name="activity">The activity to complete.</param>
    /// <param name="result">The execution result.</param>
    public void CompleteToolExecution(Activity? activity, ToolExecutionResult result);

    /// <summary>
    /// Records a tool execution error.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="exception">The exception.</param>
    public void RecordToolError(Activity? activity, string toolId, Exception exception);

    /// <summary>
    /// Records tool usage metrics.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="success">Whether the execution was successful.</param>
    /// <param name="resourceUsage">Resource usage information.</param>
    public void RecordToolUsage(string toolId, TimeSpan duration, bool success, ToolResourceUsage? resourceUsage = null);

    /// <summary>
    /// Records a security event.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="eventType">The security event type.</param>
    /// <param name="details">Event details.</param>
    public void RecordSecurityEvent(string toolId, string eventType, Dictionary<string, object?> details);

    /// <summary>
    /// Gets tool performance statistics.
    /// </summary>
    /// <param name="toolId">Optional tool ID to filter by.</param>
    /// <param name="timeRange">Time range for statistics.</param>
    /// <returns>Performance statistics.</returns>
    public Task<ToolPerformanceStatistics> GetPerformanceStatisticsAsync(string? toolId = null, TimeSpan? timeRange = null);

    /// <summary>
    /// Gets tool usage analytics.
    /// </summary>
    /// <param name="timeRange">Time range for analytics.</param>
    /// <returns>Usage analytics.</returns>
    public Task<ToolUsageAnalytics> GetUsageAnalyticsAsync(TimeSpan? timeRange = null);

    /// <summary>
    /// Exports observability data.
    /// </summary>
    /// <param name="format">Export format (json, csv, etc).</param>
    /// <param name="options">Export options.</param>
    /// <returns>Exported data.</returns>
    public Task<string> ExportObservabilityDataAsync(string format, ExportOptions? options = null);
}

/// <summary>
/// Tool performance statistics.
/// </summary>
public class ToolPerformanceStatistics
{
    /// <summary>
    /// Gets or sets the tool ID (null for aggregate stats).
    /// </summary>
    public string? ToolId { get; set; }

    /// <summary>
    /// Gets or sets the total execution count.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the success count.
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets the failure count.
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the minimum execution time.
    /// </summary>
    public TimeSpan MinExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the maximum execution time.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the P50 execution time.
    /// </summary>
    public TimeSpan P50ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the P90 execution time.
    /// </summary>
    public TimeSpan P90ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the P99 execution time.
    /// </summary>
    public TimeSpan P99ExecutionTime { get; set; }

    /// <summary>
    /// Gets the success rate.
    /// </summary>
    public double SuccessRate => ExecutionCount > 0 ? (double)SuccessCount / ExecutionCount : 0;

    /// <summary>
    /// Gets or sets resource usage statistics.
    /// </summary>
    public ResourceUsageStatistics? ResourceUsage { get; set; }

    /// <summary>
    /// Gets or sets error distribution.
    /// </summary>
    public Dictionary<string, long> ErrorDistribution { get; set; } = new();

    /// <summary>
    /// Gets or sets the time period for these statistics.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time for these statistics.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }
}

/// <summary>
/// Resource usage statistics.
/// </summary>
public class ResourceUsageStatistics
{
    /// <summary>
    /// Gets or sets average memory usage in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets average CPU usage percentage.
    /// </summary>
    public double AverageCpuUsage { get; set; }

    /// <summary>
    /// Gets or sets peak CPU usage percentage.
    /// </summary>
    public double PeakCpuUsage { get; set; }

    /// <summary>
    /// Gets or sets total file system operations.
    /// </summary>
    public long FileSystemOperations { get; set; }

    /// <summary>
    /// Gets or sets total network operations.
    /// </summary>
    public long NetworkOperations { get; set; }
}

/// <summary>
/// Tool usage analytics.
/// </summary>
public class ToolUsageAnalytics
{
    /// <summary>
    /// Gets or sets tool usage by ID.
    /// </summary>
    public Dictionary<string, ToolUsageInfo> ToolUsage { get; set; } = new();

    /// <summary>
    /// Gets or sets usage patterns.
    /// </summary>
    public List<UsagePattern> UsagePatterns { get; set; } = new();

    /// <summary>
    /// Gets or sets peak usage times.
    /// </summary>
    public List<PeakUsageTime> PeakUsageTimes { get; set; } = new();

    /// <summary>
    /// Gets or sets tool combinations frequently used together.
    /// </summary>
    public List<ToolCombination> FrequentCombinations { get; set; } = new();

    /// <summary>
    /// Gets or sets the time period for analytics.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time for analytics.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }
}

/// <summary>
/// Tool usage information.
/// </summary>
public class ToolUsageInfo
{
    /// <summary>
    /// Gets or sets the tool ID.
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets unique user count.
    /// </summary>
    public int UniqueUsers { get; set; }

    /// <summary>
    /// Gets or sets most common parameters.
    /// </summary>
    public Dictionary<string, object?> CommonParameters { get; set; } = new();

    /// <summary>
    /// Gets or sets usage trend (increase/decrease percentage).
    /// </summary>
    public double UsageTrend { get; set; }
}

/// <summary>
/// Usage pattern information.
/// </summary>
public class UsagePattern
{
    /// <summary>
    /// Gets or sets the pattern name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pattern description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the occurrence count.
    /// </summary>
    public int Occurrences { get; set; }

    /// <summary>
    /// Gets or sets the tools involved.
    /// </summary>
    public List<string> ToolIds { get; set; } = new();
}

/// <summary>
/// Peak usage time information.
/// </summary>
public class PeakUsageTime
{
    /// <summary>
    /// Gets or sets the time period.
    /// </summary>
    public DateTimeOffset Time { get; set; }

    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the concurrent executions.
    /// </summary>
    public int ConcurrentExecutions { get; set; }
}

/// <summary>
/// Tool combination information.
/// </summary>
public class ToolCombination
{
    /// <summary>
    /// Gets or sets the tool IDs in the combination.
    /// </summary>
    public List<string> ToolIds { get; set; } = new();

    /// <summary>
    /// Gets or sets how often this combination occurs.
    /// </summary>
    public int Frequency { get; set; }

    /// <summary>
    /// Gets or sets the average time between executions.
    /// </summary>
    public TimeSpan AverageTimeBetween { get; set; }
}

/// <summary>
/// Options for exporting observability data.
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// Gets or sets the start time for the export.
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time for the export.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets specific tool IDs to include.
    /// </summary>
    public List<string>? ToolIds { get; set; }

    /// <summary>
    /// Gets or sets whether to include raw data.
    /// </summary>
    public bool IncludeRawData { get; set; }

    /// <summary>
    /// Gets or sets whether to include aggregated statistics.
    /// </summary>
    public bool IncludeStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include security events.
    /// </summary>
    public bool IncludeSecurityEvents { get; set; } = true;
}
