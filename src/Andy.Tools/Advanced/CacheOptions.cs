namespace Andy.Tools.Advanced;

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
