using Andy.Tools.Advanced.MetricsCollection;
using Andy.Tools.Core;
using Andy.Tools.Execution;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Advanced.CachingSystem;

/// <summary>
/// A decorator for IToolExecutor that adds caching capabilities.
/// </summary>
public class CachingToolExecutor : IToolExecutor
{
    private readonly IToolExecutor _innerExecutor;
    private readonly IToolExecutionCache _cache;
    private readonly ILogger<CachingToolExecutor> _logger;
    private readonly IToolMetricsCollector? _metrics;

    public CachingToolExecutor(
        IToolExecutor innerExecutor,
        IToolExecutionCache cache,
        ILogger<CachingToolExecutor> logger,
        IToolMetricsCollector? metrics = null)
    {
        _innerExecutor = innerExecutor ?? throw new ArgumentNullException(nameof(innerExecutor));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;

        // Forward events from inner executor
        _innerExecutor.ExecutionStarted += (sender, args) => ExecutionStarted?.Invoke(this, args);
        _innerExecutor.ExecutionCompleted += (sender, args) => ExecutionCompleted?.Invoke(this, args);
        _innerExecutor.SecurityViolation += (sender, args) => SecurityViolation?.Invoke(this, args);
    }

    public event EventHandler<ToolExecutionStartedEventArgs>? ExecutionStarted;
    public event EventHandler<ToolExecutionCompletedEventArgs>? ExecutionCompleted;
    public event EventHandler<SecurityViolationEventArgs>? SecurityViolation;

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request)
    {
        // Check if caching is enabled in the context
        var enableCaching = request.Context.AdditionalData?.TryGetValue("EnableCaching", out var enabled) == true && 
                           enabled is bool b && b;

        if (!enableCaching)
        {
            _logger.LogDebug("Caching is disabled for this execution");
            return await _innerExecutor.ExecuteAsync(request);
        }

        // Generate cache key. Fold in the working directory and environment variables: a tool's output
        // can depend on them, so two requests with identical parameters but different cwd/env must not
        // collide on the same key. AdditionalContext is incorporated deterministically (sorted) by the cache.
        var cacheKeyContext = new CacheKeyContext
        {
            UserId = request.Context.UserId
        };

        if (!string.IsNullOrEmpty(request.Context.WorkingDirectory))
        {
            cacheKeyContext.AdditionalContext["__cwd"] = request.Context.WorkingDirectory;
        }

        foreach (var kvp in request.Context.Environment)
        {
            cacheKeyContext.AdditionalContext["__env:" + kvp.Key] = kvp.Value ?? string.Empty;
        }

        var cacheKey = _cache.GenerateCacheKey(request.ToolId, request.Parameters, cacheKeyContext);
        _logger.LogDebug("Generated cache key: {CacheKey}", cacheKey);

        // Try to get from cache
        var cachedResult = await _cache.GetAsync(cacheKey, request.Context.CancellationToken);
        if (cachedResult != null && !cachedResult.IsExpired)
        {
            _logger.LogInformation("Cache hit for tool '{ToolId}' with key: {CacheKey}", request.ToolId, cacheKey);
            await RecordCacheMetricAsync(hit: true, request.ToolId, cachedResult.Result.DurationMs ?? 0);

            // Convert cached result to execution result
            var executionResult = new ToolExecutionResult
            {
                ToolId = request.ToolId,
                CorrelationId = request.Context.CorrelationId ?? Guid.NewGuid().ToString("N")[..8],
                StartTime = cachedResult.CachedAt,
                EndTime = cachedResult.CachedAt,
                DurationMs = 0,
                IsSuccessful = cachedResult.Result.IsSuccessful,
                Data = cachedResult.Result.Data,
                ErrorMessage = cachedResult.Result.ErrorMessage,
                // ErrorCode removed
                Metadata = new Dictionary<string, object?>
                {
                    ["cache_hit"] = true,
                    ["cached_at"] = cachedResult.CachedAt,
                    ["hit_count"] = cachedResult.HitCount
                }
            };

            return executionResult;
        }

        _logger.LogDebug("Cache miss for tool '{ToolId}' with key: {CacheKey}", request.ToolId, cacheKey);
        await RecordCacheMetricAsync(hit: false, request.ToolId, 0);

        // Execute the tool
        var result = await _innerExecutor.ExecuteAsync(request);

        // Cache the result if successful or if caching failures is enabled
        if (result.IsSuccessful || ShouldCacheFailures(request.Context))
        {
            var cacheOptions = BuildCacheOptions(request.Context);
            await _cache.SetAsync(cacheKey, new ToolResult
            {
                IsSuccessful = result.IsSuccessful,
                Data = result.Data,
                ErrorMessage = result.ErrorMessage,
                // Preserve the execution duration so a future cache hit can report the time it saved.
                DurationMs = result.DurationMs,
                Metadata = result.Metadata
            }, cacheOptions, request.Context.CancellationToken);

            _logger.LogDebug("Cached result for tool '{ToolId}' with key: {CacheKey}", request.ToolId, cacheKey);
        }

        return result;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string toolId, Dictionary<string, object?> parameters, ToolExecutionContext? context = null)
    {
        context ??= new ToolExecutionContext();
        
        var request = new ToolExecutionRequest
        {
            ToolId = toolId,
            Parameters = parameters,
            Context = context
        };

        return await ExecuteAsync(request);
    }

    public Task<IList<string>> ValidateExecutionRequestAsync(ToolExecutionRequest request)
    {
        return _innerExecutor.ValidateExecutionRequestAsync(request);
    }

    public Task<ToolResourceUsage?> EstimateResourceUsageAsync(string toolId, Dictionary<string, object?> parameters)
    {
        return _innerExecutor.EstimateResourceUsageAsync(toolId, parameters);
    }

    public Task<int> CancelExecutionsAsync(string correlationId)
    {
        return _innerExecutor.CancelExecutionsAsync(correlationId);
    }

    public IReadOnlyList<RunningExecutionInfo> GetRunningExecutions()
    {
        return _innerExecutor.GetRunningExecutions();
    }

    public ToolExecutionStatistics GetStatistics()
    {
        return _innerExecutor.GetStatistics();
    }

    public void Dispose()
    {
        if (_innerExecutor is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task RecordCacheMetricAsync(bool hit, string toolId, double timeSavedMs)
    {
        if (_metrics == null)
        {
            return;
        }

        try
        {
            if (hit)
            {
                await _metrics.RecordCacheHitAsync(toolId, timeSavedMs);
            }
            else
            {
                await _metrics.RecordCacheMissAsync(toolId);
            }
        }
        catch (Exception ex)
        {
            // Metrics are best-effort; never fail an execution because recording failed.
            _logger.LogDebug(ex, "Failed to record cache {Kind} metric for tool '{ToolId}'", hit ? "hit" : "miss", toolId);
        }
    }

    private static bool ShouldCacheFailures(ToolExecutionContext context)
    {
        return context.AdditionalData?.TryGetValue("CacheFailures", out var cacheFailures) == true &&
               cacheFailures is bool b && b;
    }

    private static CacheOptions BuildCacheOptions(ToolExecutionContext context)
    {
        var options = new CacheOptions
        {
            CacheFailures = ShouldCacheFailures(context)
        };

        if (context.AdditionalData != null)
        {
            if (context.AdditionalData.TryGetValue("CacheTimeToLive", out var ttl) && ttl is TimeSpan timeToLive)
            {
                options.TimeToLive = timeToLive;
            }

            if (context.AdditionalData.TryGetValue("CachePriority", out var priority) && priority is string priorityStr)
            {
                if (Enum.TryParse<CachePriority>(priorityStr, out var cachePriority))
                {
                    options.Priority = cachePriority;
                }
            }

            if (context.AdditionalData.TryGetValue("CacheDependencies", out var deps) && deps is IEnumerable<string> dependencies)
            {
                options.Dependencies = dependencies.ToList();
            }
        }

        return options;
    }
}