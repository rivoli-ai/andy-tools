namespace Andy.Tools.Observability;

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
