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
