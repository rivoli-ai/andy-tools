namespace Andy.Tools.Advanced;

/// <summary>
/// Configuration options for tool metrics.
/// </summary>
public class ToolMetricsOptions
{
    /// <summary>
    /// Gets or sets the maximum number of metrics to keep per tool.
    /// </summary>
    public int MaxMetricsPerTool { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the metrics retention period.
    /// </summary>
    public TimeSpan? MetricsRetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the aggregation interval.
    /// </summary>
    public TimeSpan AggregationInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to collect resource usage metrics.
    /// </summary>
    public bool CollectResourceUsage { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable detailed performance tracking.
    /// </summary>
    public bool EnableDetailedTracking { get; set; } = true;
}
