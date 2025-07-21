using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andy.Tools.Advanced;

/// <summary>
/// In-memory implementation of tool execution cache.
/// </summary>
public partial class MemoryToolExecutionCache : IToolExecutionCache, IDisposable
{
    private readonly SimpleMemoryCache _cache;
    private readonly ILogger<MemoryToolExecutionCache> _logger;
    private readonly ToolCacheOptions _options;
    private readonly ConcurrentDictionary<string, CacheEntryMetadata> _metadata = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _dependencies = new();
    private readonly Timer _cleanupTimer;
    private long _hitCount;
    private long _missCount;
    private long _evictionCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryToolExecutionCache"/> class.
    /// </summary>
    public MemoryToolExecutionCache(
        IOptions<ToolCacheOptions> options,
        ILogger<MemoryToolExecutionCache> logger)
    {
        _cache = new SimpleMemoryCache(options.Value.MaxSizeBytes, options.Value.CleanupInterval);
        _options = options.Value;
        _logger = logger;

        // Start cleanup timer
        _cleanupTimer = new Timer(
            CleanupExpiredEntries,
            null,
            _options.CleanupInterval,
            _options.CleanupInterval);
    }

    /// <inheritdoc />
    public async Task<CachedToolResult?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_cache.TryGetValue(cacheKey, out CachedToolResult? cachedResult))
            {
                if (cachedResult != null && !cachedResult.IsExpired)
                {
                    Interlocked.Increment(ref _hitCount);
                    cachedResult.HitCount++;
                    cachedResult.LastAccessedAt = DateTimeOffset.UtcNow;

                    // Update sliding expiration if configured
                    if (_metadata.TryGetValue(cacheKey, out var metadata) && metadata.SlidingExpiration.HasValue)
                    {
                        var newExpiration = DateTimeOffset.UtcNow.Add(metadata.SlidingExpiration.Value);
                        cachedResult.ExpiresAt = newExpiration;

                        // Re-cache with new expiration
                        await SetInternalAsync(cacheKey, cachedResult, metadata);
                    }

                    _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
                    return cachedResult;
                }
            }

            Interlocked.Increment(ref _missCount);
            _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cached result for key: {CacheKey}", cacheKey);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(string cacheKey, ToolResult result, CacheOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            // Don't cache failures unless explicitly configured
            if (!result.IsSuccessful && !options.CacheFailures)
            {
                return;
            }

            var cachedResult = new CachedToolResult
            {
                CacheKey = cacheKey,
                ToolId = ExtractToolIdFromKey(cacheKey),
                Result = result,
                CachedAt = DateTimeOffset.UtcNow,
                HitCount = 0,
                Metadata = options.Metadata
            };

            // Calculate expiration
            if (options.AbsoluteExpiration.HasValue)
            {
                cachedResult.ExpiresAt = options.AbsoluteExpiration.Value;
            }
            else if (options.TimeToLive.HasValue)
            {
                cachedResult.ExpiresAt = DateTimeOffset.UtcNow.Add(options.TimeToLive.Value);
            }
            else
            {
                cachedResult.ExpiresAt = options.SlidingExpiration.HasValue
                    ? DateTimeOffset.UtcNow.Add(options.SlidingExpiration.Value)
                    : DateTimeOffset.UtcNow.Add(_options.DefaultTimeToLive);
            }

            var metadata = new CacheEntryMetadata
            {
                CacheKey = cacheKey,
                ToolId = cachedResult.ToolId,
                Priority = options.Priority,
                Dependencies = [.. options.Dependencies],
                SlidingExpiration = options.SlidingExpiration,
                SizeBytes = EstimateSize(result)
            };

            await SetInternalAsync(cacheKey, cachedResult, metadata);

            // Register dependencies
            foreach (var dependency in options.Dependencies)
            {
                _dependencies.AddOrUpdate(dependency,
                    [cacheKey],
                    (_, set) =>
                    {
                        set.Add(cacheKey);
                        return set;
                    });
            }

            _logger.LogDebug("Cached result for key: {CacheKey}, expires at: {ExpiresAt}", cacheKey, cachedResult.ExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching result for key: {CacheKey}", cacheKey);
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _cache.Remove(cacheKey);
            _metadata.TryRemove(cacheKey, out _);

            // Invalidate dependent entries
            if (_dependencies.TryGetValue(cacheKey, out var dependents))
            {
                foreach (var dependent in dependents)
                {
                    await InvalidateAsync(dependent, cancellationToken);
                }

                _dependencies.TryRemove(cacheKey, out _);
            }

            _logger.LogDebug("Invalidated cache key: {CacheKey}", cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache key: {CacheKey}", cacheKey);
        }
    }

    /// <inheritdoc />
    public async Task<int> InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            var regex = ConvertPatternToRegex(pattern);
            var invalidatedCount = 0;

            foreach (var kvp in _metadata)
            {
                if (regex.IsMatch(kvp.Key))
                {
                    await InvalidateAsync(kvp.Key, cancellationToken);
                    invalidatedCount++;
                }
            }

            _logger.LogInformation("Invalidated {Count} cache entries matching pattern: {Pattern}", invalidatedCount, pattern);
            return invalidatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache by pattern: {Pattern}", pattern);
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<int> InvalidateByToolAsync(string toolId, CancellationToken cancellationToken = default)
    {
        try
        {
            var invalidatedCount = 0;

            foreach (var kvp in _metadata.Where(m => m.Value.ToolId == toolId))
            {
                await InvalidateAsync(kvp.Key, cancellationToken);
                invalidatedCount++;
            }

            _logger.LogInformation("Invalidated {Count} cache entries for tool: {ToolId}", invalidatedCount, toolId);
            return invalidatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for tool: {ToolId}", toolId);
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Clear all entries from cache
            _cache.Clear();
            _metadata.Clear();
            _dependencies.Clear();

            _logger.LogInformation("Cleared all cache entries");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<CacheStatistics> GetStatisticsAsync()
    {
        var stats = new CacheStatistics
        {
            TotalEntries = _metadata.Count,
            TotalSizeBytes = _metadata.Values.Sum(m => m.SizeBytes),
            HitCount = _hitCount,
            MissCount = _missCount,
            EvictionCount = _evictionCount,
            ExpiredCount = _metadata.Values.Count(m => m.ExpiresAt <= DateTimeOffset.UtcNow)
        };

        // Group by tool
        var toolGroups = _metadata.Values.GroupBy(m => m.ToolId);
        foreach (var group in toolGroups)
        {
            var toolStats = new ToolCacheStatistics
            {
                ToolId = group.Key,
                EntryCount = group.Count(),
                SizeBytes = group.Sum(m => m.SizeBytes)
            };

            // Calculate tool-specific hit/miss counts (would need more detailed tracking)
            stats.ToolStatistics[group.Key] = toolStats;
        }

        await Task.CompletedTask;
        return stats;
    }

    /// <inheritdoc />
    public string GenerateCacheKey(string toolId, Dictionary<string, object?> parameters, CacheKeyContext? context = null)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append($"tool:{toolId}");

        // Add context elements
        if (context != null)
        {
            if (!string.IsNullOrEmpty(context.UserId))
            {
                keyBuilder.Append($":user:{context.UserId}");
            }

            if (!string.IsNullOrEmpty(context.Environment))
            {
                keyBuilder.Append($":env:{context.Environment}");
            }

            if (!string.IsNullOrEmpty(context.Version))
            {
                keyBuilder.Append($":v:{context.Version}");
            }

            foreach (var kvp in context.AdditionalContext.OrderBy(k => k.Key))
            {
                keyBuilder.Append($":{kvp.Key}:{kvp.Value}");
            }
        }

        // Sort and hash parameters
        var sortedParams = parameters
            .Where(p => context?.ExcludedParameters?.Contains(p.Key) != true)
            .OrderBy(p => p.Key)
            .Select(p =>
            {
                var valueStr = SerializeParameterValue(p.Value, context?.IncludeParameterTypes ?? false);
                return $"{p.Key}={valueStr}";
            });

        var paramsString = string.Join("&", sortedParams);

        if (!string.IsNullOrEmpty(paramsString))
        {
            // Hash long parameter strings
            if (paramsString.Length > 200)
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(paramsString));
                var hashString = Convert.ToBase64String(hash);
                keyBuilder.Append($":params:{hashString}");
            }
            else
            {
                keyBuilder.Append($":params:{paramsString}");
            }
        }

        return keyBuilder.ToString();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _cache?.Dispose();
    }

    private async Task SetInternalAsync(string cacheKey, CachedToolResult cachedResult, CacheEntryMetadata metadata)
    {
        var cacheEntryOptions = new SimpleCacheEntryOptions
        {
            Priority = metadata.Priority,
            SizeBytes = metadata.SizeBytes
        };

        if (cachedResult.ExpiresAt.HasValue)
        {
            cacheEntryOptions.AbsoluteExpiration = cachedResult.ExpiresAt.Value;
        }

        if (metadata.SlidingExpiration.HasValue)
        {
            cacheEntryOptions.SlidingExpiration = metadata.SlidingExpiration;
        }

        cacheEntryOptions.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
        {
            EvictionCallback = OnCacheEviction,
            State = cacheKey
        });

        _cache.Set(cacheKey, cachedResult, cacheEntryOptions);
        _metadata[cacheKey] = metadata;

        await Task.CompletedTask;
    }

    private void OnCacheEviction(object key, object? value, EvictionReason reason, object? state)
    {
        var cacheKey = key?.ToString() ?? "";
        _metadata.TryRemove(cacheKey, out _);
        Interlocked.Increment(ref _evictionCount);

        _logger.LogDebug("Cache entry evicted: {CacheKey}, reason: {Reason}", cacheKey, reason);
    }

    private static string ExtractToolIdFromKey(string cacheKey)
    {
        var match = MyRegex().Match(cacheKey);
        return match.Success ? match.Groups[1].Value : "unknown";
    }

    private static Regex ConvertPatternToRegex(string pattern)
    {
        var regexPattern = Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".");
        return new Regex($"^{regexPattern}$", RegexOptions.IgnoreCase);
    }

    private static string SerializeParameterValue(object? value, bool includeType)
    {
        if (value == null)
        {
            return "null";
        }

        var valueStr = value switch
        {
            string s => s,
            IEnumerable<object> enumerable => JsonSerializer.Serialize(enumerable),
            _ => JsonSerializer.Serialize(value)
        };

        return includeType ? $"{value.GetType().Name}:{valueStr}" : valueStr;
    }

    private static long EstimateSize(ToolResult result)
    {
        try
        {
            var json = JsonSerializer.Serialize(result);
            return Encoding.UTF8.GetByteCount(json);
        }
        catch
        {
            return 0;
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            var expiredKeys = _metadata
                .Where(kvp => kvp.Value.ExpiresAt <= DateTimeOffset.UtcNow)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.Remove(key);
                _metadata.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
    }

    private class CacheEntryMetadata
    {
        public string CacheKey { get; set; } = string.Empty;
        public string ToolId { get; set; } = string.Empty;
        public CachePriority Priority { get; set; }
        public HashSet<string> Dependencies { get; set; } = [];
        public TimeSpan? SlidingExpiration { get; set; }
        public long SizeBytes { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }

    [GeneratedRegex(@"tool:([^:]+)")]
    private static partial Regex MyRegex();
}

/// <summary>
/// Configuration options for tool caching.
/// </summary>
public class ToolCacheOptions
{
    /// <summary>
    /// Gets or sets the default time-to-live for cache entries.
    /// </summary>
    public TimeSpan DefaultTimeToLive { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum cache size in bytes.
    /// </summary>
    public long MaxSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Gets or sets the interval for cleaning up expired entries.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to enable cache statistics tracking.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of entries per tool.
    /// </summary>
    public int MaxEntriesPerTool { get; set; } = 1000;
}
