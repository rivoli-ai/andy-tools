using System.Collections.Concurrent;
using System.Diagnostics;
using Andy.Tools.Core;
using Andy.Tools.Core.OutputLimiting;
using Andy.Tools.Observability;
using Andy.Tools.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Execution;

/// <summary>
/// Secure tool executor with resource monitoring and sandboxing.
/// </summary>
public class ToolExecutor : IToolExecutor, IDisposable
{
    private readonly IToolRegistry _registry;
    private readonly IToolValidator _validator;
    private readonly ISecurityManager _securityManager;
    private readonly IResourceMonitor _resourceMonitor;
    private readonly IToolOutputLimiter _outputLimiter;
    private readonly IToolObservabilityService? _observabilityService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ToolExecutor> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningExecutions = new();
    private readonly ToolExecutionStatistics _statistics = new();
    private readonly object _statsLock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolExecutor"/> class.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="validator">The tool validator.</param>
    /// <param name="securityManager">The security manager.</param>
    /// <param name="resourceMonitor">The resource monitor.</param>
    /// <param name="outputLimiter">The output limiter.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="logger">The logger.</param>
    public ToolExecutor(
        IToolRegistry registry,
        IToolValidator validator,
        ISecurityManager securityManager,
        IResourceMonitor resourceMonitor,
        IToolOutputLimiter outputLimiter,
        IServiceProvider serviceProvider,
        ILogger<ToolExecutor> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _securityManager = securityManager ?? throw new ArgumentNullException(nameof(securityManager));
        _resourceMonitor = resourceMonitor ?? throw new ArgumentNullException(nameof(resourceMonitor));
        _outputLimiter = outputLimiter ?? throw new ArgumentNullException(nameof(outputLimiter));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Try to get observability service (optional)
        _observabilityService = serviceProvider.GetService<IToolObservabilityService>();

        // Subscribe to resource limit events
        _resourceMonitor.ResourceLimitExceeded += OnResourceLimitExceeded;
    }

    /// <inheritdoc />
    public event EventHandler<ToolExecutionStartedEventArgs>? ExecutionStarted;

    /// <inheritdoc />
    public event EventHandler<ToolExecutionCompletedEventArgs>? ExecutionCompleted;

