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

/// <summary>
/// Represents resource usage statistics for a tool execution.
/// </summary>
public class ToolResourceUsage
{
    /// <summary>
    /// Gets or sets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the average memory usage in bytes.
    /// </summary>
    public long AverageMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the total CPU time in milliseconds.
    /// </summary>
    public double CpuTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the number of files accessed.
    /// </summary>
    public int FilesAccessed { get; set; }

    /// <summary>
    /// Gets or sets the total bytes read from files.
    /// </summary>
    public long BytesRead { get; set; }

    /// <summary>
    /// Gets or sets the total bytes written to files.
    /// </summary>
    public long BytesWritten { get; set; }

    /// <summary>
    /// Gets or sets the number of network requests made.
    /// </summary>
    public int NetworkRequests { get; set; }

    /// <summary>
    /// Gets or sets the total bytes sent over the network.
    /// </summary>
    public long NetworkBytesSent { get; set; }

    /// <summary>
    /// Gets or sets the total bytes received over the network.
    /// </summary>
    public long NetworkBytesReceived { get; set; }

    /// <summary>
    /// Gets or sets the number of processes started.
    /// </summary>
    public int ProcessesStarted { get; set; }
}

/// <summary>
/// Interface for executing tools with security and resource management.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Executes a tool with the specified request.
    /// </summary>
    /// <param name="request">The execution request.</param>
    /// <returns>A task representing the execution result.</returns>
    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request);

    /// <summary>
    /// Executes a tool with the specified parameters and context.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="parameters">The execution parameters.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A task representing the execution result.</returns>
    public Task<ToolExecutionResult> ExecuteAsync(string toolId, Dictionary<string, object?> parameters, ToolExecutionContext? context = null);

    /// <summary>
    /// Validates a tool execution request without executing it.
    /// </summary>
    /// <param name="request">The execution request to validate.</param>
    /// <returns>A list of validation errors, or empty if valid.</returns>
    public Task<IList<string>> ValidateExecutionRequestAsync(ToolExecutionRequest request);

    /// <summary>
    /// Gets the estimated resource usage for a tool execution.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="parameters">The execution parameters.</param>
    /// <returns>Estimated resource usage, or null if not available.</returns>
    public Task<ToolResourceUsage?> EstimateResourceUsageAsync(string toolId, Dictionary<string, object?> parameters);

    /// <summary>
    /// Cancels all running executions for a specific correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>The number of executions that were cancelled.</returns>
    public Task<int> CancelExecutionsAsync(string correlationId);

    /// <summary>
    /// Gets information about currently running executions.
    /// </summary>
    /// <returns>A list of running execution information.</returns>
    public IReadOnlyList<RunningExecutionInfo> GetRunningExecutions();

    /// <summary>
    /// Gets execution statistics.
    /// </summary>
    /// <returns>Execution statistics.</returns>
    public ToolExecutionStatistics GetStatistics();

    /// <summary>
    /// Event raised when a tool execution starts.
    /// </summary>
    public event EventHandler<ToolExecutionStartedEventArgs>? ExecutionStarted;

    /// <summary>
    /// Event raised when a tool execution completes.
    /// </summary>
    public event EventHandler<ToolExecutionCompletedEventArgs>? ExecutionCompleted;

    /// <summary>
    /// Event raised when a security violation occurs during execution.
    /// </summary>
    public event EventHandler<SecurityViolationEventArgs>? SecurityViolation;
}

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

/// <summary>
/// Event arguments for tool execution completed events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolExecutionCompletedEventArgs"/> class.
/// </remarks>
/// <param name="result">The execution result.</param>
public class ToolExecutionCompletedEventArgs(ToolExecutionResult result) : EventArgs
{
    /// <summary>
    /// Gets the execution result.
    /// </summary>
    public ToolExecutionResult Result { get; } = result;
}

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

/// <summary>
/// Represents the severity of a security violation.
/// </summary>
public enum SecurityViolationSeverity
{
    /// <summary>Low severity violation.</summary>
    Low,
    /// <summary>Medium severity violation.</summary>
    Medium,
    /// <summary>High severity violation.</summary>
    High,
    /// <summary>Critical severity violation.</summary>
    Critical
}
