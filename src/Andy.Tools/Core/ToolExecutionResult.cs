namespace Andy.Tools.Core;

/// <summary>
/// Represents the result of a tool execution with additional execution details.
/// </summary>
public class ToolExecutionResult : ToolResult
{
    /// <summary>
    /// Gets or sets the tool ID that was executed.
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correlation ID for this execution.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the execution started.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets when the execution ended.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the peak memory usage during execution in bytes.
    /// </summary>
    public long? PeakMemoryUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets whether the execution was cancelled.
    /// </summary>
    public bool WasCancelled { get; set; }

    /// <summary>
    /// Gets or sets whether the execution hit resource limits.
    /// </summary>
    public bool HitResourceLimits { get; set; }

    /// <summary>
    /// Gets or sets resource usage statistics.
    /// </summary>
    public ToolResourceUsage? ResourceUsage { get; set; }

    /// <summary>
    /// Gets or sets security violations that occurred during execution.
    /// </summary>
    public IList<string> SecurityViolations { get; set; } = [];

    /// <summary>
    /// Creates a tool execution result from a tool result.
    /// </summary>
    /// <param name="toolResult">The tool result.</param>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>A tool execution result.</returns>
    public static ToolExecutionResult FromToolResult(ToolResult toolResult, string toolId, string correlationId)
    {
        return new ToolExecutionResult
        {
            ToolId = toolId,
            CorrelationId = correlationId,
            IsSuccessful = toolResult.IsSuccessful,
            Data = toolResult.Data,
            ErrorMessage = toolResult.ErrorMessage,
            Metadata = new Dictionary<string, object?>(toolResult.Metadata),
            DurationMs = toolResult.DurationMs,
            StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-(toolResult.DurationMs ?? 0)),
            EndTime = DateTimeOffset.UtcNow
        };
    }
}
