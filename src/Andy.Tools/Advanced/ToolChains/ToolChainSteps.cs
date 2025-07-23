using System.Collections.Concurrent;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Advanced.ToolChains;

/// <summary>
/// Base class for tool chain steps.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolChainStepBase"/> class.
/// </remarks>
public abstract class ToolChainStepBase(string id, string name, ToolChainStepType type, IReadOnlyList<string>? dependencies = null) : IToolChainStep
{

    /// <inheritdoc />
    public string Id { get; } = id;

    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public ToolChainStepType Type { get; } = type;

    /// <inheritdoc />
    public IReadOnlyList<string> Dependencies { get; } = dependencies ?? Array.Empty<string>();

    /// <inheritdoc />
    public virtual bool IsRetryable { get; set; } = true;

    /// <inheritdoc />
    public virtual int MaxRetries { get; set; } = 3;

    /// <inheritdoc />
    public abstract Task<ToolChainStepResult> ExecuteAsync(ToolChainContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a successful step result.
    /// </summary>
    protected ToolChainStepResult CreateSuccessResult(object? data, TimeSpan duration, Dictionary<string, object?>? metadata = null)
    {
        return new ToolChainStepResult
        {
            StepId = Id,
            StepName = Name,
            IsSuccessful = true,
            Data = data,
            Duration = duration,
            StartTime = DateTimeOffset.UtcNow.Subtract(duration),
            EndTime = DateTimeOffset.UtcNow,
            Metadata = metadata ?? []
        };
    }

    /// <summary>
    /// Creates a failure step result.
    /// </summary>
    protected ToolChainStepResult CreateFailureResult(string errorMessage, Exception? exception, TimeSpan duration)
    {
        return new ToolChainStepResult
        {
            StepId = Id,
            StepName = Name,
            IsSuccessful = false,
            ErrorMessage = errorMessage,
            Exception = exception,
            Duration = duration,
            StartTime = DateTimeOffset.UtcNow.Subtract(duration),
            EndTime = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Step that executes a tool.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolStep"/> class.
/// </remarks>
public class ToolStep(
    string id,
    string name,
    string toolId,
    Dictionary<string, object?> parameters,
    IToolExecutor toolExecutor,
    IReadOnlyList<string>? dependencies = null) : ToolChainStepBase(id, name, ToolChainStepType.Tool, dependencies)
{
    private readonly string _toolId = toolId;
    private readonly Dictionary<string, object?> _parameters = parameters;
    private readonly IToolExecutor _toolExecutor = toolExecutor;

    /// <inheritdoc />
    public override async Task<ToolChainStepResult> ExecuteAsync(ToolChainContext context, CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            context.ReportProgress($"Executing tool: {_toolId}", 0);

            // Merge parameters with context data
            var mergedParameters = new Dictionary<string, object?>(_parameters);

            // Allow parameter interpolation from previous results
            foreach (var kvp in _parameters)
            {
                if (kvp.Value is string strValue && strValue.StartsWith("{{") && strValue.EndsWith("}}"))
                {
                    var expression = strValue[2..^2].Trim();
                    mergedParameters[kvp.Key] = ResolveExpression(expression, context);
                }
            }

            // Execute the tool
            var executionResult = await _toolExecutor.ExecuteAsync(_toolId, mergedParameters, context.ExecutionContext);

            return executionResult.IsSuccessful
                ? CreateSuccessResult(
                    executionResult.Data,
                    DateTimeOffset.UtcNow - startTime,
                    new Dictionary<string, object?>
                    {
                        ["tool_id"] = _toolId,
                        ["tool_metadata"] = executionResult.Metadata
                    })
                : CreateFailureResult(
                    executionResult.ErrorMessage ?? "Tool execution failed",
                    null,
                    DateTimeOffset.UtcNow - startTime);
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                $"Tool execution failed: {ex.Message}",
                ex,
                DateTimeOffset.UtcNow - startTime);
        }
    }

    private static object? ResolveExpression(string expression, ToolChainContext context)
    {
        // Simple expression resolver - can be enhanced with more complex logic
        if (expression.StartsWith("steps."))
        {
            var parts = expression.Split('.');
            if (parts.Length >= 2)
            {
                var stepId = parts[1];
                if (context.StepResults.TryGetValue(stepId, out var stepResult))
                {
                    if (parts.Length == 2)
                    {
                        return stepResult.Data;
                    }
                    // Could add property navigation here
                }
            }
        }
        else if (expression.StartsWith("params."))
        {
            var paramName = expression[7..];
            if (context.InitialParameters.TryGetValue(paramName, out var value))
            {
                return value;
            }
        }
        else if (expression.StartsWith("state."))
        {
            var stateName = expression[6..];
            if (context.SharedState.TryGetValue(stateName, out var value))
            {
                return value;
            }
        }

        return null;
    }
}

/// <summary>
/// Step that executes conditionally.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConditionalStep"/> class.
/// </remarks>
public class ConditionalStep(
    string id,
    string name,
    Func<ToolChainContext, bool> condition,
    IToolChainStep thenStep,
    IToolChainStep? elseStep = null,
    IReadOnlyList<string>? dependencies = null) : ToolChainStepBase(id, name, ToolChainStepType.Conditional, dependencies)
{
    private readonly Func<ToolChainContext, bool> _condition = condition;
    private readonly IToolChainStep _thenStep = thenStep;
    private readonly IToolChainStep? _elseStep = elseStep;

    /// <inheritdoc />
    public override async Task<ToolChainStepResult> ExecuteAsync(ToolChainContext context, CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            context.ReportProgress("Evaluating condition", 0);

            var conditionResult = _condition(context);
            var stepToExecute = conditionResult ? _thenStep : _elseStep;

            if (stepToExecute == null)
            {
                return CreateSuccessResult(
                    null,
                    DateTimeOffset.UtcNow - startTime,
                    new Dictionary<string, object?>
                    {
                        ["condition_result"] = conditionResult,
                        ["branch_taken"] = conditionResult ? "then" : "none"
                    });
            }

            context.ReportProgress($"Executing {(conditionResult ? "then" : "else")} branch", 50);

            var result = await stepToExecute.ExecuteAsync(context, cancellationToken);

            return new ToolChainStepResult
            {
                StepId = Id,
                StepName = Name,
                IsSuccessful = result.IsSuccessful,
                Data = result.Data,
                ErrorMessage = result.ErrorMessage,
                Exception = result.Exception,
                Duration = DateTimeOffset.UtcNow - startTime,
                StartTime = startTime,
                EndTime = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, object?>
                {
                    ["condition_result"] = conditionResult,
                    ["branch_taken"] = conditionResult ? "then" : "else",
                    ["branch_result"] = result
                }
            };
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                $"Conditional execution failed: {ex.Message}",
                ex,
                DateTimeOffset.UtcNow - startTime);
        }
    }
}