    /// <inheritdoc />
    public event EventHandler<SecurityViolationEventArgs>? SecurityViolation;

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ToolExecutor));
        }

        ArgumentNullException.ThrowIfNull(request);

        var correlationId = request.Context.CorrelationId;
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N")[..8];
            request.Context.CorrelationId = correlationId;
        }

        var stopwatch = Stopwatch.StartNew();
        var result = new ToolExecutionResult
        {
            ToolId = request.ToolId,
            CorrelationId = correlationId,
            StartTime = DateTimeOffset.UtcNow
        };

        IResourceMonitoringSession? monitoringSession = null;
        ITool? tool = null;
        Activity? activity = null;
        CancellationTokenSource? executionCts = null;

        try
        {
            // Get tool registration
            var registration = _registry.GetTool(request.ToolId);
            if (registration == null)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = $"Tool '{request.ToolId}' not found";
                return result;
            }

            if (!registration.IsEnabled)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = $"Tool '{request.ToolId}' is disabled";
                return result;
            }

            // Validate execution request
            if (request.ValidateParameters || request.EnforcePermissions)
            {
                var validation = await ValidateExecutionRequestAsync(request);
                if (validation.Count > 0)
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = $"Validation failed: {string.Join(", ", validation)}";
                    return result;
                }
            }

            // Check security permissions
            if (request.EnforcePermissions)
            {
                var securityViolations = _securityManager.ValidateExecution(registration.Metadata, request.Context.Permissions);
                if (securityViolations.Count > 0)
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = $"Security validation failed: {string.Join(", ", securityViolations)}";
                    result.SecurityViolations = [.. securityViolations];

                    foreach (var violation in securityViolations)
                    {
                        _securityManager.RecordViolation(request.ToolId, correlationId, violation, SecurityViolationSeverity.High);
                        SecurityViolation?.Invoke(this, new SecurityViolationEventArgs(request.ToolId, correlationId, violation, SecurityViolationSeverity.High));
                    }

                    return result;
                }
            }

            // Start resource monitoring
            if (request.EnforceResourceLimits)
            {
                monitoringSession = _resourceMonitor.StartMonitoring(correlationId, request.Context.ResourceLimits);
            }

            // Set up cancellation
            executionCts = new CancellationTokenSource();
            if (request.TimeoutMs.HasValue)
            {
                executionCts.CancelAfter(request.TimeoutMs.Value);
            }
            else if (request.Context.ResourceLimits.MaxExecutionTimeMs > 0)
            {
                executionCts.CancelAfter(request.Context.ResourceLimits.MaxExecutionTimeMs);
            }

            // Combine with context cancellation token
            var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                executionCts.Token,
                request.Context.CancellationToken);
            request.Context.CancellationToken = combinedCts.Token;

            // Track running execution
            _runningExecutions.TryAdd(correlationId, executionCts);

            // Create tool instance
            tool = _registry.CreateTool(request.ToolId, _serviceProvider);
            if (tool == null)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = $"Failed to create instance of tool '{request.ToolId}'";
                return result;
            }

            // Initialize tool
            await tool.InitializeAsync(registration.Configuration, request.Context.CancellationToken);

            // Start observability tracking
            activity = _observabilityService?.StartToolExecution(request.ToolId, request.Parameters, request.Context);

            // Raise execution started event
            ExecutionStarted?.Invoke(this, new ToolExecutionStartedEventArgs(request.ToolId, correlationId, request.Context));

            // Execute the tool
            _logger.LogInformation("Executing tool '{ToolName}' (ID: {ToolId}) with correlation ID {CorrelationId}",
                registration.Metadata.Name, request.ToolId, correlationId);

            var toolResult = await tool.ExecuteAsync(request.Parameters, request.Context);

            // Apply output limiting if needed
            if (toolResult.IsSuccessful && toolResult.Data != null)
            {
                var outputType = DetermineOutputType(request.ToolId, registration.Metadata);
                if (_outputLimiter.NeedsLimiting(toolResult.Data, outputType))
                {
                    var limitContext = new OutputLimitContext
                    {
                        MaxCharacters = request.Context.ResourceLimits.MaxOutputSizeBytes > 0
                            ? (int)(request.Context.ResourceLimits.MaxOutputSizeBytes / 2) // Approximate chars from bytes
                            : null,
                        IncludeSummary = true,
                        ProvideSuggestions = true,
                        ToolContext = new Dictionary<string, object>
                        {
                            ["tool_id"] = request.ToolId,
                            ["tool_name"] = registration.Metadata.Name
                        }
                    };

                    var limitedOutput = _outputLimiter.LimitOutput(toolResult.Data, outputType, limitContext);

                    // Update the result with limited output
                    result.Data = limitedOutput.Content;
                    result.Metadata["output_truncated"] = limitedOutput.WasTruncated;

                    if (limitedOutput.WasTruncated)
                    {
                        result.Metadata["truncation_info"] = new
                        {
                            original_size = limitedOutput.OriginalSize,
                            truncated_size = limitedOutput.TruncatedSize,
                            reason = limitedOutput.TruncationReason,
                            summary = limitedOutput.Summary,
                            suggestions = limitedOutput.Suggestions,
                            message = limitedOutput.GetTruncationMessage()
                        };

                        _logger.LogInformation("Tool output truncated for '{ToolName}': {OriginalSize} -> {TruncatedSize} bytes",
                            registration.Metadata.Name, limitedOutput.OriginalSize, limitedOutput.TruncatedSize);
                    }
                }
                else
                {
                    result.Data = toolResult.Data;
                }
            }
            else
            {
                result.Data = toolResult.Data;
            }

            // Update result
            result.IsSuccessful = toolResult.IsSuccessful;
            result.ErrorMessage = toolResult.ErrorMessage;
            result.Metadata = new Dictionary<string, object?>(toolResult.Metadata);
            result.DurationMs = toolResult.DurationMs;

            _logger.LogInformation("Tool execution completed for '{ToolName}' (ID: {ToolId}): Success={Success}, Duration={Duration}ms",
                registration.Metadata.Name, request.ToolId, result.IsSuccessful, result.DurationMs);

            // Update statistics
            UpdateStatistics(request.ToolId, request.Context.UserId, result.IsSuccessful, false, result.DurationMs ?? 0);
        }
        catch (OperationCanceledException)
        {
            result.IsSuccessful = false;
            result.WasCancelled = true;
            result.ErrorMessage = "Tool execution was cancelled";

            _logger.LogWarning("Tool execution cancelled for '{ToolId}' with correlation ID {CorrelationId}",
                request.ToolId, correlationId);

            UpdateStatistics(request.ToolId, request.Context.UserId, false, true, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;

            _logger.LogError(ex, "Tool execution failed for '{ToolId}' with correlation ID {CorrelationId}",
                request.ToolId, correlationId);

            // Record error in observability
            _observabilityService?.RecordToolError(activity, request.ToolId, ex);

            UpdateStatistics(request.ToolId, request.Context.UserId, false, false, stopwatch.Elapsed.TotalMilliseconds);
        }
        finally
        {
            stopwatch.Stop();
            result.EndTime = DateTimeOffset.UtcNow;
            if (!result.DurationMs.HasValue)
            {
                result.DurationMs = stopwatch.Elapsed.TotalMilliseconds;
            }

            // Clean up resources
            try
            {
                if (tool != null)
                {
                    await tool.DisposeAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose tool '{ToolId}' after execution", request.ToolId);
            }

            // Stop resource monitoring and get final usage
            if (monitoringSession != null)
            {
                result.ResourceUsage = _resourceMonitor.StopMonitoring(correlationId);
                result.HitResourceLimits = monitoringSession.HasExceededLimits;
                if (monitoringSession.HasExceededLimits)
                {
                    result.Metadata["exceeded_limits"] = monitoringSession.ExceededLimits.ToList();
                }
            }

            // Remove from running executions
            _runningExecutions.TryRemove(correlationId, out _);
            executionCts?.Dispose();

            // Get security violations
            var violations = _securityManager.GetViolations(correlationId);
            if (violations.Count > 0)
            {
                result.SecurityViolations = [.. violations.Select(v => v.Description)];
            }

            // Complete observability tracking
            _observabilityService?.CompleteToolExecution(activity, result);

            // Record any security events
            if (violations.Count > 0)
            {
                foreach (var violation in violations)
                {
                    _observabilityService?.RecordSecurityEvent(
                        request.ToolId,
                        "SecurityViolation",
                        new Dictionary<string, object?>
                        {
                            ["violation"] = violation.Description,
                            ["severity"] = violation.Severity.ToString(),
                            ["user"] = request.Context.UserId,
                            ["correlation_id"] = correlationId
                        });
                }
            }

            // Raise execution completed event
            ExecutionCompleted?.Invoke(this, new ToolExecutionCompletedEventArgs(result));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteAsync(string toolId, Dictionary<string, object?> parameters, ToolExecutionContext? context = null)
    {
        // TEMPORARY: Console logging for tool invocations
        var parametersJson = System.Text.Json.JsonSerializer.Serialize(parameters, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
        Console.WriteLine($"[TOOL] Executing: {toolId} | Parameters: {parametersJson}");

        var request = new ToolExecutionRequest
        {
            ToolId = toolId,
            Parameters = parameters,
            Context = context ?? new ToolExecutionContext()
        };

        return await ExecuteAsync(request);
    }

    /// <inheritdoc />
    public async Task<IList<string>> ValidateExecutionRequestAsync(ToolExecutionRequest request)
    {
        var errors = new List<string>();

        // Get tool registration
        var registration = _registry.GetTool(request.ToolId);
        if (registration == null)
        {
            errors.Add($"Tool '{request.ToolId}' not found");
            return errors;
        }

        // Validate using the validator
        var validationResult = _validator.ValidateExecutionRequest(request, registration.Metadata);
        if (!validationResult.IsValid)
        {
            errors.AddRange(validationResult.Errors.Select(e => e.Message));
        }

        await Task.CompletedTask; // Make this async for future enhancements
        return errors;
    }

    /// <inheritdoc />
    public async Task<ToolResourceUsage?> EstimateResourceUsageAsync(string toolId, Dictionary<string, object?> parameters)
    {
        // This is a placeholder implementation
        // In a real implementation, this could analyze the tool and parameters to provide estimates
        await Task.CompletedTask;
        return null;
    }

    /// <inheritdoc />
    public async Task<int> CancelExecutionsAsync(string correlationId)
    {
        if (string.IsNullOrEmpty(correlationId))
        {
            return 0;
        }

        var cancelledCount = 0;
        var toCancel = _runningExecutions
            .Where(kvp => kvp.Key.Equals(correlationId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var kvp in toCancel)
        {
            try
            {
                kvp.Value.Cancel();
                cancelledCount++;
                _logger.LogInformation("Cancelled execution for correlation ID {CorrelationId}", kvp.Key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cancel execution for correlation ID {CorrelationId}", kvp.Key);
            }
        }

        await Task.CompletedTask;
        return cancelledCount;
    }

    /// <inheritdoc />
    public IReadOnlyList<RunningExecutionInfo> GetRunningExecutions()
    {
        var runningExecutions = new List<RunningExecutionInfo>();
        var monitoringSessions = _resourceMonitor.GetActiveSessions();

        foreach (var session in monitoringSessions)
        {
            var info = new RunningExecutionInfo
            {
                CorrelationId = session.CorrelationId,
                StartTime = session.StartTime,
                CurrentResourceUsage = session.CurrentUsage
            };

            runningExecutions.Add(info);
        }

        return runningExecutions.AsReadOnly();
    }

    /// <inheritdoc />
    public ToolExecutionStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new ToolExecutionStatistics
            {
                TotalExecutions = _statistics.TotalExecutions,
                SuccessfulExecutions = _statistics.SuccessfulExecutions,
                FailedExecutions = _statistics.FailedExecutions,
                CancelledExecutions = _statistics.CancelledExecutions,
                AverageExecutionTimeMs = _statistics.AverageExecutionTimeMs,
                SecurityViolations = _statistics.SecurityViolations,
                ResourceLimitViolations = _statistics.ResourceLimitViolations,
                ExecutionsByTool = new Dictionary<string, long>(_statistics.ExecutionsByTool),
                ExecutionsByUser = new Dictionary<string, long>(_statistics.ExecutionsByUser),
                GeneratedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private void UpdateStatistics(string toolId, string? userId, bool success, bool cancelled, double durationMs)
    {
        lock (_statsLock)
        {
            _statistics.TotalExecutions++;

            if (success)
            {
                _statistics.SuccessfulExecutions++;
            }
            else if (cancelled)
            {
                _statistics.CancelledExecutions++;
            }
            else
            {
                _statistics.FailedExecutions++;
            }

            // Update average execution time
            var totalMs = (_statistics.AverageExecutionTimeMs * (_statistics.TotalExecutions - 1)) + durationMs;
            _statistics.AverageExecutionTimeMs = totalMs / _statistics.TotalExecutions;

            // Update by tool
            _statistics.ExecutionsByTool.TryGetValue(toolId, out var toolCount);
            _statistics.ExecutionsByTool[toolId] = toolCount + 1;

            // Update by user
            if (!string.IsNullOrEmpty(userId))
            {
                _statistics.ExecutionsByUser.TryGetValue(userId, out var userCount);
                _statistics.ExecutionsByUser[userId] = userCount + 1;
            }
        }
    }

    private void OnResourceLimitExceeded(object? sender, ResourceLimitExceededEventArgs e)
    {
        lock (_statsLock)
        {
            _statistics.ResourceLimitViolations++;
        }

        _logger.LogWarning("Resource limit exceeded for correlation {CorrelationId}: {LimitType} ({Current} > {Limit})",
            e.CorrelationId, e.LimitType, e.CurrentValue, e.LimitValue);

        // Cancel the execution if it's still running
        if (_runningExecutions.TryGetValue(e.CorrelationId, out var cts))
        {
            try
            {
                cts.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cancel execution after resource limit exceeded for correlation {CorrelationId}", e.CorrelationId);
            }
        }
    }

    private OutputType DetermineOutputType(string toolId, ToolMetadata metadata)
    {
        // Determine output type based on tool ID and metadata
        var toolIdLower = toolId.ToLowerInvariant();
        var toolNameLower = metadata.Name.ToLowerInvariant();

        // File listing tools
        if (toolIdLower.Contains("list") && (toolIdLower.Contains("dir") || toolIdLower.Contains("file")))
        {
            return OutputType.FileList;
        }

        // File content tools
        if (toolIdLower.Contains("read") && toolIdLower.Contains("file"))
        {
            return OutputType.FileContent;
        }

        // Directory tree tools
        if (toolIdLower.Contains("tree") || (toolIdLower.Contains("dir") && toolIdLower.Contains("structure")))
        {
            return OutputType.DirectoryTree;
        }

        // Log or console output
        if (toolIdLower.Contains("log") || toolIdLower.Contains("console") || toolIdLower.Contains("output"))
        {
            return OutputType.Logs;
        }

        // Check by category
        if (metadata.Category == ToolCategory.FileSystem)
        {
            if (toolNameLower.Contains("list"))
            {
                return OutputType.FileList;
            }

            if (toolNameLower.Contains("read"))
            {
                return OutputType.FileContent;
            }
        }

        // Default to text for most tools
        return OutputType.Text;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            // Cancel all running executions
            foreach (var cts in _runningExecutions.Values)
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cancel execution during disposal");
                }
            }

            _runningExecutions.Clear();

            // Unsubscribe from events
            if (_resourceMonitor != null)
            {
                _resourceMonitor.ResourceLimitExceeded -= OnResourceLimitExceeded;
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
