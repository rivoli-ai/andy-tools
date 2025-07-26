using System.Diagnostics;
using Andy.Tools.Core;
using Andy.Tools.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Andy.Tools.Tests.Observability;

public class ToolObservabilityServiceTests : IDisposable
{
    private readonly ToolObservabilityService _service;
    private readonly ToolObservabilityOptions _options;
    private readonly ActivityListener _activityListener;

    public ToolObservabilityServiceTests()
    {
        _options = new ToolObservabilityOptions
        {
            MetricsAggregationInterval = TimeSpan.FromSeconds(1),
            RetentionPeriod = TimeSpan.FromHours(1),
            EnableDetailedTracing = true
        };

        _service = new ToolObservabilityService(
            Options.Create(_options),
            NullLogger<ToolObservabilityService>.Instance);

        // Set up activity listener to enable activities in tests
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Andy.Tools",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => { },
            ActivityStopped = activity => { }
        };

        ActivitySource.AddActivityListener(_activityListener);
    }

    [Fact]
    public void StartToolExecution_CreatesActivity()
    {
        // Arrange
        var toolId = "test_tool";
        var parameters = new Dictionary<string, object?> { ["param1"] = "value1" };
        var context = new ToolExecutionContext { UserId = "testuser", SessionId = "session123" };

        // Act
        var activity = _service.StartToolExecution(toolId, parameters, context);

        // Assert
        Assert.NotNull(activity);
        Assert.Equal($"ToolExecution.{toolId}", activity.OperationName);
        Assert.Equal(toolId, activity.GetTagItem("tool.id"));
        Assert.Equal("testuser", activity.GetTagItem("tool.user"));
        Assert.Equal("session123", activity.GetTagItem("tool.session"));
    }

    [Fact]
    public void CompleteToolExecution_RecordsMetrics()
    {
        // Arrange
        var toolId = "test_tool";
        var parameters = new Dictionary<string, object?>();
        var context = new ToolExecutionContext();
        var activity = _service.StartToolExecution(toolId, parameters, context);

        var result = new ToolExecutionResult
        {
            ToolId = toolId,
            IsSuccessful = true,
            DurationMs = 100,
            ResourceUsage = new ToolResourceUsage
            {
                PeakMemoryBytes = 1024 * 1024,
                CpuTimeMs = 50
            }
        };

        // Act
        _service.CompleteToolExecution(activity, result);

        // Assert - activity should be stopped
        Assert.NotNull(activity);
        Assert.True(activity.Duration > TimeSpan.Zero);
    }

    [Fact]
    public void RecordToolError_SetsActivityError()
    {
        // Arrange
        var toolId = "test_tool";
        var parameters = new Dictionary<string, object?>();
        var context = new ToolExecutionContext();
        var activity = _service.StartToolExecution(toolId, parameters, context);
        var exception = new InvalidOperationException("Test error");

        // Act
        _service.RecordToolError(activity, toolId, exception);

        // Assert
        Assert.NotNull(activity);
        Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, activity.Status);
        Assert.Equal("Test error", activity.StatusDescription);
    }

    [Fact]
    public void RecordSecurityEvent_StoresEvent()
    {
        // Arrange
        var toolId = "test_tool";
        var eventType = "PermissionDenied";
        var details = new Dictionary<string, object?>
        {
            ["permission"] = "FileSystem",
            ["path"] = "/restricted/file"
        };

        // Act
        _service.RecordSecurityEvent(toolId, eventType, details);

        // No direct assertion possible without exposing internal state
        // In real tests, we'd verify through GetStatistics or similar
    }

    [Fact]
    public async Task GetPerformanceStatisticsAsync_ReturnsEmptyStatsForNoData()
    {
        // Act
        var stats = await _service.GetPerformanceStatisticsAsync();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(0, stats.ExecutionCount);
        Assert.Equal(0, stats.SuccessCount);
        Assert.Equal(0, stats.FailureCount);
        Assert.Equal(0, stats.SuccessRate);
    }

    [Fact]
    public async Task GetPerformanceStatisticsAsync_CalculatesCorrectStats()
    {
        // Arrange - Execute some tools
        var toolId = "test_tool";
        var context = new ToolExecutionContext();

        // Successful execution
        var activity1 = _service.StartToolExecution(toolId, new Dictionary<string, object?>(), context);
        await Task.Delay(50);
        _service.CompleteToolExecution(activity1, new ToolExecutionResult
        {
            ToolId = toolId,
            IsSuccessful = true,
            DurationMs = 50
        });

        // Failed execution
        var activity2 = _service.StartToolExecution(toolId, new Dictionary<string, object?>(), context);
        await Task.Delay(30);
        _service.CompleteToolExecution(activity2, new ToolExecutionResult
        {
            ToolId = toolId,
            IsSuccessful = false,
            DurationMs = 30,
            ErrorMessage = "Test failure"
        });

        // Act
        var stats = await _service.GetPerformanceStatisticsAsync(toolId);

        // Assert
        Assert.Equal(2, stats.ExecutionCount);
        Assert.Equal(1, stats.SuccessCount);
        Assert.Equal(1, stats.FailureCount);
        Assert.Equal(0.5, stats.SuccessRate);
        Assert.True(stats.AverageExecutionTime.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task GetUsageAnalyticsAsync_TracksToolUsage()
    {
        // Arrange
        var tool1 = "tool1";
        var tool2 = "tool2";
        var context = new ToolExecutionContext { UserId = "user1" };

        // Execute tools
        for (int i = 0; i < 3; i++)
        {
            var activity = _service.StartToolExecution(tool1, new Dictionary<string, object?>(), context);
            _service.CompleteToolExecution(activity, new ToolExecutionResult { ToolId = tool1, IsSuccessful = true });
        }

        var activity2 = _service.StartToolExecution(tool2, new Dictionary<string, object?>(), context);
        _service.CompleteToolExecution(activity2, new ToolExecutionResult { ToolId = tool2, IsSuccessful = true });

        // Act
        var analytics = await _service.GetUsageAnalyticsAsync();

        // Assert
        Assert.NotNull(analytics);
        Assert.Equal(2, analytics.ToolUsage.Count);

        Assert.True(analytics.ToolUsage.ContainsKey(tool1));
        Assert.Equal(3, analytics.ToolUsage[tool1].ExecutionCount);
        Assert.Equal(1, analytics.ToolUsage[tool1].UniqueUsers);

        Assert.True(analytics.ToolUsage.ContainsKey(tool2));
        Assert.Equal(1, analytics.ToolUsage[tool2].ExecutionCount);
    }

    [Fact]
    public async Task ExportObservabilityDataAsync_ExportsToJson()
    {
        // Arrange
        var toolId = "export_test";
        var context = new ToolExecutionContext();

        var activity = _service.StartToolExecution(toolId, new Dictionary<string, object?>(), context);
        _service.CompleteToolExecution(activity, new ToolExecutionResult
        {
            ToolId = toolId,
            IsSuccessful = true,
            DurationMs = 100
        });

        var options = new ExportOptions
        {
            IncludeStatistics = true,
            IncludeRawData = false
        };

        // Act
        var json = await _service.ExportObservabilityDataAsync("json", options);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("PerformanceStatistics", json);
        Assert.Contains("UsageAnalytics", json);
        Assert.Contains(toolId, json);
    }

    [Fact]
    public async Task ExportObservabilityDataAsync_ExportsToCsv()
    {
        // Arrange
        var toolId = "csv_test";
        var context = new ToolExecutionContext();

        var activity = _service.StartToolExecution(toolId, new Dictionary<string, object?>(), context);
        _service.CompleteToolExecution(activity, new ToolExecutionResult
        {
            ToolId = toolId,
            IsSuccessful = true,
            DurationMs = 100
        });

        var options = new ExportOptions
        {
            IncludeStatistics = true,
            IncludeRawData = true
        };

        // Act
        var csv = await _service.ExportObservabilityDataAsync("csv", options);

        // Assert
        Assert.NotNull(csv);
        Assert.Contains("Tool Executions", csv);
        Assert.Contains("Performance Statistics", csv);
        Assert.Contains(toolId, csv);
    }

    [Fact]
    public async Task ExportObservabilityDataAsync_UnsupportedFormat_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(
            () => _service.ExportObservabilityDataAsync("xml"));
    }

    public void Dispose()
    {
        _activityListener?.Dispose();
        _service?.Dispose();
    }
}
