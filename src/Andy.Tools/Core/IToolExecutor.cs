namespace Andy.Tools.Core;

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
