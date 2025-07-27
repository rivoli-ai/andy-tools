using Andy.Tools.Advanced.MetricsCollection;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Advanced.MetricsCollection;

public class ResourceUsageMetricsTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var metrics = new ResourceUsageMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.CpuUsagePercent.Should().Be(0);
        metrics.MemoryUsageBytes.Should().Be(0);
        metrics.DiskReadBytes.Should().Be(0);
        metrics.DiskWriteBytes.Should().Be(0);
        metrics.NetworkSentBytes.Should().Be(0);
        metrics.NetworkReceivedBytes.Should().Be(0);
    }

    [Fact]
    public void CpuUsagePercent_ShouldBeSettable()
    {
        // Arrange
        var metrics = new ResourceUsageMetrics();

        // Act
        metrics.CpuUsagePercent = 75.5;

        // Assert
        metrics.CpuUsagePercent.Should().Be(75.5);
    }

    [Fact]
    public void MemoryUsageBytes_ShouldBeSettable()
    {
        // Arrange
        var metrics = new ResourceUsageMetrics();

        // Act
        metrics.MemoryUsageBytes = 1_073_741_824; // 1GB

        // Assert
        metrics.MemoryUsageBytes.Should().Be(1_073_741_824);
    }

    [Fact]
    public void DiskReadBytes_ShouldBeSettable()
    {
        // Arrange
        var metrics = new ResourceUsageMetrics();

        // Act
        metrics.DiskReadBytes = 10_485_760; // 10MB

        // Assert
        metrics.DiskReadBytes.Should().Be(10_485_760);
    }

    [Fact]
    public void DiskWriteBytes_ShouldBeSettable()
    {
        // Arrange
        var metrics = new ResourceUsageMetrics();

        // Act
        metrics.DiskWriteBytes = 5_242_880; // 5MB

        // Assert
        metrics.DiskWriteBytes.Should().Be(5_242_880);
    }

    [Fact]
    public void NetworkSentBytes_ShouldBeSettable()
    {
        // Arrange
        var metrics = new ResourceUsageMetrics();

        // Act
        metrics.NetworkSentBytes = 1_048_576; // 1MB

        // Assert
        metrics.NetworkSentBytes.Should().Be(1_048_576);
    }

    [Fact]
    public void NetworkReceivedBytes_ShouldBeSettable()
    {
        // Arrange
        var metrics = new ResourceUsageMetrics();

        // Act
        metrics.NetworkReceivedBytes = 2_097_152; // 2MB

        // Assert
        metrics.NetworkReceivedBytes.Should().Be(2_097_152);
    }

    [Fact]
    public void ResourceUsageMetrics_ShouldSupportFluentConfiguration()
    {
        // Arrange & Act
        var metrics = new ResourceUsageMetrics
        {
            CpuUsagePercent = 45.5,
            MemoryUsageBytes = 512_000_000,
            DiskReadBytes = 100_000,
            DiskWriteBytes = 50_000,
            NetworkSentBytes = 25_000,
            NetworkReceivedBytes = 75_000
        };

        // Assert
        metrics.CpuUsagePercent.Should().Be(45.5);
        metrics.MemoryUsageBytes.Should().Be(512_000_000);
        metrics.DiskReadBytes.Should().Be(100_000);
        metrics.DiskWriteBytes.Should().Be(50_000);
        metrics.NetworkSentBytes.Should().Be(25_000);
        metrics.NetworkReceivedBytes.Should().Be(75_000);
    }

    [Fact]
    public void ResourceUsageMetrics_ShouldBeIndependent_WhenMultipleInstancesCreated()
    {
        // Arrange
        var metrics1 = new ResourceUsageMetrics();
        var metrics2 = new ResourceUsageMetrics();

        // Act
        metrics1.CpuUsagePercent = 50.0;
        metrics1.MemoryUsageBytes = 1_000_000;
        
        metrics2.CpuUsagePercent = 75.0;
        metrics2.MemoryUsageBytes = 2_000_000;

        // Assert
        metrics1.CpuUsagePercent.Should().Be(50.0);
        metrics2.CpuUsagePercent.Should().Be(75.0);
        
        metrics1.MemoryUsageBytes.Should().Be(1_000_000);
        metrics2.MemoryUsageBytes.Should().Be(2_000_000);
    }

    [Fact]
    public void ResourceUsageMetrics_ShouldHandleRealWorldScenario()
    {
        // Arrange - Simulate resource usage during heavy load
        var metrics = new ResourceUsageMetrics
        {
            CpuUsagePercent = 85.7,
            MemoryUsageBytes = 3_221_225_472, // ~3GB
            DiskReadBytes = 104_857_600,      // 100MB
            DiskWriteBytes = 52_428_800,      // 50MB
            NetworkSentBytes = 10_485_760,    // 10MB
            NetworkReceivedBytes = 20_971_520 // 20MB
        };

        // Act & Assert
        metrics.CpuUsagePercent.Should().BeGreaterThan(80);
        metrics.MemoryUsageBytes.Should().BeGreaterThan(3_000_000_000);
        metrics.DiskReadBytes.Should().BeGreaterThan(metrics.DiskWriteBytes);
        metrics.NetworkReceivedBytes.Should().BeGreaterThan(metrics.NetworkSentBytes);
    }

    [Fact]
    public void ResourceUsageMetrics_ShouldHandleZeroValues()
    {
        // Arrange
        var metrics = new ResourceUsageMetrics();

        // Act - All values remain at default (0)

        // Assert
        metrics.CpuUsagePercent.Should().Be(0);
        metrics.MemoryUsageBytes.Should().Be(0);
        metrics.DiskReadBytes.Should().Be(0);
        metrics.DiskWriteBytes.Should().Be(0);
        metrics.NetworkSentBytes.Should().Be(0);
        metrics.NetworkReceivedBytes.Should().Be(0);
    }

    [Fact]
    public void ResourceUsageMetrics_ShouldHandleMaxValues()
    {
        // Arrange
        var metrics = new ResourceUsageMetrics
        {
            CpuUsagePercent = 100.0,
            MemoryUsageBytes = long.MaxValue,
            DiskReadBytes = long.MaxValue,
            DiskWriteBytes = long.MaxValue,
            NetworkSentBytes = long.MaxValue,
            NetworkReceivedBytes = long.MaxValue
        };

        // Assert
        metrics.CpuUsagePercent.Should().Be(100.0);
        metrics.MemoryUsageBytes.Should().Be(long.MaxValue);
        metrics.DiskReadBytes.Should().Be(long.MaxValue);
        metrics.DiskWriteBytes.Should().Be(long.MaxValue);
        metrics.NetworkSentBytes.Should().Be(long.MaxValue);
        metrics.NetworkReceivedBytes.Should().Be(long.MaxValue);
    }
}