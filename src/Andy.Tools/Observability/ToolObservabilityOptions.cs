namespace Andy.Tools.Observability;

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
