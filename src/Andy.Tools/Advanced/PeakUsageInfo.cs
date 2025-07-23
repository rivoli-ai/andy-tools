namespace Andy.Tools.Advanced;

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
