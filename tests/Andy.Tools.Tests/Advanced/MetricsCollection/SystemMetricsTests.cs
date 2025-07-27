using Andy.Tools.Advanced.MetricsCollection;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Advanced.MetricsCollection;

public class SystemMetricsTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var metrics = new SystemMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.TotalExecutions.Should().Be(0);
        metrics.SuccessfulExecutions.Should().Be(0);
        metrics.FailedExecutions.Should().Be(0);
        metrics.UniqueToolsUsed.Should().Be(0);
        metrics.UniqueUsers.Should().Be(0);
        metrics.UniqueSessions.Should().Be(0);
        metrics.TotalCacheHits.Should().Be(0);
        metrics.TotalCacheMisses.Should().Be(0);
        metrics.MostUsedTools.Should().NotBeNull().And.BeEmpty();
        metrics.SlowestTools.Should().NotBeNull().And.BeEmpty();
        metrics.LeastReliableTools.Should().NotBeNull().And.BeEmpty();
        metrics.PeakUsageTimes.Should().NotBeNull().And.BeEmpty();
        metrics.TimeRange.Should().BeNull();
        metrics.CalculatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void OverallSuccessRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var metrics = new SystemMetrics
        {
            TotalExecutions = 1000,
            SuccessfulExecutions = 950
        };

        // Act & Assert
        metrics.OverallSuccessRate.Should().Be(0.95);
    }

    [Fact]
    public void OverallSuccessRate_ShouldReturnZero_WhenNoExecutions()
    {
        // Arrange
        var metrics = new SystemMetrics();

        // Act & Assert
        metrics.OverallSuccessRate.Should().Be(0);
    }

    [Fact]
    public void OverallCacheHitRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var metrics = new SystemMetrics
        {
            TotalCacheHits = 750,
            TotalCacheMisses = 250
        };

        // Act & Assert
        metrics.OverallCacheHitRate.Should().Be(0.75);
    }

    [Fact]
    public void OverallCacheHitRate_ShouldReturnZero_WhenNoCacheAccess()
    {
        // Arrange
        var metrics = new SystemMetrics();

        // Act & Assert
        metrics.OverallCacheHitRate.Should().Be(0);
    }

    [Fact]
    public void TotalExecutions_ShouldBeSettable()
    {
        // Arrange
        var metrics = new SystemMetrics();

        // Act
        metrics.TotalExecutions = 10000;

        // Assert
        metrics.TotalExecutions.Should().Be(10000);
    }

    [Fact]
    public void MostUsedTools_ShouldBeSettable()
    {
        // Arrange
        var metrics = new SystemMetrics();
        var tools = new List<ToolUsageInfo>();

        // Act
        metrics.MostUsedTools = tools;

        // Assert
        metrics.MostUsedTools.Should().BeSameAs(tools);
    }

    [Fact]
    public void SlowestTools_ShouldBeSettable()
    {
        // Arrange
        var metrics = new SystemMetrics();
        var tools = new List<ToolPerformanceInfo>();

        // Act
        metrics.SlowestTools = tools;

        // Assert
        metrics.SlowestTools.Should().BeSameAs(tools);
    }

    [Fact]
    public void LeastReliableTools_ShouldBeSettable()
    {
        // Arrange
        var metrics = new SystemMetrics();
        var tools = new List<ToolReliabilityInfo>();

        // Act
        metrics.LeastReliableTools = tools;

        // Assert
        metrics.LeastReliableTools.Should().BeSameAs(tools);
    }

    [Fact]
    public void PeakUsageTimes_ShouldSupportAddingItems()
    {
        // Arrange
        var metrics = new SystemMetrics();
        var peakUsage = new PeakUsageInfo
        {
            TimePeriod = DateTimeOffset.UtcNow,
            ExecutionCount = 1000,
            AverageResponseTimeMs = 50
        };

        // Act
        metrics.PeakUsageTimes.Add(peakUsage);

        // Assert
        metrics.PeakUsageTimes.Should().ContainSingle();
    }

    [Fact]
    public void TimeRange_ShouldBeSettable()
    {
        // Arrange
        var metrics = new SystemMetrics();
        var timeRange = new TimeRange
        {
            Start = DateTimeOffset.UtcNow.AddDays(-7),
            End = DateTimeOffset.UtcNow
        };

        // Act
        metrics.TimeRange = timeRange;

        // Assert
        metrics.TimeRange.Should().BeSameAs(timeRange);
    }

    [Fact]
    public void SystemMetrics_ShouldSupportFluentConfiguration()
    {
        // Arrange & Act
        var timeRange = TimeRange.LastDays(7);
        var metrics = new SystemMetrics
        {
            TotalExecutions = 50000,
            SuccessfulExecutions = 48000,
            FailedExecutions = 2000,
            UniqueToolsUsed = 25,
            UniqueUsers = 100,
            UniqueSessions = 500,
            TotalCacheHits = 35000,
            TotalCacheMisses = 15000,
            TimeRange = timeRange
        };

        // Assert
        metrics.TotalExecutions.Should().Be(50000);
        metrics.SuccessfulExecutions.Should().Be(48000);
        metrics.FailedExecutions.Should().Be(2000);
        metrics.OverallSuccessRate.Should().Be(0.96);
        metrics.UniqueToolsUsed.Should().Be(25);
        metrics.UniqueUsers.Should().Be(100);
        metrics.UniqueSessions.Should().Be(500);
        metrics.OverallCacheHitRate.Should().Be(0.7);
        metrics.TimeRange.Should().BeSameAs(timeRange);
    }

    [Fact]
    public void SystemMetrics_ShouldHandleRealWorldScenario()
    {
        // Arrange - Simulate weekly metrics
        var metrics = new SystemMetrics
        {
            TotalExecutions = 1_000_000,
            SuccessfulExecutions = 985_000,
            FailedExecutions = 15_000,
            UniqueToolsUsed = 50,
            UniqueUsers = 5000,
            UniqueSessions = 25000,
            TotalCacheHits = 750_000,
            TotalCacheMisses = 250_000,
            TimeRange = TimeRange.LastDays(7)
        };

        // Add peak usage times (hourly for a day)
        for (int hour = 0; hour < 24; hour++)
        {
            metrics.PeakUsageTimes.Add(new PeakUsageInfo
            {
                TimePeriod = DateTimeOffset.UtcNow.Date.AddHours(hour),
                ExecutionCount = hour >= 9 && hour <= 17 ? 50_000 : 10_000, // Business hours
                AverageResponseTimeMs = hour >= 9 && hour <= 17 ? 75 : 25
            });
        }

        // Assert
        metrics.OverallSuccessRate.Should().Be(0.985);
        metrics.OverallCacheHitRate.Should().Be(0.75);
        metrics.PeakUsageTimes.Should().HaveCount(24);
        metrics.PeakUsageTimes.Where(p => p.ExecutionCount > 40_000).Should().HaveCount(9); // 9 business hours
    }

    [Fact]
    public void CalculatedAt_ShouldBeCurrentTime_ByDefault()
    {
        // Arrange & Act
        var beforeCreation = DateTimeOffset.UtcNow;
        var metrics = new SystemMetrics();
        var afterCreation = DateTimeOffset.UtcNow;

        // Assert
        metrics.CalculatedAt.Should().BeOnOrAfter(beforeCreation);
        metrics.CalculatedAt.Should().BeOnOrBefore(afterCreation);
    }
}