namespace Andy.Tools.Advanced;

/// <summary>
/// Tool reliability information.
/// </summary>
public class ToolReliabilityInfo
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
    /// Gets or sets the failure rate.
    /// </summary>
    public double FailureRate { get; set; }

    /// <summary>
    /// Gets or sets the total failures.
    /// </summary>
    public long TotalFailures { get; set; }

    /// <summary>
    /// Gets or sets the most common error.
    /// </summary>
    public string? MostCommonError { get; set; }
}
