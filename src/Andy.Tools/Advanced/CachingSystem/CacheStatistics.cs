namespace Andy.Tools.Advanced.CachingSystem;

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
