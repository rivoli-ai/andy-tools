namespace Andy.Tools.Observability;

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
