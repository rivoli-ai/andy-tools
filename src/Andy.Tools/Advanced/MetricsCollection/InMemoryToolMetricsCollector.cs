using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andy.Tools.Advanced.MetricsCollection;

/// <summary>
/// In-memory implementation of tool metrics collector.
/// </summary>
public class InMemoryToolMetricsCollector : IToolMetricsCollector, IDisposable
{
    private readonly ConcurrentDictionary<string, List<ToolExecutionMetrics>> _metricsByTool = new();
    private readonly ConcurrentDictionary<string, ToolCacheMetrics> _cacheMetrics = new();
    private readonly ILogger<InMemoryToolMetricsCollector> _logger;
    private readonly ToolMetricsOptions _options;
    private readonly Timer _aggregationTimer;
    private readonly object _metricsLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryToolMetricsCollector"/> class.
    /// </summary>
    public InMemoryToolMetricsCollector(
        IOptions<ToolMetricsOptions> options,
        ILogger<InMemoryToolMetricsCollector> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Start aggregation timer
        _aggregationTimer = new Timer(
            AggregateMetrics,
            null,
            _options.AggregationInterval,
            _options.AggregationInterval);
    }

    /// <inheritdoc />
    public async Task RecordExecutionAsync(ToolExecutionMetrics execution)
    {
        try
        {
            lock (_metricsLock)
            {
                var metrics = _metricsByTool.GetOrAdd(execution.ToolId, _ => []);
                metrics.Add(execution);

                // Enforce retention policy
                if (_options.MaxMetricsPerTool > 0 && metrics.Count > _options.MaxMetricsPerTool)
                {
                    var toRemove = metrics.Count - _options.MaxMetricsPerTool;
                    metrics.RemoveRange(0, toRemove);
                }
            }

            _logger.LogDebug(
                "Recorded execution for tool {ToolId}: Success={Success}, Duration={Duration}ms",
                execution.ToolId, execution.IsSuccessful, execution.DurationMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording tool execution metrics");
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RecordCacheHitAsync(string toolId, double timeSavedMs)
    {
        try
        {
            var metrics = _cacheMetrics.GetOrAdd(toolId, _ => new ToolCacheMetrics());
            Interlocked.Increment(ref metrics.Hits);
            metrics.TotalTimeSavedMs += timeSavedMs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording cache hit");
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RecordCacheMissAsync(string toolId)
    {
        try
        {
            var metrics = _cacheMetrics.GetOrAdd(toolId, _ => new ToolCacheMetrics());
            Interlocked.Increment(ref metrics.Misses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording cache miss");
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<ToolMetrics> GetToolMetricsAsync(string toolId, TimeRange? timeRange = null)
    {
        try
        {
            var metrics = new ToolMetrics
            {
                ToolId = toolId,
                TimeRange = timeRange
            };

            if (!_metricsByTool.TryGetValue(toolId, out var executions))
            {
                return metrics;
            }

            List<ToolExecutionMetrics> filteredExecutions;
            lock (_metricsLock)
            {
                filteredExecutions = timeRange != null
                    ? [.. executions.Where(e => e.StartTime >= timeRange.Start && e.StartTime <= timeRange.End)]
                    : [.. executions];
            }

            if (filteredExecutions.Count == 0)
            {
                return metrics;
            }

            // Calculate basic metrics
            metrics.TotalExecutions = filteredExecutions.Count;
            metrics.SuccessfulExecutions = filteredExecutions.Count(e => e.IsSuccessful);
            metrics.FailedExecutions = filteredExecutions.Count(e => !e.IsSuccessful);

            // Calculate duration metrics
            var durations = filteredExecutions.Select(e => e.DurationMs).OrderBy(d => d).ToList();
            metrics.AverageDurationMs = durations.Average();
            metrics.MinDurationMs = durations.First();
            metrics.MaxDurationMs = durations.Last();
            metrics.P50DurationMs = GetPercentile(durations, 50);
            metrics.P90DurationMs = GetPercentile(durations, 90);
            metrics.P99DurationMs = GetPercentile(durations, 99);

            // Calculate error distribution
            var errors = filteredExecutions
                .Where(e => !e.IsSuccessful && !string.IsNullOrEmpty(e.ErrorCode))
                .GroupBy(e => e.ErrorCode!)
                .ToDictionary(g => g.Key, g => (long)g.Count());
            metrics.ErrorDistribution = errors;

            // Add cache metrics
            if (_cacheMetrics.TryGetValue(toolId, out var cacheMetrics))
            {
                metrics.CacheHits = cacheMetrics.Hits;
                metrics.CacheMisses = cacheMetrics.Misses;
                metrics.AverageTimeSavedByCacheMs = cacheMetrics.Hits > 0
                    ? cacheMetrics.TotalTimeSavedMs / cacheMetrics.Hits
                    : 0;
            }

            // Calculate average resource usage
            var resourceMetrics = filteredExecutions
                .Where(e => e.ResourceUsage != null)
                .Select(e => e.ResourceUsage!)
                .ToList();

            if (resourceMetrics.Count > 0)
            {
                metrics.AverageResourceUsage = new ResourceUsageMetrics
                {
                    CpuUsagePercent = resourceMetrics.Average(r => r.CpuUsagePercent),
                    MemoryUsageBytes = (long)resourceMetrics.Average(r => r.MemoryUsageBytes),
                    DiskReadBytes = (long)resourceMetrics.Average(r => r.DiskReadBytes),
                    DiskWriteBytes = (long)resourceMetrics.Average(r => r.DiskWriteBytes),
                    NetworkSentBytes = (long)resourceMetrics.Average(r => r.NetworkSentBytes),
                    NetworkReceivedBytes = (long)resourceMetrics.Average(r => r.NetworkReceivedBytes)
                };
            }

            await Task.CompletedTask;
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tool metrics for {ToolId}", toolId);
            return new ToolMetrics { ToolId = toolId };
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, ToolMetrics>> GetAllToolMetricsAsync(TimeRange? timeRange = null)
    {
        var result = new Dictionary<string, ToolMetrics>();

        foreach (var toolId in _metricsByTool.Keys)
        {
            result[toolId] = await GetToolMetricsAsync(toolId, timeRange);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<SystemMetrics> GetSystemMetricsAsync(TimeRange? timeRange = null)
    {
        try
        {
            var metrics = new SystemMetrics
            {
                TimeRange = timeRange
            };

            var allExecutions = new List<ToolExecutionMetrics>();
            lock (_metricsLock)
            {
                foreach (var executions in _metricsByTool.Values)
                {
                    var filtered = timeRange != null
                        ? executions.Where(e => e.StartTime >= timeRange.Start && e.StartTime <= timeRange.End)
                        : executions;
                    allExecutions.AddRange(filtered);
                }
            }

            metrics.TotalExecutions = allExecutions.Count;
            metrics.SuccessfulExecutions = allExecutions.Count(e => e.IsSuccessful);
            metrics.FailedExecutions = allExecutions.Count(e => !e.IsSuccessful);

            metrics.UniqueToolsUsed = allExecutions.Select(e => e.ToolId).Distinct().Count();
            metrics.UniqueUsers = allExecutions.Where(e => e.UserId != null).Select(e => e.UserId).Distinct().Count();
            metrics.UniqueSessions = allExecutions.Where(e => e.SessionId != null).Select(e => e.SessionId).Distinct().Count();

            // Calculate most used tools
            var toolUsage = allExecutions
                .GroupBy(e => e.ToolId)
                .Select(g => new ToolUsageInfo
                {
                    ToolId = g.Key,
                    ToolName = g.First().ToolName,
                    ExecutionCount = g.Count(),
                    UsagePercentage = (double)g.Count() / allExecutions.Count * 100
                })
                .OrderByDescending(t => t.ExecutionCount)
                .Take(10)
                .ToList();
            metrics.MostUsedTools = toolUsage;

            // Calculate slowest tools
            var toolPerformance = allExecutions
                .GroupBy(e => e.ToolId)
                .Select(g =>
                {
                    var durations = g.Select(e => e.DurationMs).OrderBy(d => d).ToList();
                    return new ToolPerformanceInfo
                    {
                        ToolId = g.Key,
                        ToolName = g.First().ToolName,
                        AverageDurationMs = durations.Average(),
                        P99DurationMs = GetPercentile(durations, 99)
                    };
                })
                .OrderByDescending(t => t.AverageDurationMs)
                .Take(10)
                .ToList();
            metrics.SlowestTools = toolPerformance;

            // Calculate least reliable tools
            var toolReliability = allExecutions
                .GroupBy(e => e.ToolId)
                .Select(g =>
                {
                    var failures = g.Where(e => !e.IsSuccessful).ToList();
                    var mostCommonError = failures
                        .Where(f => !string.IsNullOrEmpty(f.ErrorCode))
                        .GroupBy(f => f.ErrorCode)
                        .OrderByDescending(eg => eg.Count())
                        .FirstOrDefault()?.Key;

                    return new ToolReliabilityInfo
                    {
                        ToolId = g.Key,
                        ToolName = g.First().ToolName,
                        FailureRate = (double)failures.Count / g.Count(),
                        TotalFailures = failures.Count,
                        MostCommonError = mostCommonError
                    };
                })
                .Where(t => t.FailureRate > 0)
                .OrderByDescending(t => t.FailureRate)
                .Take(10)
                .ToList();
            metrics.LeastReliableTools = toolReliability;

            // Calculate peak usage times (hourly)
            var peakUsage = allExecutions
                .GroupBy(e => new DateTime(e.StartTime.Year, e.StartTime.Month, e.StartTime.Day, e.StartTime.Hour, 0, 0))
                .Select(g => new PeakUsageInfo
                {
                    TimePeriod = new DateTimeOffset(g.Key, TimeSpan.Zero),
                    ExecutionCount = g.Count(),
                    AverageResponseTimeMs = g.Average(e => e.DurationMs)
                })
                .OrderByDescending(p => p.ExecutionCount)
                .Take(24)
                .ToList();
            metrics.PeakUsageTimes = peakUsage;

            // Add cache metrics
            metrics.TotalCacheHits = _cacheMetrics.Values.Sum(m => m.Hits);
            metrics.TotalCacheMisses = _cacheMetrics.Values.Sum(m => m.Misses);

            await Task.CompletedTask;
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system metrics");
            return new SystemMetrics();
        }
    }

    /// <inheritdoc />
    public async Task<List<PerformanceTrend>> GetPerformanceTrendsAsync(
        string? toolId,
        TimeInterval interval,
        TimeRange timeRange)
    {
        try
        {
            var trends = new List<PerformanceTrend>();

            List<ToolExecutionMetrics> executions;
            lock (_metricsLock)
            {
                if (toolId != null)
                {
                    if (!_metricsByTool.TryGetValue(toolId, out var toolExecutions))
                    {
                        return trends;
                    }

                    executions = [.. toolExecutions.Where(e => e.StartTime >= timeRange.Start && e.StartTime <= timeRange.End)];
                }
                else
                {
                    executions = [.. _metricsByTool.Values
                        .SelectMany(list => list)
                        .Where(e => e.StartTime >= timeRange.Start && e.StartTime <= timeRange.End)];
                }
            }

            // Group by interval
            var groups = GroupByInterval(executions, interval);

            foreach (var group in groups)
            {
                var groupExecutions = group.ToList();
                if (groupExecutions.Count == 0)
                {
                    continue;
                }

                var trend = new PerformanceTrend
                {
                    Timestamp = group.Key,
                    ExecutionCount = groupExecutions.Count,
                    SuccessRate = (double)groupExecutions.Count(e => e.IsSuccessful) / groupExecutions.Count,
                    AverageDurationMs = groupExecutions.Average(e => e.DurationMs)
                };

                // Add cache metrics if available
                if (toolId != null && _cacheMetrics.TryGetValue(toolId, out var cacheMetrics))
                {
                    trend.CacheHitRate = cacheMetrics.Hits + cacheMetrics.Misses > 0
                        ? (double)cacheMetrics.Hits / (cacheMetrics.Hits + cacheMetrics.Misses)
                        : 0;
                }

                trends.Add(trend);
            }

            await Task.CompletedTask;
            return [.. trends.OrderBy(t => t.Timestamp)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance trends");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<string> ExportMetricsAsync(MetricsExportFormat format, TimeRange? timeRange = null)
    {
        try
        {
            var systemMetrics = await GetSystemMetricsAsync(timeRange);
            var toolMetrics = await GetAllToolMetricsAsync(timeRange);

            return format switch
            {
                MetricsExportFormat.Json => ExportAsJson(systemMetrics, toolMetrics),
                MetricsExportFormat.Csv => ExportAsCsv(systemMetrics, toolMetrics),
                MetricsExportFormat.Prometheus => ExportAsPrometheus(systemMetrics, toolMetrics),
                MetricsExportFormat.OpenTelemetry => ExportAsOpenTelemetry(systemMetrics, toolMetrics),
                _ => throw new NotSupportedException($"Export format {format} is not supported")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting metrics");
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public async Task<int> ClearOldMetricsAsync(TimeSpan olderThan)
    {
        try
        {
            var cutoffTime = DateTimeOffset.UtcNow.Subtract(olderThan);
            var removedCount = 0;

            lock (_metricsLock)
            {
                foreach (var kvp in _metricsByTool)
                {
                    var before = kvp.Value.Count;
                    kvp.Value.RemoveAll(e => e.StartTime < cutoffTime);
                    removedCount += before - kvp.Value.Count;
                }
            }

            _logger.LogInformation("Cleared {Count} metrics older than {Cutoff}", removedCount, cutoffTime);

            await Task.CompletedTask;
            return removedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing old metrics");
            return 0;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _aggregationTimer?.Dispose();
    }

    private static double GetPercentile(List<double> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        var index = percentile / 100.0 * (sortedValues.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper)
        {
            return sortedValues[lower];
        }

        var weight = index - lower;
        return (sortedValues[lower] * (1 - weight)) + (sortedValues[upper] * weight);
    }

    private static IEnumerable<IGrouping<DateTimeOffset, ToolExecutionMetrics>> GroupByInterval(
        List<ToolExecutionMetrics> executions,
        TimeInterval interval)
    {
        return interval switch
        {
            TimeInterval.Minute => executions.GroupBy(e => new DateTimeOffset(
                e.StartTime.Year, e.StartTime.Month, e.StartTime.Day,
                e.StartTime.Hour, e.StartTime.Minute, 0, e.StartTime.Offset)),

            TimeInterval.Hour => executions.GroupBy(e => new DateTimeOffset(
                e.StartTime.Year, e.StartTime.Month, e.StartTime.Day,
                e.StartTime.Hour, 0, 0, e.StartTime.Offset)),

            TimeInterval.Day => executions.GroupBy(e => new DateTimeOffset(
                e.StartTime.Year, e.StartTime.Month, e.StartTime.Day,
                0, 0, 0, e.StartTime.Offset)),

            TimeInterval.Week => executions.GroupBy(e =>
            {
                var date = e.StartTime.Date;
                var dayOfWeek = (int)date.DayOfWeek;
                var weekStart = date.AddDays(-dayOfWeek);
                return new DateTimeOffset(weekStart, e.StartTime.Offset);
            }),

            TimeInterval.Month => executions.GroupBy(e => new DateTimeOffset(
                e.StartTime.Year, e.StartTime.Month, 1,
                0, 0, 0, e.StartTime.Offset)),

            _ => throw new ArgumentOutOfRangeException(nameof(interval))
        };
    }

    private static string ExportAsJson(SystemMetrics systemMetrics, Dictionary<string, ToolMetrics> toolMetrics)
    {
        var export = new
        {
            ExportedAt = DateTimeOffset.UtcNow,
            System = systemMetrics,
            Tools = toolMetrics
        };

        return JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string ExportAsCsv(SystemMetrics systemMetrics, Dictionary<string, ToolMetrics> toolMetrics)
    {
        var csv = new StringBuilder();

        // Header
        csv.AppendLine("ToolId,ToolName,TotalExecutions,SuccessRate,AverageDurationMs,P50DurationMs,P90DurationMs,P99DurationMs,CacheHitRate");

        // Data
        foreach (var kvp in toolMetrics)
        {
            var m = kvp.Value;
            csv.AppendLine($"{m.ToolId},{m.ToolName},{m.TotalExecutions},{m.SuccessRate:F2},{m.AverageDurationMs:F2},{m.P50DurationMs:F2},{m.P90DurationMs:F2},{m.P99DurationMs:F2},{m.CacheHitRate:F2}");
        }

        return csv.ToString();
    }

    private static string ExportAsPrometheus(SystemMetrics systemMetrics, Dictionary<string, ToolMetrics> toolMetrics)
    {
        var prometheus = new StringBuilder();

        // System metrics
        prometheus.AppendLine($"# HELP andy_tools_total_executions Total number of tool executions");
        prometheus.AppendLine($"# TYPE andy_tools_total_executions counter");
        prometheus.AppendLine($"andy_tools_total_executions {systemMetrics.TotalExecutions}");

        prometheus.AppendLine($"# HELP andy_tools_success_rate Overall tool success rate");
        prometheus.AppendLine($"# TYPE andy_tools_success_rate gauge");
        prometheus.AppendLine($"andy_tools_success_rate {systemMetrics.OverallSuccessRate:F4}");

        // Tool-specific metrics
        prometheus.AppendLine($"# HELP andy_tool_executions Tool executions by tool");
        prometheus.AppendLine($"# TYPE andy_tool_executions counter");
        foreach (var kvp in toolMetrics)
        {
            prometheus.AppendLine($"andy_tool_executions{{tool=\"{kvp.Key}\"}} {kvp.Value.TotalExecutions}");
        }

        prometheus.AppendLine($"# HELP andy_tool_duration_ms Tool execution duration in milliseconds");
        prometheus.AppendLine($"# TYPE andy_tool_duration_ms histogram");
        foreach (var kvp in toolMetrics)
        {
            var m = kvp.Value;
            prometheus.AppendLine($"andy_tool_duration_ms{{tool=\"{kvp.Key}\",quantile=\"0.5\"}} {m.P50DurationMs:F2}");
            prometheus.AppendLine($"andy_tool_duration_ms{{tool=\"{kvp.Key}\",quantile=\"0.9\"}} {m.P90DurationMs:F2}");
            prometheus.AppendLine($"andy_tool_duration_ms{{tool=\"{kvp.Key}\",quantile=\"0.99\"}} {m.P99DurationMs:F2}");
        }

        return prometheus.ToString();
    }

    private static string ExportAsOpenTelemetry(SystemMetrics systemMetrics, Dictionary<string, ToolMetrics> toolMetrics)
    {
        // Simplified OpenTelemetry format
        var otel = new
        {
            ResourceMetrics = new[]
            {
                new
                {
                    Resource = new { Attributes = new[] { new { Key = "service.name", Value = "andy-tools" } } },
                    ScopeMetrics = new[]
                    {
                        new
                        {
                            Scope = new { Name = "andy.tools.metrics" },
                            Metrics = toolMetrics.Select(kvp => new
                            {
                                Name = $"tool.executions.{kvp.Key}",
                                Sum = new
                                {
                                    DataPoints = new[]
                                    {
                                        new
                                        {
                                            Attributes = new[] { new { Key = "tool.id", Value = kvp.Key } },
                                            Value = kvp.Value.TotalExecutions,
                                            TimeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000
                                        }
                                    }
                                }
                            })
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(otel, new JsonSerializerOptions { WriteIndented = true });
    }

    private void AggregateMetrics(object? state)
    {
        try
        {
            // Perform any periodic aggregation or cleanup
            if (_options.MetricsRetentionPeriod.HasValue)
            {
                _ = ClearOldMetricsAsync(_options.MetricsRetentionPeriod.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during metrics aggregation");
        }
    }

    private class ToolCacheMetrics
    {
        public long Hits;
        public long Misses;
        public double TotalTimeSavedMs;
    }
}