/// <summary>
/// Step that executes multiple sub-steps in parallel.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ParallelStep"/> class.
/// </remarks>
public class ParallelStep(
    string id,
    string name,
    IReadOnlyList<IToolChainStep> steps,
    IReadOnlyList<string>? dependencies = null) : ToolChainStepBase(id, name, ToolChainStepType.Parallel, dependencies)
{
    private readonly IReadOnlyList<IToolChainStep> _steps = steps;

    /// <inheritdoc />
    public override async Task<ToolChainStepResult> ExecuteAsync(ToolChainContext context, CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;
        var results = new ConcurrentDictionary<string, ToolChainStepResult>();

        try
        {
            context.ReportProgress($"Starting parallel execution of {_steps.Count} steps", 0);

            var tasks = _steps.Select(async step =>
            {
                try
                {
                    var result = await step.ExecuteAsync(context, cancellationToken);
                    results[step.Id] = result;
                    return result;
                }
                catch (Exception ex)
                {
                    var errorResult = new ToolChainStepResult
                    {
                        StepId = step.Id,
                        StepName = step.Name,
                        IsSuccessful = false,
                        ErrorMessage = ex.Message,
                        Exception = ex,
                        StartTime = DateTimeOffset.UtcNow,
                        EndTime = DateTimeOffset.UtcNow
                    };
                    results[step.Id] = errorResult;
                    return errorResult;
                }
            });

            var allResults = await Task.WhenAll(tasks);

            var successCount = allResults.Count(r => r.IsSuccessful);
            var isSuccessful = successCount == allResults.Length;

            return new ToolChainStepResult
            {
                StepId = Id,
                StepName = Name,
                IsSuccessful = isSuccessful,
                Data = results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Data),
                ErrorMessage = isSuccessful ? null : $"{allResults.Length - successCount} of {allResults.Length} steps failed",
                Duration = DateTimeOffset.UtcNow - startTime,
                StartTime = startTime,
                EndTime = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, object?>
                {
                    ["total_steps"] = allResults.Length,
                    ["successful_steps"] = successCount,
                    ["failed_steps"] = allResults.Length - successCount,
                    ["step_results"] = results
                }
            };
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                $"Parallel execution failed: {ex.Message}",
                ex,
                DateTimeOffset.UtcNow - startTime);
        }
    }
}

