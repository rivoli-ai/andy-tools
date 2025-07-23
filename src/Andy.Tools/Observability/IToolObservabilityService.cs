using System.Diagnostics;
using Andy.Tools.Core;

namespace Andy.Tools.Observability;

/// <summary>
/// Provides observability services for tool execution including metrics, tracing, and logging.
/// </summary>
public interface IToolObservabilityService
{
    /// <summary>
    /// Records the start of a tool execution.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="parameters">The execution parameters.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>An activity representing the tool execution.</returns>
    public Activity? StartToolExecution(string toolId, Dictionary<string, object?> parameters, ToolExecutionContext context);

    /// <summary>
    /// Records the completion of a tool execution.
    /// </summary>
    /// <param name="activity">The activity to complete.</param>
    /// <param name="result">The execution result.</param>
    public void CompleteToolExecution(Activity? activity, ToolExecutionResult result);

    /// <summary>
    /// Records a tool execution error.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="exception">The exception.</param>
    public void RecordToolError(Activity? activity, string toolId, Exception exception);

    /// <summary>
    /// Records tool usage metrics.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="success">Whether the execution was successful.</param>
    /// <param name="resourceUsage">Resource usage information.</param>
    public void RecordToolUsage(string toolId, TimeSpan duration, bool success, ToolResourceUsage? resourceUsage = null);

    /// <summary>
    /// Records a security event.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="eventType">The security event type.</param>
    /// <param name="details">Event details.</param>
    public void RecordSecurityEvent(string toolId, string eventType, Dictionary<string, object?> details);

    /// <summary>
    /// Gets tool performance statistics.
    /// </summary>
    /// <param name="toolId">Optional tool ID to filter by.</param>
    /// <param name="timeRange">Time range for statistics.</param>
    /// <returns>Performance statistics.</returns>
    public Task<ToolPerformanceStatistics> GetPerformanceStatisticsAsync(string? toolId = null, TimeSpan? timeRange = null);

    /// <summary>
    /// Gets tool usage analytics.
    /// </summary>
    /// <param name="timeRange">Time range for analytics.</param>
    /// <returns>Usage analytics.</returns>
    public Task<ToolUsageAnalytics> GetUsageAnalyticsAsync(TimeSpan? timeRange = null);

    /// <summary>
    /// Exports observability data.
    /// </summary>
    /// <param name="format">Export format (json, csv, etc).</param>
    /// <param name="options">Export options.</param>
    /// <returns>Exported data.</returns>
    public Task<string> ExportObservabilityDataAsync(string format, ExportOptions? options = null);
}
