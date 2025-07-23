namespace Andy.Tools.Advanced;

/// <summary>
/// Represents an error in tool chain execution.
/// </summary>
public class ToolChainError
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the step ID where the error occurred.
    /// </summary>
    public string? StepId { get; set; }

    /// <summary>
    /// Gets or sets the exception if one was thrown.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets when the error occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    public Dictionary<string, object?> Details { get; set; } = [];
}
