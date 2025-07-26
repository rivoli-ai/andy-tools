namespace Andy.Tools.Core;

/// <summary>
/// Event arguments for security violation events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SecurityViolationEventArgs"/> class.
/// </remarks>
/// <param name="toolId">The tool ID.</param>
/// <param name="correlationId">The correlation ID.</param>
/// <param name="violation">The violation description.</param>
/// <param name="severity">The violation severity.</param>
public class SecurityViolationEventArgs(string toolId, string correlationId, string violation, SecurityViolationSeverity severity) : EventArgs
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
    /// Gets the violation description.
    /// </summary>
    public string Violation { get; } = violation;

    /// <summary>
    /// Gets the severity of the violation.
    /// </summary>
    public SecurityViolationSeverity Severity { get; } = severity;
}
