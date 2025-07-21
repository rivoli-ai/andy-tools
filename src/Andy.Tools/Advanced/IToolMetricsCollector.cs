using Andy.Tools.Core;

namespace Andy.Tools.Advanced;

/// <summary>
/// Interface for collecting tool execution metrics.
/// </summary>
public interface IToolMetricsCollector
{
    /// <summary>
    /// Records a tool execution.
    /// </summary>
    /// <param name="execution">The execution details.</param>
    public Task RecordExecutionAsync(ToolExecutionMetrics execution);

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="timeSavedMs">Time saved by using cache.</param>
    public Task RecordCacheHitAsync(string toolId, double timeSavedMs);

    /// <summary>
    /// Records a cache miss.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    public Task RecordCacheMissAsync(string toolId);

    /// <summary>
    /// Gets metrics for a specific tool.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="timeRange">Optional time range.</param>
    /// <returns>Tool metrics.</returns>
    public Task<ToolMetrics> GetToolMetricsAsync(string toolId, TimeRange? timeRange = null);

    /// <summary>
    /// Gets metrics for all tools.
    /// </summary>
    /// <param name="timeRange">Optional time range.</param>
    /// <returns>Metrics for all tools.</returns>
    public Task<Dictionary<string, ToolMetrics>> GetAllToolMetricsAsync(TimeRange? timeRange = null);

    /// <summary>
    /// Gets system-wide metrics.
    /// </summary>
    /// <param name="timeRange">Optional time range.</param>
    /// <returns>System metrics.</returns>
    public Task<SystemMetrics> GetSystemMetricsAsync(TimeRange? timeRange = null);

    /// <summary>
    /// Gets performance trends.
    /// </summary>
    /// <param name="toolId">Optional tool ID for tool-specific trends.</param>
    /// <param name="interval">Time interval for aggregation.</param>
    /// <param name="timeRange">Time range.</param>
    /// <returns>Performance trends.</returns>
    public Task<List<PerformanceTrend>> GetPerformanceTrendsAsync(
        string? toolId,
        TimeInterval interval,
        TimeRange timeRange);

    /// <summary>
    /// Exports metrics in a specific format.
    /// </summary>
    /// <param name="format">Export format.</param>
    /// <param name="timeRange">Optional time range.</param>
    /// <returns>Exported metrics data.</returns>
    public Task<string> ExportMetricsAsync(MetricsExportFormat format, TimeRange? timeRange = null);

    /// <summary>
    /// Clears metrics older than the specified age.
    /// </summary>
    /// <param name="olderThan">Age threshold.</param>
    /// <returns>Number of metrics cleared.</returns>
    public Task<int> ClearOldMetricsAsync(TimeSpan olderThan);
}

/// <summary>
/// Tool execution metrics.
/// </summary>
public class ToolExecutionMetrics
{
    /// <summary>
    /// Gets or sets the execution ID.
    /// </summary>
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the tool ID.
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the execution was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the execution duration in milliseconds.
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Gets or sets the error code if failed.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets resource usage metrics.
    /// </summary>
    public ResourceUsageMetrics? ResourceUsage { get; set; }

    /// <summary>
    /// Gets or sets custom metrics.
    /// </summary>
    public Dictionary<string, double> CustomMetrics { get; set; } = [];

    /// <summary>
    /// Gets or sets custom tags.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = [];
}

/// <summary>
/// Resource usage metrics.
/// </summary>
public class ResourceUsageMetrics
{
    /// <summary>
    /// Gets or sets CPU usage percentage.
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Gets or sets memory usage in bytes.
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets disk I/O read bytes.
    /// </summary>
    public long DiskReadBytes { get; set; }

    /// <summary>
    /// Gets or sets disk I/O write bytes.
    /// </summary>
    public long DiskWriteBytes { get; set; }

    /// <summary>
    /// Gets or sets network I/O sent bytes.
    /// </summary>
    public long NetworkSentBytes { get; set; }

