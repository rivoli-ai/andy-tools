namespace Andy.Tools.Advanced;

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
