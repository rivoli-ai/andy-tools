using Andy.Tools.Advanced.MetricsCollection;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Advanced.MetricsCollection;

public class PerformanceTrendTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var trend = new PerformanceTrend();

        // Assert
        trend.Should().NotBeNull();
        trend.Timestamp.Should().Be(default(DateTimeOffset));
        trend.ExecutionCount.Should().Be(0);
        trend.SuccessRate.Should().Be(0);
        trend.AverageDurationMs.Should().Be(0);
        trend.CacheHitRate.Should().Be(0);
        trend.CustomMetrics.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Timestamp_ShouldBeSettable()
    {
        // Arrange
        var trend = new PerformanceTrend();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        trend.Timestamp = timestamp;

        // Assert
        trend.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void ExecutionCount_ShouldBeSettable()
    {
        // Arrange
        var trend = new PerformanceTrend();

        // Act
        trend.ExecutionCount = 5000;

        // Assert
        trend.ExecutionCount.Should().Be(5000);
    }

    [Fact]
    public void SuccessRate_ShouldBeSettable()
    {
        // Arrange
        var trend = new PerformanceTrend();

        // Act
        trend.SuccessRate = 0.95;

        // Assert
        trend.SuccessRate.Should().Be(0.95);
    }

    [Fact]
    public void AverageDurationMs_ShouldBeSettable()
    {
        // Arrange
        var trend = new PerformanceTrend();

        // Act
        trend.AverageDurationMs = 125.5;

        // Assert
        trend.AverageDurationMs.Should().Be(125.5);
    }

    [Fact]
    public void CacheHitRate_ShouldBeSettable()
    {
        // Arrange
        var trend = new PerformanceTrend();

        // Act
        trend.CacheHitRate = 0.75;

        // Assert
        trend.CacheHitRate.Should().Be(0.75);
    }

    [Fact]
    public void CustomMetrics_ShouldBeSettable()
    {
        // Arrange
        var trend = new PerformanceTrend();
        var customMetrics = new Dictionary<string, double>
        {
            ["cpu_usage"] = 45.5,
            ["memory_mb"] = 512.0
        };

        // Act
        trend.CustomMetrics = customMetrics;

        // Assert
        trend.CustomMetrics.Should().BeSameAs(customMetrics);
    }

    [Fact]
    public void CustomMetrics_ShouldSupportAddingItems()
    {
        // Arrange
        var trend = new PerformanceTrend();

        // Act
        trend.CustomMetrics["latency_p95"] = 200.0;
        trend.CustomMetrics["latency_p99"] = 500.0;
        trend.CustomMetrics["error_rate"] = 0.02;

        // Assert
        trend.CustomMetrics.Should().HaveCount(3);
        trend.CustomMetrics["latency_p95"].Should().Be(200.0);
        trend.CustomMetrics["latency_p99"].Should().Be(500.0);
        trend.CustomMetrics["error_rate"].Should().Be(0.02);
    }

    [Fact]
    public void PerformanceTrend_ShouldSupportFluentConfiguration()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var trend = new PerformanceTrend
        {
            Timestamp = timestamp,
            ExecutionCount = 1000,
            SuccessRate = 0.98,
            AverageDurationMs = 50.0,
            CacheHitRate = 0.85,
            CustomMetrics = new Dictionary<string, double>
            {
                ["queue_depth"] = 10.0,
                ["active_connections"] = 25.0
            }
        };

        // Assert
        trend.Timestamp.Should().Be(timestamp);
        trend.ExecutionCount.Should().Be(1000);
        trend.SuccessRate.Should().Be(0.98);
        trend.AverageDurationMs.Should().Be(50.0);
        trend.CacheHitRate.Should().Be(0.85);
        trend.CustomMetrics.Should().HaveCount(2);
    }

    [Fact]
    public void PerformanceTrend_ShouldBeIndependent_WhenMultipleInstancesCreated()
    {
        // Arrange
        var trend1 = new PerformanceTrend();
        var trend2 = new PerformanceTrend();

        // Act
        trend1.ExecutionCount = 100;
        trend1.SuccessRate = 0.9;
        trend1.CustomMetrics["metric1"] = 1.0;
        
        trend2.ExecutionCount = 200;
        trend2.SuccessRate = 0.95;
        trend2.CustomMetrics["metric2"] = 2.0;

        // Assert
        trend1.ExecutionCount.Should().Be(100);
        trend2.ExecutionCount.Should().Be(200);
        
        trend1.SuccessRate.Should().Be(0.9);
        trend2.SuccessRate.Should().Be(0.95);
        
        trend1.CustomMetrics.Should().ContainSingle().Which.Key.Should().Be("metric1");
        trend2.CustomMetrics.Should().ContainSingle().Which.Key.Should().Be("metric2");
    }

    [Fact]
    public void PerformanceTrend_ShouldHandleRealWorldScenario()
    {
        // Arrange - Simulate performance metrics over time
        var trends = new List<PerformanceTrend>();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-1);

        // Act - Create hourly trend data
        for (int i = 0; i < 5; i++)
        {
            trends.Add(new PerformanceTrend
            {
                Timestamp = baseTime.AddMinutes(i * 15),
                ExecutionCount = 1000 + (i * 100),
                SuccessRate = 0.95 + (i * 0.01),
                AverageDurationMs = 100 - (i * 10),
                CacheHitRate = 0.7 + (i * 0.05),
                CustomMetrics = new Dictionary<string, double>
                {
                    ["cpu_percent"] = 30 + (i * 5),
                    ["memory_mb"] = 256 + (i * 50)
                }
            });
        }

        // Assert
        trends.Should().HaveCount(5);
        trends.First().ExecutionCount.Should().Be(1000);
        trends.Last().ExecutionCount.Should().Be(1400);
        trends.First().SuccessRate.Should().Be(0.95);
        trends.Last().SuccessRate.Should().Be(0.99);
        trends.All(t => t.CustomMetrics.ContainsKey("cpu_percent")).Should().BeTrue();
        trends.All(t => t.CustomMetrics.ContainsKey("memory_mb")).Should().BeTrue();
    }
}