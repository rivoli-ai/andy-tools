namespace Andy.Tools.Advanced;

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
