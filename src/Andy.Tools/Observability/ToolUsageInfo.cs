namespace Andy.Tools.Observability;

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
