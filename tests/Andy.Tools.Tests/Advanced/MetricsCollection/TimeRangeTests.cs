using Andy.Tools.Advanced.MetricsCollection;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Advanced.MetricsCollection;

public class TimeRangeTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var timeRange = new TimeRange();

        // Assert
        timeRange.Should().NotBeNull();
        timeRange.Start.Should().Be(default(DateTimeOffset));
        timeRange.End.Should().Be(default(DateTimeOffset));
    }

    [Fact]
    public void Start_ShouldBeSettable()
    {
        // Arrange
        var timeRange = new TimeRange();
        var startTime = DateTimeOffset.UtcNow.AddDays(-7);

        // Act
        timeRange.Start = startTime;

        // Assert
        timeRange.Start.Should().Be(startTime);
    }

    [Fact]
    public void End_ShouldBeSettable()
    {
        // Arrange
        var timeRange = new TimeRange();
        var endTime = DateTimeOffset.UtcNow;

        // Act
        timeRange.End = endTime;

        // Assert
        timeRange.End.Should().Be(endTime);
    }

    [Fact]
    public void LastHours_ShouldCreateCorrectTimeRange()
    {
        // Arrange
        var beforeCreation = DateTimeOffset.UtcNow;

        // Act
        var timeRange = TimeRange.LastHours(24);

        // Assert
        var afterCreation = DateTimeOffset.UtcNow;
        timeRange.Start.Should().BeCloseTo(beforeCreation.AddHours(-24), TimeSpan.FromSeconds(1));
        timeRange.End.Should().BeCloseTo(afterCreation, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LastDays_ShouldCreateCorrectTimeRange()
    {
        // Arrange
        var beforeCreation = DateTimeOffset.UtcNow;

        // Act
        var timeRange = TimeRange.LastDays(7);

        // Assert
        var afterCreation = DateTimeOffset.UtcNow;
        timeRange.Start.Should().BeCloseTo(beforeCreation.AddDays(-7), TimeSpan.FromSeconds(1));
        timeRange.End.Should().BeCloseTo(afterCreation, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LastHours_WithZero_ShouldCreatePointInTimeRange()
    {
        // Act
        var timeRange = TimeRange.LastHours(0);

        // Assert
        timeRange.Start.Should().BeCloseTo(timeRange.End, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LastDays_WithZero_ShouldCreatePointInTimeRange()
    {
        // Act
        var timeRange = TimeRange.LastDays(0);

        // Assert
        timeRange.Start.Should().BeCloseTo(timeRange.End, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LastHours_WithNegativeValue_ShouldCreateFutureTimeRange()
    {
        // Act
        var timeRange = TimeRange.LastHours(-1);

        // Assert
        timeRange.Start.Should().BeAfter(timeRange.End);
    }

    [Fact]
    public void TimeRange_ShouldSupportFluentConfiguration()
    {
        // Arrange & Act
        var start = DateTimeOffset.UtcNow.AddMonths(-1);
        var end = DateTimeOffset.UtcNow;
        
        var timeRange = new TimeRange
        {
            Start = start,
            End = end
        };

        // Assert
        timeRange.Start.Should().Be(start);
        timeRange.End.Should().Be(end);
    }

    [Fact]
    public void TimeRange_ShouldBeIndependent_WhenMultipleInstancesCreated()
    {
        // Act
        var range1 = TimeRange.LastHours(1);
        var range2 = TimeRange.LastDays(1);

        // Assert
        range1.Should().NotBeSameAs(range2);
        (range2.Start - range1.Start).Should().BeCloseTo(TimeSpan.FromHours(23), TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(24)]
    [InlineData(48)]
    public void LastHours_ShouldCreateCorrectDuration(int hours)
    {
        // Act
        var timeRange = TimeRange.LastHours(hours);

        // Assert
        var duration = timeRange.End - timeRange.Start;
        duration.Should().BeCloseTo(TimeSpan.FromHours(hours), TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(365)]
    public void LastDays_ShouldCreateCorrectDuration(int days)
    {
        // Act
        var timeRange = TimeRange.LastDays(days);

        // Assert
        var duration = timeRange.End - timeRange.Start;
        duration.Should().BeCloseTo(TimeSpan.FromDays(days), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TimeRange_ShouldHandleCustomRanges()
    {
        // Arrange - Create a range for last month
        var now = DateTimeOffset.UtcNow;
        var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
        var startOfLastMonth = startOfMonth.AddMonths(-1);
        
        // Act
        var timeRange = new TimeRange
        {
            Start = startOfLastMonth,
            End = startOfMonth.AddSeconds(-1) // Last second of previous month
        };

        // Assert
        timeRange.Start.Month.Should().Be(startOfLastMonth.Month);
        timeRange.End.Month.Should().Be(startOfLastMonth.Month);
        timeRange.End.Should().BeBefore(startOfMonth);
    }

    [Fact]
    public void TimeRange_ShouldAllowReversedRange()
    {
        // Arrange
        var later = DateTimeOffset.UtcNow;
        var earlier = later.AddDays(-7);

        // Act
        var timeRange = new TimeRange
        {
            Start = later,
            End = earlier
        };

        // Assert
        timeRange.Start.Should().BeAfter(timeRange.End);
    }
}