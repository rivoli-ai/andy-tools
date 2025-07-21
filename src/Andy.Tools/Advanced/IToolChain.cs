using Andy.Tools.Core;

namespace Andy.Tools.Advanced;

/// <summary>
/// Represents a chain of tools that can be executed in sequence with dependency management.
/// </summary>
public interface IToolChain
{
    /// <summary>
    /// Gets the unique identifier for this tool chain.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the name of this tool chain.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the description of this tool chain.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the steps in this tool chain.
    /// </summary>
    public IReadOnlyList<IToolChainStep> Steps { get; }

    /// <summary>
    /// Adds a step to the tool chain.
    /// </summary>
    /// <param name="step">The step to add.</param>
    /// <returns>The tool chain for fluent configuration.</returns>
    public IToolChain AddStep(IToolChainStep step);

    /// <summary>
    /// Adds a tool step to the chain.
    /// </summary>
    /// <param name="toolId">The ID of the tool to execute.</param>
    /// <param name="parameters">The parameters for the tool.</param>
    /// <param name="name">Optional name for the step.</param>
    /// <returns>The tool chain for fluent configuration.</returns>
    public IToolChain AddToolStep(string toolId, Dictionary<string, object?> parameters, string? name = null);

    /// <summary>
    /// Adds a conditional step to the chain.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="thenStep">The step to execute if condition is true.</param>
    /// <param name="elseStep">The step to execute if condition is false.</param>
    /// <returns>The tool chain for fluent configuration.</returns>
    public IToolChain AddConditionalStep(Func<ToolChainContext, bool> condition, IToolChainStep thenStep, IToolChainStep? elseStep = null);

    /// <summary>
    /// Adds a parallel execution step to the chain.
    /// </summary>
    /// <param name="steps">The steps to execute in parallel.</param>
    /// <param name="name">Optional name for the parallel step.</param>
    /// <returns>The tool chain for fluent configuration.</returns>
    public IToolChain AddParallelStep(IEnumerable<IToolChainStep> steps, string? name = null);

    /// <summary>
    /// Adds a transformation step that processes the previous result.
    /// </summary>
    /// <param name="transform">The transformation function.</param>
    /// <param name="name">Optional name for the step.</param>
    /// <returns>The tool chain for fluent configuration.</returns>
    public IToolChain AddTransformStep(Func<object?, ToolChainContext, Task<object?>> transform, string? name = null);

    /// <summary>
    /// Validates the tool chain configuration.
    /// </summary>
    /// <returns>A list of validation errors, or empty if valid.</returns>
    public IList<string> Validate();

