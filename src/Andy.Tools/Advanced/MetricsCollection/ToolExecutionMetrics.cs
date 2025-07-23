namespace Andy.Tools.Advanced.MetricsCollection;

/// <summary>
/// Tool execution metrics.
/// </summary>
public class ToolExecutionMetrics
{
    /// <summary>
    /// Gets or sets the execution ID.
    /// </summary>
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the tool ID.
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the execution was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the execution duration in milliseconds.
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Gets or sets the error code if failed.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets resource usage metrics.
    /// </summary>
    public ResourceUsageMetrics? ResourceUsage { get; set; }

    /// <summary>
    /// Gets or sets custom metrics.
    /// </summary>
    public Dictionary<string, double> CustomMetrics { get; set; } = [];

    /// <summary>
    /// Gets or sets custom tags.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = [];
}
