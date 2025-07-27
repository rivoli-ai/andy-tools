using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Andy.Tools.Advanced.MetricsCollection;
using Andy.Tools.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Andy.Tools.Tests.Advanced.MetricsCollection;

public class InMemoryToolMetricsCollectorTests : IDisposable
{
    private readonly InMemoryToolMetricsCollector _collector;
    private readonly Mock<ILogger<InMemoryToolMetricsCollector>> _mockLogger;
    private readonly ToolMetricsOptions _options;

    public InMemoryToolMetricsCollectorTests()
    {
        _options = new ToolMetricsOptions
        {
            MaxMetricsPerTool = 1000,
            AggregationInterval = TimeSpan.FromMinutes(5),
            MetricsRetentionPeriod = TimeSpan.FromHours(24),
            CollectResourceUsage = true,
            EnableDetailedTracking = true
        };

        _mockLogger = new Mock<ILogger<InMemoryToolMetricsCollector>>();
        var optionsWrapper = Options.Create(_options);
        _collector = new InMemoryToolMetricsCollector(optionsWrapper, _mockLogger.Object);
    }

    public void Dispose()
    {
        _collector?.Dispose();
    }

    #region Recording Metrics Tests

    [Fact]
    public async Task RecordExecutionAsync_ShouldStoreMetrics()
    {
        // Arrange
        var execution = CreateExecutionMetrics("tool1", true, 100);

        // Act
        await _collector.RecordExecutionAsync(execution);
        var metrics = await _collector.GetToolMetricsAsync("tool1");

        // Assert
        metrics.TotalExecutions.Should().Be(1);
        metrics.SuccessfulExecutions.Should().Be(1);
        metrics.FailedExecutions.Should().Be(0);
        metrics.AverageDurationMs.Should().Be(100);
    }

    [Fact]
    public async Task RecordExecutionAsync_WithMaxMetrics_ShouldEnforceLimit()
    {
        // Arrange
        var smallLimitCollector = new InMemoryToolMetricsCollector(
            Options.Create(new ToolMetricsOptions { MaxMetricsPerTool = 5 }),
            _mockLogger.Object);

        // Act - Add more than the limit
        for (int i = 0; i < 10; i++)
        {
            await smallLimitCollector.RecordExecutionAsync(
                CreateExecutionMetrics("tool1", true, i * 10, DateTimeOffset.UtcNow.AddMinutes(-i)));
        }

        var metrics = await smallLimitCollector.GetToolMetricsAsync("tool1");

        // Assert
        metrics.TotalExecutions.Should().Be(5);
        smallLimitCollector.Dispose();
    }