    /// <summary>
    /// Executes the tool chain.
    /// </summary>
    /// <param name="initialParameters">Initial parameters for the chain.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the tool chain execution.</returns>
    public Task<ToolChainResult> ExecuteAsync(
        Dictionary<string, object?>? initialParameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a step in a tool chain.
/// </summary>
public interface IToolChainStep
{
    /// <summary>
    /// Gets the unique identifier for this step.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the name of this step.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type of this step.
    /// </summary>
    public ToolChainStepType Type { get; }

    /// <summary>
    /// Gets the dependencies for this step (step IDs that must complete before this step).
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; }

    /// <summary>
    /// Gets whether this step can be retried on failure.
    /// </summary>
    public bool IsRetryable { get; }

    /// <summary>
    /// Gets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; }

    /// <summary>
    /// Executes the step.
    /// </summary>
    /// <param name="context">The tool chain execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the step execution.</returns>
    public Task<ToolChainStepResult> ExecuteAsync(ToolChainContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Types of tool chain steps.
/// </summary>
public enum ToolChainStepType
{
    /// <summary>Tool execution step.</summary>
    Tool,
    /// <summary>Conditional branching step.</summary>
    Conditional,
    /// <summary>Parallel execution step.</summary>
    Parallel,
    /// <summary>Data transformation step.</summary>
    Transform,
    /// <summary>Loop/iteration step.</summary>
    Loop,
    /// <summary>Error handling step.</summary>
    ErrorHandler,
    /// <summary>Custom step.</summary>
    Custom
}

/// <summary>
/// Context for tool chain execution.
/// </summary>
public class ToolChainContext
{
    /// <summary>
    /// Gets the tool chain being executed.
    /// </summary>
    public IToolChain Chain { get; init; } = null!;

    /// <summary>
    /// Gets the current step being executed.
    /// </summary>
    public IToolChainStep? CurrentStep { get; set; }

    /// <summary>
    /// Gets the initial parameters passed to the chain.
    /// </summary>
    public Dictionary<string, object?> InitialParameters { get; init; } = [];

    /// <summary>
    /// Gets the results of completed steps.
    /// </summary>
    public Dictionary<string, ToolChainStepResult> StepResults { get; } = [];

    /// <summary>
    /// Gets the shared state between steps.
    /// </summary>
    public Dictionary<string, object?> SharedState { get; } = [];

    /// <summary>
    /// Gets the execution context from the tool framework.
    /// </summary>
    public ToolExecutionContext ExecutionContext { get; init; } = null!;

    /// <summary>
    /// Gets the start time of the chain execution.
    /// </summary>
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the current execution status.
    /// </summary>
    public ToolChainExecutionStatus Status { get; set; } = ToolChainExecutionStatus.NotStarted;

    /// <summary>
    /// Gets the list of errors encountered during execution.
    /// </summary>
    public List<ToolChainError> Errors { get; } = [];

    /// <summary>
    /// Gets the progress callback for reporting chain progress.
    /// </summary>
    public Action<ToolChainProgress>? OnProgress { get; set; }

    /// <summary>
    /// Gets the result of the previous step.
    /// </summary>
    public object? PreviousResult => StepResults.Values.LastOrDefault()?.Data;

    /// <summary>
    /// Gets a step result by step ID.
    /// </summary>
    /// <param name="stepId">The step ID.</param>
    /// <returns>The step result, or null if not found.</returns>
    public ToolChainStepResult? GetStepResult(string stepId)
    {
        return StepResults.TryGetValue(stepId, out var result) ? result : null;
    }

    /// <summary>
    /// Reports progress for the current step.
    /// </summary>
    /// <param name="message">Progress message.</param>
    /// <param name="percentage">Progress percentage (0-100).</param>
    public void ReportProgress(string message, double percentage)
    {
        OnProgress?.Invoke(new ToolChainProgress
        {
            ChainId = Chain.Id,
            StepId = CurrentStep?.Id,
            Message = message,
            Percentage = percentage,
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}

/// <summary>
/// Status of tool chain execution.
/// </summary>
public enum ToolChainExecutionStatus
{
    /// <summary>Not started.</summary>
    NotStarted,
    /// <summary>Currently running.</summary>
    Running,
    /// <summary>Completed successfully.</summary>
    Completed,
    /// <summary>Failed with errors.</summary>
    Failed,
    /// <summary>Cancelled by user.</summary>
    Cancelled,
    /// <summary>Partially completed with some failures.</summary>
    PartiallyCompleted
}

/// <summary>
/// Result of a tool chain execution.
/// </summary>
public class ToolChainResult
{
    /// <summary>
    /// Gets or sets the chain ID.
    /// </summary>
    public string ChainId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the execution status.
    /// </summary>
    public ToolChainExecutionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the final result data.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets the step results.
    /// </summary>
    public Dictionary<string, ToolChainStepResult> StepResults { get; set; } = [];

    /// <summary>
    /// Gets or sets the errors encountered.
    /// </summary>
    public List<ToolChainError> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets the execution duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];

    /// <summary>
    /// Gets whether the execution was successful.
    /// </summary>
    public bool IsSuccessful => Status == ToolChainExecutionStatus.Completed;

    /// <summary>
    /// Gets the number of successful steps.
    /// </summary>
    public int SuccessfulSteps => StepResults.Count(r => r.Value.IsSuccessful);

    /// <summary>
    /// Gets the number of failed steps.
    /// </summary>
    public int FailedSteps => StepResults.Count(r => !r.Value.IsSuccessful);
}

/// <summary>
/// Result of a tool chain step execution.
/// </summary>
public class ToolChainStepResult
{
    /// <summary>
    /// Gets or sets the step ID.
    /// </summary>
    public string StepId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the step name.
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the step was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the result data.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets the error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the exception if one occurred.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the execution duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts.
    /// </summary>
    public int RetryAttempts { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];
}

/// <summary>
/// Represents an error in tool chain execution.
/// </summary>
public class ToolChainError
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the step ID where the error occurred.
    /// </summary>
    public string? StepId { get; set; }

    /// <summary>
    /// Gets or sets the exception if one was thrown.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets when the error occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    public Dictionary<string, object?> Details { get; set; } = [];
}

/// <summary>
/// Progress information for tool chain execution.
/// </summary>
public class ToolChainProgress
{
    /// <summary>
    /// Gets or sets the chain ID.
    /// </summary>
    public string ChainId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current step ID.
    /// </summary>
    public string? StepId { get; set; }

    /// <summary>
    /// Gets or sets the progress message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the progress percentage (0-100).
    /// </summary>
    public double Percentage { get; set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
