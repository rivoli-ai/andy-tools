namespace Andy.Tools.Advanced.MetricsCollection;

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
