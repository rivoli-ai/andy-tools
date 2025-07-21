using Andy.Tools.Core;

namespace Andy.Tools.Advanced;

/// <summary>
/// Interface for caching tool execution results.
/// </summary>
public interface IToolExecutionCache
{
    /// <summary>
    /// Tries to get a cached result for a tool execution.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached result, or null if not found or expired.</returns>
    public Task<CachedToolResult?> GetAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a tool execution result in the cache.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="result">The result to cache.</param>
    /// <param name="options">Cache options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task SetAsync(string cacheKey, ToolResult result, CacheOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates a cached result.
    /// </summary>
    /// <param name="cacheKey">The cache key to invalidate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cached results matching a pattern.
    /// </summary>
    /// <param name="pattern">The pattern to match (supports wildcards).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of entries invalidated.</returns>
    public Task<int> InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cached results for a specific tool.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of entries invalidated.</returns>
    public Task<int> InvalidateByToolAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached results.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <returns>Cache statistics.</returns>
    public Task<CacheStatistics> GetStatisticsAsync();

    /// <summary>
    /// Generates a cache key for a tool execution.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="parameters">The tool parameters.</param>
    /// <param name="context">Optional context for cache key generation.</param>
    /// <returns>The generated cache key.</returns>
    public string GenerateCacheKey(string toolId, Dictionary<string, object?> parameters, CacheKeyContext? context = null);
}

/// <summary>
/// Represents a cached tool execution result.
/// </summary>
public class CachedToolResult
{
    /// <summary>
    /// Gets or sets the cache key.
    /// </summary>
    public string CacheKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool ID.
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cached result.
    /// </summary>
    public ToolResult Result { get; set; } = null!;

    /// <summary>
    /// Gets or sets when the result was cached.
    /// </summary>
    public DateTimeOffset CachedAt { get; set; }

    /// <summary>
    /// Gets or sets when the cache entry expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the number of times this entry has been accessed.
    /// </summary>
    public int HitCount { get; set; }

    /// <summary>
    /// Gets or sets the last access time.
    /// </summary>
    public DateTimeOffset? LastAccessedAt { get; set; }

    /// <summary>
    /// Gets or sets cache metadata.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];

    /// <summary>
    /// Gets whether this cache entry has expired.
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTimeOffset.UtcNow;
}

/// <summary>
/// Options for caching tool execution results.
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Gets or sets the time-to-live for the cache entry.
    /// </summary>
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>
    /// Gets or sets the absolute expiration time.
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; set; }

    /// <summary>
    /// Gets or sets the sliding expiration time.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets whether to cache failed results.
    /// </summary>
    public bool CacheFailures { get; set; } = false;

    /// <summary>
    /// Gets or sets cache dependencies (other cache keys that, when invalidated, invalidate this entry).
    /// </summary>
    public List<string> Dependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets custom cache metadata.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the cache priority.
    /// </summary>
    public CachePriority Priority { get; set; } = CachePriority.Normal;

    /// <summary>
    /// Creates default cache options.
    /// </summary>
    public static CacheOptions Default => new()
    {
        TimeToLive = TimeSpan.FromMinutes(5),
        CacheFailures = false,
        Priority = CachePriority.Normal
    };

    /// <summary>
    /// Creates cache options for short-lived data.
    /// </summary>
    public static CacheOptions ShortLived => new()
    {
        TimeToLive = TimeSpan.FromMinutes(1),
        Priority = CachePriority.Low
    };

    /// <summary>
    /// Creates cache options for long-lived data.
    /// </summary>
    public static CacheOptions LongLived => new()
    {
        TimeToLive = TimeSpan.FromHours(24),
        Priority = CachePriority.High
    };
}

/// <summary>
/// Cache priority levels.
/// </summary>
public enum CachePriority
{
    /// <summary>Low priority - first to be evicted.</summary>
    Low,
    /// <summary>Normal priority.</summary>
    Normal,
    /// <summary>High priority - last to be evicted.</summary>
    High,
    /// <summary>Never evict (unless explicitly invalidated).</summary>
    NeverEvict
}

/// <summary>
/// Context for cache key generation.
/// </summary>
public class CacheKeyContext
{
    /// <summary>
    /// Gets or sets the user ID for user-specific caching.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the environment for environment-specific caching.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Gets or sets the version for version-specific caching.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets additional context values.
    /// </summary>
    public Dictionary<string, string> AdditionalContext { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to include parameter types in the cache key.
    /// </summary>
    public bool IncludeParameterTypes { get; set; } = false;

    /// <summary>
    /// Gets or sets parameters to exclude from cache key generation.
    /// </summary>
    public HashSet<string> ExcludedParameters { get; set; } = [];
}

/// <summary>
/// Statistics about the cache.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Gets or sets the total number of entries.
    /// </summary>
    public long TotalEntries { get; set; }

    /// <summary>
    /// Gets or sets the total cache size in bytes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of cache hits.
    /// </summary>
    public long HitCount { get; set; }

    /// <summary>
    /// Gets or sets the number of cache misses.
    /// </summary>
    public long MissCount { get; set; }

    /// <summary>
    /// Gets or sets the number of evictions.
    /// </summary>
    public long EvictionCount { get; set; }

    /// <summary>
    /// Gets or sets the number of expired entries.
    /// </summary>
    public long ExpiredCount { get; set; }

    /// <summary>
    /// Gets the cache hit ratio.
    /// </summary>
    public double HitRatio => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;

    /// <summary>
    /// Gets or sets statistics by tool.
    /// </summary>
    public Dictionary<string, ToolCacheStatistics> ToolStatistics { get; set; } = [];

    /// <summary>
    /// Gets or sets the timestamp when statistics were collected.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Cache statistics for a specific tool.
/// </summary>
public class ToolCacheStatistics
{
    /// <summary>
    /// Gets or sets the tool ID.
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of cached entries.
    /// </summary>
    public long EntryCount { get; set; }

    /// <summary>
    /// Gets or sets the total size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of hits.
    /// </summary>
    public long HitCount { get; set; }

    /// <summary>
    /// Gets or sets the number of misses.
    /// </summary>
    public long MissCount { get; set; }

    /// <summary>
    /// Gets or sets the average execution time saved by caching (in milliseconds).
    /// </summary>
    public double AverageTimeSavedMs { get; set; }
}
