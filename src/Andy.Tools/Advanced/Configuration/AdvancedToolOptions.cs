namespace Andy.Tools.Advanced.Configuration;

/// <summary>
/// Configuration options for advanced tool features.
/// </summary>
public class AdvancedToolOptions
{
    /// <summary>
    /// Gets or sets whether to enable caching.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable lazy loading.
    /// </summary>
    public bool EnableLazyLoading { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable metrics collection.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable detailed metrics.
    /// </summary>
    public bool EnableDetailedMetrics { get; set; } = false;

    /// <summary>
    /// Gets or sets the environment name.
    /// </summary>
    public string Environment { get; set; } = "production";

    /// <summary>
    /// Gets or sets the default cache time-to-live.
    /// </summary>
    public TimeSpan CacheTimeToLive { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum cache size in bytes.
    /// </summary>
    public long MaxCacheSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Gets or sets the maximum metrics per tool.
    /// </summary>
    public int MaxMetricsPerTool { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the metrics retention period.
    /// </summary>
    public TimeSpan MetricsRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}