    /// <summary>
    /// Gets or sets network I/O received bytes.
    /// </summary>
    public long NetworkReceivedBytes { get; set; }
}

/// <summary>
/// Aggregated metrics for a tool.
/// </summary>
public class ToolMetrics
{
    /// <summary>
    /// Gets or sets the tool ID.
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total execution count.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the successful execution count.
    /// </summary>
    public long SuccessfulExecutions { get; set; }

    /// <summary>
    /// Gets or sets the failed execution count.
    /// </summary>
    public long FailedExecutions { get; set; }

    /// <summary>
    /// Gets the success rate.
    /// </summary>
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions : 0;

    /// <summary>
    /// Gets or sets the average duration in milliseconds.
    /// </summary>
    public double AverageDurationMs { get; set; }

    /// <summary>
    /// Gets or sets the minimum duration in milliseconds.
    /// </summary>
    public double MinDurationMs { get; set; }

    /// <summary>
    /// Gets or sets the maximum duration in milliseconds.
    /// </summary>
    public double MaxDurationMs { get; set; }

    /// <summary>
    /// Gets or sets the 50th percentile duration.
    /// </summary>
    public double P50DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the 90th percentile duration.
    /// </summary>
    public double P90DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile duration.
    /// </summary>
    public double P99DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the cache hit count.
    /// </summary>
    public long CacheHits { get; set; }

    /// <summary>
    /// Gets or sets the cache miss count.
    /// </summary>
    public long CacheMisses { get; set; }

    /// <summary>
    /// Gets the cache hit rate.
    /// </summary>
    public double CacheHitRate => CacheHits + CacheMisses > 0 ? (double)CacheHits / (CacheHits + CacheMisses) : 0;

    /// <summary>
    /// Gets or sets the average time saved by caching.
    /// </summary>
    public double AverageTimeSavedByCacheMs { get; set; }

    /// <summary>
    /// Gets or sets error distribution.
    /// </summary>
    public Dictionary<string, long> ErrorDistribution { get; set; } = [];

    /// <summary>
    /// Gets or sets average resource usage.
    /// </summary>
    public ResourceUsageMetrics? AverageResourceUsage { get; set; }

    /// <summary>
    /// Gets or sets the time range for these metrics.
    /// </summary>
    public TimeRange? TimeRange { get; set; }

    /// <summary>
    /// Gets or sets when the metrics were calculated.
    /// </summary>
    public DateTimeOffset CalculatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// System-wide metrics.
/// </summary>
public class SystemMetrics
{
    /// <summary>
    /// Gets or sets total executions across all tools.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets successful executions across all tools.
    /// </summary>
    public long SuccessfulExecutions { get; set; }

    /// <summary>
    /// Gets or sets failed executions across all tools.
    /// </summary>
    public long FailedExecutions { get; set; }

    /// <summary>
    /// Gets the overall success rate.
    /// </summary>
    public double OverallSuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions : 0;

    /// <summary>
    /// Gets or sets the number of unique tools used.
    /// </summary>
    public int UniqueToolsUsed { get; set; }

    /// <summary>
    /// Gets or sets the number of unique users.
    /// </summary>
    public int UniqueUsers { get; set; }

    /// <summary>
    /// Gets or sets the number of unique sessions.
    /// </summary>
    public int UniqueSessions { get; set; }

    /// <summary>
    /// Gets or sets the most used tools.
    /// </summary>
    public List<ToolUsageInfo> MostUsedTools { get; set; } = [];

    /// <summary>
    /// Gets or sets the slowest tools.
    /// </summary>
    public List<ToolPerformanceInfo> SlowestTools { get; set; } = [];

    /// <summary>
    /// Gets or sets the tools with highest failure rates.
    /// </summary>
    public List<ToolReliabilityInfo> LeastReliableTools { get; set; } = [];

