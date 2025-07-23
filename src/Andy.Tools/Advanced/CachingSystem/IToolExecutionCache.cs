using Andy.Tools.Core;

namespace Andy.Tools.Advanced.CachingSystem;

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