/// <summary>
/// Step that transforms data.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TransformStep"/> class.
/// </remarks>
public class TransformStep(
    string id,
    string name,
    Func<object?, ToolChainContext, Task<object?>> transform,
    IReadOnlyList<string>? dependencies = null) : ToolChainStepBase(id, name, ToolChainStepType.Transform, dependencies)
{
    private readonly Func<object?, ToolChainContext, Task<object?>> _transform = transform;

    /// <inheritdoc />
    public override async Task<ToolChainStepResult> ExecuteAsync(ToolChainContext context, CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            context.ReportProgress("Transforming data", 0);

            var input = context.PreviousResult;
            var output = await _transform(input, context);

            return CreateSuccessResult(
                output,
                DateTimeOffset.UtcNow - startTime,
                new Dictionary<string, object?>
                {
                    ["input_type"] = input?.GetType().Name,
                    ["output_type"] = output?.GetType().Name
                });
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                $"Transform failed: {ex.Message}",
                ex,
                DateTimeOffset.UtcNow - startTime);
        }
    }
}

/// <summary>
/// Step that loops over a collection.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="LoopStep"/> class.
/// </remarks>
public class LoopStep(
    string id,
    string name,
    Func<ToolChainContext, IEnumerable<object?>> itemsProvider,
    IToolChainStep bodyStep,
    string iteratorVariableName = "item",
    IReadOnlyList<string>? dependencies = null) : ToolChainStepBase(id, name, ToolChainStepType.Loop, dependencies)
{
    private readonly Func<ToolChainContext, IEnumerable<object?>> _itemsProvider = itemsProvider;
    private readonly IToolChainStep _bodyStep = bodyStep;
    private readonly string _iteratorVariableName = iteratorVariableName;

    /// <inheritdoc />
    public override async Task<ToolChainStepResult> ExecuteAsync(ToolChainContext context, CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;
        var results = new List<ToolChainStepResult>();

        try
        {
            var items = _itemsProvider(context).ToList();
            context.ReportProgress($"Starting loop over {items.Count} items", 0);

            for (int i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = items[i];

                // Store loop context
                var previousValue = context.SharedState.TryGetValue(_iteratorVariableName, out object? value)
                    ? value : null;

                context.SharedState[_iteratorVariableName] = item;
                context.SharedState[$"{_iteratorVariableName}_index"] = i;

                try
                {
                    context.ReportProgress($"Processing item {i + 1} of {items.Count}", i * 100.0 / items.Count);

                    var result = await _bodyStep.ExecuteAsync(context, cancellationToken);
                    results.Add(result);

                    if (!result.IsSuccessful)
                    {
                        // Optionally break on first failure
                        break;
                    }
                }
                finally
                {
                    // Restore previous value
                    if (previousValue != null)
                    {
                        context.SharedState[_iteratorVariableName] = previousValue;
                    }
                    else
                    {
                        context.SharedState.Remove(_iteratorVariableName);
                    }

                    context.SharedState.Remove($"{_iteratorVariableName}_index");
                }
            }

            var successCount = results.Count(r => r.IsSuccessful);
            var isSuccessful = successCount == results.Count;

            return new ToolChainStepResult
            {
                StepId = Id,
                StepName = Name,
                IsSuccessful = isSuccessful,
                Data = results.Select(r => r.Data).ToList(),
                ErrorMessage = isSuccessful ? null : $"{results.Count - successCount} of {results.Count} iterations failed",
                Duration = DateTimeOffset.UtcNow - startTime,
                StartTime = startTime,
                EndTime = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, object?>
                {
                    ["total_iterations"] = results.Count,
                    ["successful_iterations"] = successCount,
                    ["failed_iterations"] = results.Count - successCount,
                    ["iteration_results"] = results
                }
            };
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                $"Loop execution failed: {ex.Message}",
                ex,
                DateTimeOffset.UtcNow - startTime);
        }
    }
}
