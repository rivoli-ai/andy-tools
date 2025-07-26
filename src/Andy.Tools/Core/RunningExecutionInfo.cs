namespace Andy.Tools.Core;

/// <summary>
/// Information about a currently running tool execution.
/// </summary>
public class RunningExecutionInfo
{
    /// <summary>
    /// Gets or sets the tool ID.
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correlation ID.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the execution started.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the current resource usage.
    /// </summary>
    public ToolResourceUsage? CurrentResourceUsage { get; set; }

    /// <summary>
    /// Gets or sets the user ID executing the tool.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public string? SessionId { get; set; }
}
