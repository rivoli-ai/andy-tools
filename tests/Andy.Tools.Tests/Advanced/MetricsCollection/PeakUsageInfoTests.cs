using Andy.Tools.Advanced.MetricsCollection;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Advanced.MetricsCollection;

public class PeakUsageInfoTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var info = new PeakUsageInfo();

        // Assert
        info.Should().NotBeNull();
        info.TimePeriod.Should().Be(default(DateTimeOffset));
        info.ExecutionCount.Should().Be(0);
        info.AverageResponseTimeMs.Should().Be(0);
    }

    [Fact]
    public void TimePeriod_ShouldBeSettable()
    {
        // Arrange
        var info = new PeakUsageInfo();
        var timePeriod = DateTimeOffset.UtcNow;

        // Act
        info.TimePeriod = timePeriod;

        // Assert
        info.TimePeriod.Should().Be(timePeriod);
    }

    [Fact]
    public void ExecutionCount_ShouldBeSettable()
    {
        // Arrange
        var info = new PeakUsageInfo();

        // Act
        info.ExecutionCount = 1000;

        // Assert
        info.ExecutionCount.Should().Be(1000);
    }

    [Fact]
    public void AverageResponseTimeMs_ShouldBeSettable()
    {
        // Arrange
        var info = new PeakUsageInfo();

        // Act
        info.AverageResponseTimeMs = 123.45;

        // Assert
        info.AverageResponseTimeMs.Should().Be(123.45);
    }

    [Fact]
    public void PeakUsageInfo_ShouldSupportFluentConfiguration()
    {
        // Arrange & Act
        var timePeriod = DateTimeOffset.UtcNow.AddHours(-1);
        var info = new PeakUsageInfo
        {
            TimePeriod = timePeriod,
            ExecutionCount = 500,
            AverageResponseTimeMs = 25.5
        };

        // Assert
        info.TimePeriod.Should().Be(timePeriod);
        info.ExecutionCount.Should().Be(500);
        info.AverageResponseTimeMs.Should().Be(25.5);
    }

    [Fact]
    public void PeakUsageInfo_ShouldBeIndependent_WhenMultipleInstancesCreated()
    {
        // Arrange
        var info1 = new PeakUsageInfo();
        var info2 = new PeakUsageInfo();

        // Act
        info1.ExecutionCount = 100;
        info1.AverageResponseTimeMs = 10.0;
        
        info2.ExecutionCount = 200;
        info2.AverageResponseTimeMs = 20.0;

        // Assert
        info1.ExecutionCount.Should().Be(100);
        info2.ExecutionCount.Should().Be(200);
        
        info1.AverageResponseTimeMs.Should().Be(10.0);
        info2.AverageResponseTimeMs.Should().Be(20.0);
    }

    [Fact]
    public void PeakUsageInfo_ShouldHandleRealWorldScenario()
    {
        // Arrange - Simulate peak usage data for an hour
        var peakHour = DateTimeOffset.UtcNow.Date.AddHours(14); // 2 PM
        
        // Act
        var info = new PeakUsageInfo
        {
            TimePeriod = peakHour,
            ExecutionCount = 15000,
            AverageResponseTimeMs = 45.7
        };

        // Assert
        info.TimePeriod.Should().BeCloseTo(peakHour, TimeSpan.FromSeconds(1));
        info.ExecutionCount.Should().Be(15000);
        info.AverageResponseTimeMs.Should().Be(45.7);
    }
}