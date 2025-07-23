namespace Andy.Tools.Core;

/// <summary>
/// Event arguments for tool execution started events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolExecutionStartedEventArgs"/> class.
/// </remarks>
/// <param name="toolId">The tool ID.</param>
/// <param name="correlationId">The correlation ID.</param>
/// <param name="context">The execution context.</param>
public class ToolExecutionStartedEventArgs(string toolId, string correlationId, ToolExecutionContext context) : EventArgs
{
    /// <summary>
    /// Gets the tool ID.
    /// </summary>
    public string ToolId { get; } = toolId;

    /// <summary>
    /// Gets the correlation ID.
    /// </summary>
    public string CorrelationId { get; } = correlationId;

    /// <summary>
    /// Gets the execution context.
    /// </summary>
    public ToolExecutionContext Context { get; } = context;
}
