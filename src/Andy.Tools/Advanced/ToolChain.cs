using System.Collections.Concurrent;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Advanced;

/// <summary>
/// Default implementation of a tool chain.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolChain"/> class.
/// </remarks>
public class ToolChain(string id, string name, string description, IToolExecutor toolExecutor, ILogger<ToolChain> logger) : IToolChain
{
    private readonly List<IToolChainStep> _steps = [];
    private readonly IToolExecutor _toolExecutor = toolExecutor;
    private readonly ILogger<ToolChain> _logger = logger;

    /// <inheritdoc />
    public string Id { get; } = id;

    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public string Description { get; } = description;

    /// <inheritdoc />
    public IReadOnlyList<IToolChainStep> Steps => _steps.AsReadOnly();

    /// <inheritdoc />
    public IToolChain AddStep(IToolChainStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _steps.Add(step);
        return this;
    }

    /// <inheritdoc />
    public IToolChain AddToolStep(string toolId, Dictionary<string, object?> parameters, string? name = null)
    {
        var step = new ToolStep(
            id: Guid.NewGuid().ToString(),
            name: name ?? $"Execute {toolId}",
            toolId: toolId,
            parameters: parameters,
            toolExecutor: _toolExecutor);

        return AddStep(step);
    }

    /// <inheritdoc />
    public IToolChain AddConditionalStep(Func<ToolChainContext, bool> condition, IToolChainStep thenStep, IToolChainStep? elseStep = null)
    {
        var step = new ConditionalStep(
            id: Guid.NewGuid().ToString(),
            name: "Conditional",
            condition: condition,
            thenStep: thenStep,
            elseStep: elseStep);

        return AddStep(step);
    }

    /// <inheritdoc />
    public IToolChain AddParallelStep(IEnumerable<IToolChainStep> steps, string? name = null)
    {
        var stepsList = steps.ToList();
        if (stepsList.Count == 0)
        {
            throw new ArgumentException("Parallel step must contain at least one sub-step", nameof(steps));
        }

        var step = new ParallelStep(
            id: Guid.NewGuid().ToString(),
            name: name ?? "Parallel Execution",
            steps: stepsList);

        return AddStep(step);
    }

    /// <inheritdoc />
    public IToolChain AddTransformStep(Func<object?, ToolChainContext, Task<object?>> transform, string? name = null)
    {
        var step = new TransformStep(
            id: Guid.NewGuid().ToString(),
            name: name ?? "Transform",
            transform: transform);

        return AddStep(step);
    }

    /// <inheritdoc />
    public IList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
        {
            errors.Add("Tool chain ID cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Tool chain name cannot be empty");
        }

        if (_steps.Count == 0)
        {
            errors.Add("Tool chain must contain at least one step");
        }

        // Validate step dependencies
        var stepIds = new HashSet<string>(_steps.Select(s => s.Id));
        foreach (var step in _steps)
        {
            foreach (var dependency in step.Dependencies)
            {
                if (!stepIds.Contains(dependency))
                {
                    errors.Add($"Step '{step.Name}' has dependency on unknown step '{dependency}'");
                }
            }
        }