    /// <summary>
    /// Gets or sets peak usage times.
    /// </summary>
    public List<PeakUsageInfo> PeakUsageTimes { get; set; } = [];

    /// <summary>
    /// Gets or sets total cache hits.
    /// </summary>
    public long TotalCacheHits { get; set; }

    /// <summary>
    /// Gets or sets total cache misses.
    /// </summary>
    public long TotalCacheMisses { get; set; }

    /// <summary>
    /// Gets the overall cache hit rate.
    /// </summary>
    public double OverallCacheHitRate => TotalCacheHits + TotalCacheMisses > 0
        ? (double)TotalCacheHits / (TotalCacheHits + TotalCacheMisses) : 0;

    /// <summary>
    /// Gets or sets the time range for these metrics.
    /// </summary>
    public TimeRange? TimeRange { get; set; }

    /// <summary>
    /// Gets or sets when the metrics were calculated.
    /// </summary>
    public DateTimeOffset CalculatedAt { get; set; } = DateTimeOffset.UtcNow;
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
    /// Gets or sets the tool name.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the percentage of total executions.
    /// </summary>
    public double UsagePercentage { get; set; }
}

/// <summary>
/// Tool performance information.
/// </summary>
public class ToolPerformanceInfo
{
    /// <summary>
    /// Gets or sets the tool ID.
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the average duration.
    /// </summary>
    public double AverageDurationMs { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile duration.
    /// </summary>
    public double P99DurationMs { get; set; }
}

/// <summary>
/// Tool reliability information.
/// </summary>
public class ToolReliabilityInfo
{
    /// <summary>
    /// Gets or sets the tool ID.
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the failure rate.
    /// </summary>
    public double FailureRate { get; set; }

    /// <summary>
    /// Gets or sets the total failures.
    /// </summary>
    public long TotalFailures { get; set; }

    /// <summary>
    /// Gets or sets the most common error.
    /// </summary>
    public string? MostCommonError { get; set; }
}

/// <summary>
/// Peak usage information.
/// </summary>
public class PeakUsageInfo
{
    /// <summary>
    /// Gets or sets the time period.
    /// </summary>
    public DateTimeOffset TimePeriod { get; set; }

    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the average response time.
    /// </summary>
    public double AverageResponseTimeMs { get; set; }
}

/// <summary>
/// Performance trend data point.
/// </summary>
public class PerformanceTrend
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the success rate.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the average duration.
    /// </summary>
    public double AverageDurationMs { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate.
    /// </summary>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets custom metrics.
    /// </summary>
    public Dictionary<string, double> CustomMetrics { get; set; } = [];
}

/// <summary>
/// Time range for metrics queries.
/// </summary>
public class TimeRange
{
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTimeOffset End { get; set; }

    /// <summary>
    /// Creates a time range for the last N hours.
    /// </summary>
    public static TimeRange LastHours(int hours) => new()
    {
        Start = DateTimeOffset.UtcNow.AddHours(-hours),
        End = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Creates a time range for the last N days.
    /// </summary>
    public static TimeRange LastDays(int days) => new()
    {
        Start = DateTimeOffset.UtcNow.AddDays(-days),
        End = DateTimeOffset.UtcNow
    };
}

/// <summary>
/// Time interval for aggregation.
/// </summary>
public enum TimeInterval
{
    /// <summary>Minute interval.</summary>
    Minute,
    /// <summary>Hour interval.</summary>
    Hour,
    /// <summary>Day interval.</summary>
    Day,
    /// <summary>Week interval.</summary>
    Week,
    /// <summary>Month interval.</summary>
    Month
}

/// <summary>
/// Metrics export format.
/// </summary>
public enum MetricsExportFormat
{
    /// <summary>JSON format.</summary>
    Json,
    /// <summary>CSV format.</summary>
    Csv,
    /// <summary>Prometheus format.</summary>
    Prometheus,
    /// <summary>OpenTelemetry format.</summary>
    OpenTelemetry
}
