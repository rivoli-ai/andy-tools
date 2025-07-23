namespace Andy.Tools.Advanced;

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
