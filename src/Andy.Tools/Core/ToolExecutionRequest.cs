namespace Andy.Tools.Core;

/// <summary>
/// Represents a tool execution request.
/// </summary>
public class ToolExecutionRequest
{
    /// <summary>
    /// Gets or sets the ID of the tool to execute.
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameters for tool execution.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = [];

    /// <summary>
    /// Gets or sets the execution context.
    /// </summary>
    public ToolExecutionContext Context { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to validate parameters before execution.
    /// </summary>
    public bool ValidateParameters { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enforce permissions.
    /// </summary>
    public bool EnforcePermissions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enforce resource limits.
    /// </summary>
    public bool EnforceResourceLimits { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for this execution in milliseconds.
    /// </summary>
    public int? TimeoutMs { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for this execution.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];
}