        // Check for circular dependencies
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var step in _steps)
        {
            if (HasCircularDependency(step.Id, visited, recursionStack))
            {
                errors.Add($"Circular dependency detected involving step '{step.Name}'");
            }
        }

        return errors;
    }

    /// <inheritdoc />
    public async Task<ToolChainResult> ExecuteAsync(
        Dictionary<string, object?>? initialParameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = Validate();
        if (validationErrors.Count > 0)
        {
            return new ToolChainResult
            {
                ChainId = Id,
                Status = ToolChainExecutionStatus.Failed,
                Errors = [.. validationErrors.Select(e => new ToolChainError
                {
                    Code = "VALIDATION_ERROR",
                    Message = e
                })],
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow
            };
        }

        var chainContext = new ToolChainContext
        {
            Chain = this,
            InitialParameters = initialParameters ?? [],
            ExecutionContext = context,
            Status = ToolChainExecutionStatus.Running
        };

        var result = new ToolChainResult
        {
            ChainId = Id,
            StartTime = DateTimeOffset.UtcNow,
            Status = ToolChainExecutionStatus.Running
        };

        try
        {
            _logger.LogInformation("Starting tool chain execution: {ChainName} ({ChainId})", Name, Id);

            // Execute steps in dependency order
            var executedSteps = new HashSet<string>();
            var stepQueue = GetExecutionOrder();

            foreach (var step in stepQueue)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check if dependencies are satisfied
                if (!AreDependenciesSatisfied(step, executedSteps))
                {
                    continue;
                }

                chainContext.CurrentStep = step;
                chainContext.ReportProgress($"Executing step: {step.Name}", CalculateProgress(executedSteps.Count, _steps.Count));

                try
                {
                    var stepResult = await ExecuteStepWithRetry(step, chainContext, cancellationToken);
                    chainContext.StepResults[step.Id] = stepResult;
                    result.StepResults[step.Id] = stepResult;

                    if (!stepResult.IsSuccessful && step.Type != ToolChainStepType.ErrorHandler)
                    {
                        _logger.LogWarning("Step failed: {StepName} - {Error}", step.Name, stepResult.ErrorMessage);

                        // Check if we should continue or fail the chain
                        if (ShouldFailChain(step))
                        {
                            result.Status = ToolChainExecutionStatus.Failed;
                            result.Errors.Add(new ToolChainError
                            {
                                Code = "STEP_FAILED",
                                Message = $"Critical step '{step.Name}' failed: {stepResult.ErrorMessage}",
                                StepId = step.Id,
                                Exception = stepResult.Exception
                            });
                            break;
                        }
                    }

                    executedSteps.Add(step.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error executing step: {StepName}", step.Name);

                    var stepResult = new ToolChainStepResult
                    {
                        StepId = step.Id,
                        StepName = step.Name,
                        IsSuccessful = false,
                        ErrorMessage = ex.Message,
                        Exception = ex,
                        StartTime = DateTimeOffset.UtcNow,
                        EndTime = DateTimeOffset.UtcNow
                    };

                    chainContext.StepResults[step.Id] = stepResult;
                    result.StepResults[step.Id] = stepResult;
                    result.Errors.Add(new ToolChainError
                    {
                        Code = "STEP_EXCEPTION",
                        Message = ex.Message,
                        StepId = step.Id,
                        Exception = ex
                    });

                    if (ShouldFailChain(step))
                    {
                        result.Status = ToolChainExecutionStatus.Failed;
                        break;
                    }
                }
            }

            // Determine final status
            if (result.Status != ToolChainExecutionStatus.Failed)
            {
                if (executedSteps.Count == _steps.Count && result.FailedSteps == 0)
                {
                    result.Status = ToolChainExecutionStatus.Completed;
                }
                else
                {
                    result.Status = result.SuccessfulSteps > 0 ? ToolChainExecutionStatus.PartiallyCompleted : ToolChainExecutionStatus.Failed;
                }
            }

            // Set final result data
            result.Data = chainContext.PreviousResult;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Tool chain execution cancelled: {ChainName}", Name);
            result.Status = ToolChainExecutionStatus.Cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool chain execution failed: {ChainName}", Name);
            result.Status = ToolChainExecutionStatus.Failed;
            result.Errors.Add(new ToolChainError
            {
                Code = "CHAIN_EXCEPTION",
                Message = ex.Message,
                Exception = ex
            });
        }
        finally
        {
            result.EndTime = DateTimeOffset.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            chainContext.Status = result.Status;

            _logger.LogInformation(
                "Tool chain execution completed: {ChainName} - Status: {Status}, Duration: {Duration:F2}s",
                Name, result.Status, result.Duration.TotalSeconds);
        }

        return result;
    }

    private async Task<ToolChainStepResult> ExecuteStepWithRetry(
        IToolChainStep step,
        ToolChainContext context,
        CancellationToken cancellationToken)
    {
        var maxAttempts = step.IsRetryable ? step.MaxRetries + 1 : 1;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (attempt > 1)
                {
                    _logger.LogInformation("Retrying step: {StepName} (attempt {Attempt}/{MaxAttempts})",
                        step.Name, attempt, maxAttempts);

                    // Exponential backoff
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    await Task.Delay(delay, cancellationToken);
                }

                var result = await step.ExecuteAsync(context, cancellationToken);
                result.RetryAttempts = attempt - 1;

                if (result.IsSuccessful || !step.IsRetryable)
                {
                    return result;
                }

                lastException = result.Exception;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (!step.IsRetryable || attempt == maxAttempts)
                {
                    throw;
                }
            }
        }

        // All retries failed
        return new ToolChainStepResult
        {
            StepId = step.Id,
            StepName = step.Name,
            IsSuccessful = false,
            ErrorMessage = $"Step failed after {maxAttempts} attempts",
            Exception = lastException,
            RetryAttempts = maxAttempts - 1,
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow
        };
    }

    private List<IToolChainStep> GetExecutionOrder()
    {
        // Simple topological sort for dependency ordering
        var sorted = new List<IToolChainStep>();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var step in _steps)
        {
            if (!visited.Contains(step.Id))
            {
                TopologicalSort(step, visited, recursionStack, sorted);
            }
        }

        sorted.Reverse();
        return sorted;
    }

    private void TopologicalSort(
        IToolChainStep step,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<IToolChainStep> sorted)
    {
        visited.Add(step.Id);
        recursionStack.Add(step.Id);

        // Process dependencies first
        foreach (var depId in step.Dependencies)
        {
            var depStep = _steps.FirstOrDefault(s => s.Id == depId);
            if (depStep != null && !visited.Contains(depStep.Id))
            {
                TopologicalSort(depStep, visited, recursionStack, sorted);
            }
        }

        recursionStack.Remove(step.Id);
        sorted.Add(step);
    }

    private bool HasCircularDependency(string stepId, HashSet<string> visited, HashSet<string> recursionStack)
    {
        visited.Add(stepId);
        recursionStack.Add(stepId);

        var step = _steps.FirstOrDefault(s => s.Id == stepId);
        if (step != null)
        {
            foreach (var depId in step.Dependencies)
            {
                if (!visited.Contains(depId))
                {
                    if (HasCircularDependency(depId, visited, recursionStack))
                    {
                        return true;
                    }
                }
                else if (recursionStack.Contains(depId))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(stepId);
        return false;
    }

    private static bool AreDependenciesSatisfied(IToolChainStep step, HashSet<string> executedSteps)
    {
        return step.Dependencies.All(executedSteps.Contains);
    }

    private static bool ShouldFailChain(IToolChainStep step)
    {
        // For now, fail the chain if any non-error-handler step fails
        // This can be made configurable in the future
        return step.Type != ToolChainStepType.ErrorHandler;
    }

    private static double CalculateProgress(int completed, int total)
    {
        return total > 0 ? completed * 100.0 / total : 0;
    }
}

/// <summary>
/// Builder for creating tool chains fluently.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolChainBuilder"/> class.
/// </remarks>
public class ToolChainBuilder(IToolExecutor toolExecutor, ILoggerFactory loggerFactory)
{
    private readonly IToolExecutor _toolExecutor = toolExecutor;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private string _id = Guid.NewGuid().ToString();
    private string _name = "Unnamed Chain";
    private string _description = "";

    /// <summary>
    /// Sets the chain ID.
    /// </summary>
    public ToolChainBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the chain name.
    /// </summary>
    public ToolChainBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the chain description.
    /// </summary>
    public ToolChainBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Builds the tool chain.
    /// </summary>
    public IToolChain Build()
    {
        var logger = _loggerFactory.CreateLogger<ToolChain>();
        return new ToolChain(_id, _name, _description, _toolExecutor, logger);
    }
}
