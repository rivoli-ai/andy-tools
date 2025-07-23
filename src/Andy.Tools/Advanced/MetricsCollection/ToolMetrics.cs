namespace Andy.Tools.Advanced.MetricsCollection;

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
