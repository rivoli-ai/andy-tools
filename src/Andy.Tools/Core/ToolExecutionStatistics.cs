namespace Andy.Tools.Core;

/// <summary>
/// Statistics about tool executions.
/// </summary>
public class ToolExecutionStatistics
{
    /// <summary>
    /// Gets or sets the total number of executions.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of successful executions.
    /// </summary>
    public long SuccessfulExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed executions.
    /// </summary>
    public long FailedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of cancelled executions.
    /// </summary>
    public long CancelledExecutions { get; set; }

    /// <summary>
    /// Gets or sets the average execution time in milliseconds.
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the number of security violations.
    /// </summary>
    public long SecurityViolations { get; set; }

    /// <summary>
    /// Gets or sets the number of resource limit violations.
    /// </summary>
    public long ResourceLimitViolations { get; set; }

    /// <summary>
    /// Gets or sets the breakdown by tool ID.
    /// </summary>
    public Dictionary<string, long> ExecutionsByTool { get; set; } = [];

    /// <summary>
    /// Gets or sets the breakdown by user ID.
    /// </summary>
    public Dictionary<string, long> ExecutionsByUser { get; set; } = [];

    /// <summary>
    /// Gets or sets when these statistics were generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
