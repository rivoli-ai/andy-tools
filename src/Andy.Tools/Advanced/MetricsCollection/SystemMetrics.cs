namespace Andy.Tools.Advanced.MetricsCollection;

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
