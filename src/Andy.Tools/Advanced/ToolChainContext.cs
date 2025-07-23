using Andy.Tools.Core;

namespace Andy.Tools.Advanced;

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
