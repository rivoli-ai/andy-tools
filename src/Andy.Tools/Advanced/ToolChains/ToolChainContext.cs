using System.Collections.Concurrent;
using Andy.Tools.Core;

namespace Andy.Tools.Advanced.ToolChains;

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
    /// Gets the results of completed steps. Backed by a concurrent dictionary because parallel steps
    /// may read and write it simultaneously.
    /// </summary>
    public IDictionary<string, ToolChainStepResult> StepResults { get; } = new ConcurrentDictionary<string, ToolChainStepResult>();

    /// <summary>
    /// Gets the shared state between steps. Backed by a concurrent dictionary because parallel steps may
    /// read and write it simultaneously.
    /// </summary>
    public IDictionary<string, object?> SharedState { get; } = new ConcurrentDictionary<string, object?>();

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
    /// Gets or sets the id of the most recently executed step. The orchestrator sets this so
    /// <see cref="PreviousResult"/> is well-defined even though <see cref="StepResults"/> is unordered.
    /// </summary>
    public string? LastStepId { get; set; }

    /// <summary>
    /// Gets the result data of the most recently executed step. Resolved via <see cref="LastStepId"/>
    /// rather than dictionary enumeration order, which is not insertion-ordered.
    /// </summary>
    public object? PreviousResult =>
        LastStepId != null && StepResults.TryGetValue(LastStepId, out var result) ? result?.Data : null;

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
