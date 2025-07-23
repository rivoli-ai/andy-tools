using Andy.Tools.Core;

namespace Andy.Tools.Advanced;

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
