namespace Andy.Tools.Advanced.ToolChains;

/// <summary>
/// Progress information for tool chain execution.
/// </summary>
public class ToolChainProgress
{
    /// <summary>
    /// Gets or sets the chain ID.
    /// </summary>
    public string ChainId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current step ID.
    /// </summary>
    public string? StepId { get; set; }

    /// <summary>
    /// Gets or sets the progress message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the progress percentage (0-100).
    /// </summary>
    public double Percentage { get; set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