    [Fact]
    public async Task RecordCacheHitAsync_ShouldIncrementCounters()
    {
        // Arrange - Need at least one execution for GetToolMetricsAsync to return cache data
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100));
        
        // Act
        await _collector.RecordCacheHitAsync("tool1", 50);
        await _collector.RecordCacheHitAsync("tool1", 100);
        var metrics = await _collector.GetToolMetricsAsync("tool1");

        // Assert
        metrics.CacheHits.Should().Be(2);
        metrics.AverageTimeSavedByCacheMs.Should().Be(75);
    }

    [Fact]
    public async Task RecordCacheMissAsync_ShouldIncrementCounters()
    {
        // Arrange - Need at least one execution for GetToolMetricsAsync to return cache data
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100));
        
        // Act
        await _collector.RecordCacheMissAsync("tool1");
        await _collector.RecordCacheMissAsync("tool1");
        await _collector.RecordCacheHitAsync("tool1", 100);
        var metrics = await _collector.GetToolMetricsAsync("tool1");

        // Assert
        metrics.CacheMisses.Should().Be(2);
        metrics.CacheHits.Should().Be(1);
        metrics.CacheHitRate.Should().BeApproximately(0.333, 0.001);
    }

    #endregion

    #region Tool Metrics Retrieval Tests

    [Fact]
    public async Task GetToolMetricsAsync_ShouldCalculateBasicStats()
    {
        // Arrange
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 200));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", false, 150));

        // Act
        var metrics = await _collector.GetToolMetricsAsync("tool1");

        // Assert
        metrics.TotalExecutions.Should().Be(3);
        metrics.SuccessfulExecutions.Should().Be(2);
        metrics.FailedExecutions.Should().Be(1);
        metrics.SuccessRate.Should().BeApproximately(0.667, 0.001);
        metrics.AverageDurationMs.Should().Be(150);
    }

    [Fact]
    public async Task GetToolMetricsAsync_ShouldCalculatePercentiles()
    {
        // Arrange - Add executions with known durations
        var durations = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        foreach (var duration in durations)
        {
            await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, duration));
        }

        // Act
        var metrics = await _collector.GetToolMetricsAsync("tool1");

        // Assert
        metrics.MinDurationMs.Should().Be(10);
        metrics.MaxDurationMs.Should().Be(100);
        metrics.P50DurationMs.Should().BeApproximately(55, 1); // Median
        metrics.P90DurationMs.Should().BeApproximately(91, 1);
        metrics.P99DurationMs.Should().BeApproximately(99.1, 1);
    }

    [Fact]
    public async Task GetToolMetricsAsync_WithTimeRange_ShouldFilterResults()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100, now.AddHours(-2)));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 200, now.AddMinutes(-30)));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 300, now.AddMinutes(-5)));

        var timeRange = new TimeRange
        {
            Start = now.AddHours(-1),
            End = now
        };

        // Act
        var metrics = await _collector.GetToolMetricsAsync("tool1", timeRange);

        // Assert
        metrics.TotalExecutions.Should().Be(2); // Only last 2 are within range
        metrics.AverageDurationMs.Should().Be(250);
    }

    [Fact]
    public async Task GetToolMetricsAsync_NoData_ShouldReturnEmptyMetrics()
    {
        // Act
        var metrics = await _collector.GetToolMetricsAsync("non-existent-tool");

        // Assert
        metrics.ToolId.Should().Be("non-existent-tool");
        metrics.TotalExecutions.Should().Be(0);
        metrics.SuccessRate.Should().Be(0);
    }

    [Fact]
    public async Task GetToolMetricsAsync_WithErrorDistribution_ShouldGroupErrors()
    {
        // Arrange
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", false, 100, errorCode: "ERROR_001"));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", false, 100, errorCode: "ERROR_001"));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", false, 100, errorCode: "ERROR_002"));

        // Act
        var metrics = await _collector.GetToolMetricsAsync("tool1");

        // Assert
        metrics.ErrorDistribution.Should().ContainKey("ERROR_001").WhoseValue.Should().Be(2);
        metrics.ErrorDistribution.Should().ContainKey("ERROR_002").WhoseValue.Should().Be(1);
    }

    [Fact]
    public async Task GetToolMetricsAsync_WithResourceUsage_ShouldCalculateAverages()
    {
        // Arrange
        var resourceUsage1 = new ResourceUsageMetrics
        {
            CpuUsagePercent = 50,
            MemoryUsageBytes = 1000,
            DiskReadBytes = 500,
            DiskWriteBytes = 200
        };
        var resourceUsage2 = new ResourceUsageMetrics
        {
            CpuUsagePercent = 60,
            MemoryUsageBytes = 2000,
            DiskReadBytes = 1000,
            DiskWriteBytes = 400
        };

        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100, resourceUsage: resourceUsage1));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100, resourceUsage: resourceUsage2));

        // Act
        var metrics = await _collector.GetToolMetricsAsync("tool1");

        // Assert
        metrics.AverageResourceUsage.Should().NotBeNull();
        metrics.AverageResourceUsage!.CpuUsagePercent.Should().Be(55);
        metrics.AverageResourceUsage.MemoryUsageBytes.Should().Be(1500);
        metrics.AverageResourceUsage.DiskReadBytes.Should().Be(750);
        metrics.AverageResourceUsage.DiskWriteBytes.Should().Be(300);
    }

    #endregion

    #region System Metrics Tests

    [Fact]
    public async Task GetSystemMetricsAsync_ShouldAggregateAllTools()
    {
        // Arrange
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", false, 200));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool2", true, 300));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool3", true, 150));

        // Act
        var systemMetrics = await _collector.GetSystemMetricsAsync();

        // Assert
        systemMetrics.TotalExecutions.Should().Be(4);
        systemMetrics.SuccessfulExecutions.Should().Be(3);
        systemMetrics.FailedExecutions.Should().Be(1);
        systemMetrics.OverallSuccessRate.Should().Be(0.75);
        systemMetrics.UniqueToolsUsed.Should().Be(3);
    }

    [Fact]
    public async Task GetSystemMetricsAsync_ShouldIdentifyMostUsedTools()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
            await _collector.RecordExecutionAsync(CreateExecutionMetrics("popular-tool", true, 100, toolName: "Popular Tool"));
        for (int i = 0; i < 5; i++)
            await _collector.RecordExecutionAsync(CreateExecutionMetrics("medium-tool", true, 100, toolName: "Medium Tool"));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("rare-tool", true, 100, toolName: "Rare Tool"));

        // Act
        var systemMetrics = await _collector.GetSystemMetricsAsync();

        // Assert
        systemMetrics.MostUsedTools.Should().HaveCountGreaterOrEqualTo(3);
        var mostUsed = systemMetrics.MostUsedTools.First();
        mostUsed.ToolId.Should().Be("popular-tool");
        mostUsed.ExecutionCount.Should().Be(10);
        mostUsed.UsagePercentage.Should().BeApproximately(62.5, 0.1); // 10/16
    }

    [Fact]
    public async Task GetSystemMetricsAsync_ShouldIdentifySlowestTools()
    {
        // Arrange
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("slow-tool", true, 1000, toolName: "Slow Tool"));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("medium-tool", true, 500, toolName: "Medium Tool"));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("fast-tool", true, 100, toolName: "Fast Tool"));

        // Act
        var systemMetrics = await _collector.GetSystemMetricsAsync();

        // Assert
        systemMetrics.SlowestTools.Should().HaveCountGreaterOrEqualTo(3);
        var slowest = systemMetrics.SlowestTools.First();
        slowest.ToolId.Should().Be("slow-tool");
        slowest.AverageDurationMs.Should().Be(1000);
    }

    [Fact]
    public async Task GetSystemMetricsAsync_ShouldIdentifyLeastReliableTools()
    {
        // Arrange
        // Unreliable tool - 50% failure rate
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("unreliable", true, 100, toolName: "Unreliable Tool"));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("unreliable", false, 100, toolName: "Unreliable Tool", errorCode: "ERROR_001"));

        // Reliable tool - 0% failure rate
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("reliable", true, 100, toolName: "Reliable Tool"));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("reliable", true, 100, toolName: "Reliable Tool"));

        // Act
        var systemMetrics = await _collector.GetSystemMetricsAsync();

        // Assert
        systemMetrics.LeastReliableTools.Should().HaveCountGreaterOrEqualTo(1);
        var leastReliable = systemMetrics.LeastReliableTools.First();
        leastReliable.ToolId.Should().Be("unreliable");
        leastReliable.FailureRate.Should().Be(0.5);
        leastReliable.TotalFailures.Should().Be(1);
        leastReliable.MostCommonError.Should().Be("ERROR_001");
    }

    [Fact]
    public async Task GetSystemMetricsAsync_ShouldCalculatePeakUsageTimes()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // Create executions at different hours
        for (int hour = 0; hour < 24; hour++)
        {
            var time = new DateTimeOffset(baseTime.AddHours(hour), TimeSpan.Zero);
            var count = hour == 14 ? 5 : 1; // Peak at 2 PM
            
            for (int i = 0; i < count; i++)
            {
                await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100, time));
            }
        }

        // Act
        var systemMetrics = await _collector.GetSystemMetricsAsync();

        // Assert
        systemMetrics.PeakUsageTimes.Should().NotBeEmpty();
        var peakTime = systemMetrics.PeakUsageTimes.OrderByDescending(p => p.ExecutionCount).First();
        peakTime.TimePeriod.Hour.Should().Be(14);
        peakTime.ExecutionCount.Should().Be(5);
    }

    #endregion

    #region Performance Trends Tests

    [Fact]
    public async Task GetPerformanceTrendsAsync_ByMinute_ShouldGroupCorrectly()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        
        for (int i = 0; i < 5; i++)
        {
            await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100, baseTime.AddMinutes(i)));
        }

        var timeRange = new TimeRange { Start = baseTime, End = baseTime.AddMinutes(10) };

        // Act
        var trends = await _collector.GetPerformanceTrendsAsync("tool1", TimeInterval.Minute, timeRange);

        // Assert
        trends.Should().HaveCount(5);
        trends.All(t => t.ExecutionCount == 1).Should().BeTrue();
    }

    [Fact]
    public async Task GetPerformanceTrendsAsync_ByHour_ShouldGroupCorrectly()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        
        // Add executions across multiple hours
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100, baseTime));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 200, baseTime.AddMinutes(30)));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", false, 150, baseTime.AddHours(1)));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 300, baseTime.AddHours(2)));

        var timeRange = new TimeRange { Start = baseTime.AddMinutes(-30), End = baseTime.AddHours(3) };

        // Act
        var trends = await _collector.GetPerformanceTrendsAsync("tool1", TimeInterval.Hour, timeRange);

        // Assert
        trends.Should().HaveCountGreaterOrEqualTo(3);
        var orderedTrends = trends.OrderBy(t => t.Timestamp).ToList();
        orderedTrends[0].ExecutionCount.Should().Be(2); // First hour has 2 executions
        orderedTrends[0].AverageDurationMs.Should().Be(150);
        orderedTrends[0].SuccessRate.Should().Be(1.0);
        orderedTrends[1].SuccessRate.Should().Be(0.0); // Second hour has 1 failure
    }

    [Fact]
    public async Task GetPerformanceTrendsAsync_WithToolFilter_ShouldFilterByTool()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100, now));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool2", true, 200, now));

        var timeRange = new TimeRange { Start = now.AddHours(-1), End = now.AddHours(1) };

        // Act
        var trends = await _collector.GetPerformanceTrendsAsync("tool1", TimeInterval.Hour, timeRange);

        // Assert
        trends.Should().HaveCount(1);
        trends[0].ExecutionCount.Should().Be(1);
        trends[0].AverageDurationMs.Should().Be(100);
    }

    [Fact]
    public async Task GetPerformanceTrendsAsync_AllTools_ShouldAggregateAllData()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100, now));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool2", true, 200, now));
        await _collector.RecordCacheHitAsync("tool1", 50);

        var timeRange = new TimeRange { Start = now.AddHours(-1), End = now.AddHours(1) };

        // Act
        var trends = await _collector.GetPerformanceTrendsAsync(null, TimeInterval.Hour, timeRange);

        // Assert
        trends.Should().HaveCount(1);
        trends[0].ExecutionCount.Should().Be(2);
        trends[0].AverageDurationMs.Should().Be(150);
    }

    #endregion

    #region Export Tests

    [Fact]
    public async Task ExportMetricsAsync_Json_ShouldGenerateValidJson()
    {
        // Arrange
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100));

        // Act
        var json = await _collector.ExportMetricsAsync(MetricsExportFormat.Json);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"ExportedAt\"");
        json.Should().Contain("\"System\"");
        json.Should().Contain("\"Tools\"");
        json.Should().Contain("\"tool1\"");
    }

    [Fact]
    public async Task ExportMetricsAsync_Csv_ShouldGenerateValidCsv()
    {
        // Arrange
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool2", true, 200));

        // Act
        var csv = await _collector.ExportMetricsAsync(MetricsExportFormat.Csv);

        // Assert
        csv.Should().NotBeNullOrEmpty();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().StartWith("ToolId,ToolName,TotalExecutions");
        lines.Should().HaveCountGreaterThan(2); // Header + data rows
    }

    [Fact]
    public async Task ExportMetricsAsync_Prometheus_ShouldGenerateValidFormat()
    {
        // Arrange
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100));

        // Act
        var prometheus = await _collector.ExportMetricsAsync(MetricsExportFormat.Prometheus);

        // Assert
        prometheus.Should().NotBeNullOrEmpty();
        prometheus.Should().Contain("# HELP");
        prometheus.Should().Contain("# TYPE");
        prometheus.Should().Contain("andy_tools_total_executions");
        prometheus.Should().Contain("andy_tool_executions{tool=\"tool1\"}");
    }

    [Fact]
    public async Task ExportMetricsAsync_OpenTelemetry_ShouldGenerateValidFormat()
    {
        // Arrange
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100));

        // Act
        var otel = await _collector.ExportMetricsAsync(MetricsExportFormat.OpenTelemetry);

        // Assert
        otel.Should().NotBeNullOrEmpty();
        otel.Should().Contain("ResourceMetrics");
        otel.Should().Contain("service.name");
        otel.Should().Contain("andy-tools");
    }

    #endregion

    #region Maintenance Tests

    [Fact]
    public async Task ClearOldMetricsAsync_ShouldRemoveOldData()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100, now.AddHours(-25))); // Old
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 200, now.AddHours(-1)));  // Recent

        // Act
        var removedCount = await _collector.ClearOldMetricsAsync(TimeSpan.FromHours(24));
        var metrics = await _collector.GetToolMetricsAsync("tool1");

        // Assert
        removedCount.Should().Be(1);
        metrics.TotalExecutions.Should().Be(1);
        metrics.AverageDurationMs.Should().Be(200);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task GetAllToolMetricsAsync_ShouldReturnAllTools()
    {
        // Arrange
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool1", true, 100));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool2", true, 200));
        await _collector.RecordExecutionAsync(CreateExecutionMetrics("tool3", true, 300));

        // Act
        var allMetrics = await _collector.GetAllToolMetricsAsync();

        // Assert
        allMetrics.Should().HaveCount(3);
        allMetrics.Should().ContainKey("tool1");
        allMetrics.Should().ContainKey("tool2");
        allMetrics.Should().ContainKey("tool3");
    }

    [Fact]
    public async Task RecordExecutionAsync_WithNullUserId_ShouldHandleGracefully()
    {
        // Arrange
        var execution = CreateExecutionMetrics("tool1", true, 100);
        execution.UserId = null;

        // Act
        await _collector.RecordExecutionAsync(execution);
        var systemMetrics = await _collector.GetSystemMetricsAsync();

        // Assert
        systemMetrics.UniqueUsers.Should().Be(0);
    }

    [Fact]
    public async Task GetPerformanceTrendsAsync_EmptyData_ShouldReturnEmptyList()
    {
        // Arrange
        var timeRange = new TimeRange
        {
            Start = DateTimeOffset.UtcNow.AddHours(-1),
            End = DateTimeOffset.UtcNow
        };

        // Act
        var trends = await _collector.GetPerformanceTrendsAsync("non-existent", TimeInterval.Hour, timeRange);

        // Assert
        trends.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static ToolExecutionMetrics CreateExecutionMetrics(
        string toolId,
        bool isSuccessful,
        double durationMs,
        DateTimeOffset? startTime = null,
        string? toolName = null,
        string? errorCode = null,
        ResourceUsageMetrics? resourceUsage = null)
    {
        return new ToolExecutionMetrics
        {
            ToolId = toolId,
            ToolName = toolName ?? toolId,
            StartTime = startTime ?? DateTimeOffset.UtcNow,
            DurationMs = durationMs,
            IsSuccessful = isSuccessful,
            ErrorCode = errorCode,
            ResourceUsage = resourceUsage,
            UserId = "test-user",
            SessionId = "test-session",
            CorrelationId = Guid.NewGuid().ToString()
        };
    }

    #endregion
}