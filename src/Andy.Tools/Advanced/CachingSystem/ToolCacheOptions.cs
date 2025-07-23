namespace Andy.Tools.Advanced.CachingSystem;

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
    /// Gets or sets whether to enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of entries per tool.
    /// </summary>
    public int MaxEntriesPerTool { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to use sliding expiration.
    /// </summary>
    public bool UseSlidingExpiration { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to invalidate cache on tool update.
    /// </summary>
    public bool InvalidateOnToolUpdate { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory pressure threshold (0.0 to 1.0).
    /// </summary>
    public double MemoryPressureThreshold { get; set; } = 0.9;

    /// <summary>
    /// Gets or sets whether to enable distributed caching.
    /// </summary>
    public bool EnableDistributedCache { get; set; } = false;

    /// <summary>
    /// Gets or sets the distributed cache connection string.
    /// </summary>
    public string? DistributedCacheConnectionString { get; set; }

    /// <summary>
    /// Gets or sets whether to enable cache compression.
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// Gets or sets the minimum size in bytes for compression.
    /// </summary>
    public long CompressionThresholdBytes { get; set; } = 1024; // 1KB
}