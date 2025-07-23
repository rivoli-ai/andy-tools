using Andy.Tools.Core;

namespace Andy.Tools.Advanced.MetricsCollection;

/// <summary>
/// Interface for collecting tool execution metrics.
/// </summary>
public interface IToolMetricsCollector
{
    /// <summary>
    /// Records a tool execution.
    /// </summary>
    /// <param name="execution">The execution details.</param>
    public Task RecordExecutionAsync(ToolExecutionMetrics execution);

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="timeSavedMs">Time saved by using cache.</param>
    public Task RecordCacheHitAsync(string toolId, double timeSavedMs);

    /// <summary>
    /// Records a cache miss.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    public Task RecordCacheMissAsync(string toolId);

    /// <summary>
    /// Gets metrics for a specific tool.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="timeRange">Optional time range.</param>
    /// <returns>Tool metrics.</returns>
    public Task<ToolMetrics> GetToolMetricsAsync(string toolId, TimeRange? timeRange = null);

    /// <summary>
    /// Gets metrics for all tools.
    /// </summary>
    /// <param name="timeRange">Optional time range.</param>
    /// <returns>Metrics for all tools.</returns>
    public Task<Dictionary<string, ToolMetrics>> GetAllToolMetricsAsync(TimeRange? timeRange = null);

    /// <summary>
    /// Gets system-wide metrics.
    /// </summary>
    /// <param name="timeRange">Optional time range.</param>
    /// <returns>System metrics.</returns>
    public Task<SystemMetrics> GetSystemMetricsAsync(TimeRange? timeRange = null);

    /// <summary>
    /// Gets performance trends.
    /// </summary>
    /// <param name="toolId">Optional tool ID for tool-specific trends.</param>
    /// <param name="interval">Time interval for aggregation.</param>
    /// <param name="timeRange">Time range.</param>
    /// <returns>Performance trends.</returns>
    public Task<List<PerformanceTrend>> GetPerformanceTrendsAsync(
        string? toolId,
        TimeInterval interval,
        TimeRange timeRange);

    /// <summary>
    /// Exports metrics in a specific format.
    /// </summary>
    /// <param name="format">Export format.</param>
    /// <param name="timeRange">Optional time range.</param>
    /// <returns>Exported metrics data.</returns>
    public Task<string> ExportMetricsAsync(MetricsExportFormat format, TimeRange? timeRange = null);

    /// <summary>
    /// Clears metrics older than the specified age.
    /// </summary>
    /// <param name="olderThan">Age threshold.</param>
    /// <returns>Number of metrics cleared.</returns>
    public Task<int> ClearOldMetricsAsync(TimeSpan olderThan);
}
