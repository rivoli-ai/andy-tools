namespace Andy.Tools.Core;

/// <summary>
/// Represents the execution context for a tool.
/// </summary>
public class ToolExecutionContext
{
    /// <summary>
    /// Gets or sets the correlation ID for tracking this execution.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID executing the tool.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the session ID for this execution.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the working directory for tool execution.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets environment variables for tool execution.
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = [];

    /// <summary>
    /// Gets or sets the permissions granted for this execution.
    /// </summary>
    public ToolPermissions Permissions { get; set; } = new();

    /// <summary>
    /// Gets or sets the resource limits for this execution.
    /// </summary>
    public ToolResourceLimits ResourceLimits { get; set; } = new();

    /// <summary>
    /// Gets or sets the cancellation token for this execution.
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = default;

    /// <summary>
    /// Gets or sets additional context data.
    /// </summary>
    public Dictionary<string, object?> AdditionalData { get; set; } = [];

    /// <summary>
    /// Gets or sets the progress callback for reporting progress messages.
    /// </summary>
    public Action<string>? OnProgress { get; set; }

    /// <summary>
    /// Gets or sets the progress callback for reporting progress with percentage.
    /// </summary>
    public Action<double, string>? OnProgressWithPercentage { get; set; }
}
