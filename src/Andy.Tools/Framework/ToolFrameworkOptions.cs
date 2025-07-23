using Andy.Tools.Core;
using Andy.Tools.Discovery;

namespace Andy.Tools.Framework;

/// <summary>
/// Configuration options for the tool framework.
/// </summary>
public class ToolFrameworkOptions
{
    /// <summary>
    /// Gets or sets whether to automatically discover and register tools.
    /// </summary>
    public bool AutoDiscoverTools { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically register built-in tools.
    /// </summary>
    public bool RegisterBuiltInTools { get; set; } = true;

    /// <summary>
    /// Gets or sets the discovery options.
    /// </summary>
    public ToolDiscoveryOptions DiscoveryOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the default resource limits for tool execution.
    /// </summary>
    public ToolResourceLimits DefaultResourceLimits { get; set; } = new();

    /// <summary>
    /// Gets or sets the default permissions for tool execution.
    /// </summary>
    public ToolPermissions DefaultPermissions { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to enable security checks.
    /// </summary>
    public bool EnableSecurity { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable resource monitoring.
    /// </summary>
    public bool EnableResourceMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable observability features.
    /// </summary>
    public bool EnableObservability { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable detailed activity tracing.
    /// </summary>
    public bool EnableDetailedTracing { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to export metrics to external systems.
    /// </summary>
    public bool EnableMetricsExport { get; set; } = true;

    /// <summary>
    /// Gets or sets the metrics aggregation interval.
    /// </summary>
    public TimeSpan MetricsAggregationInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the observability data retention period.
    /// </summary>
    public TimeSpan ObservabilityRetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the maximum age for security violations before they are cleaned up.
    /// </summary>
    public TimeSpan SecurityViolationMaxAge { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets directories to scan for plugin tools.
    /// </summary>
    public List<string> PluginDirectories { get; set; } = [];
}
