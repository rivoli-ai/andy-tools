namespace Andy.Tools.Advanced;

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
