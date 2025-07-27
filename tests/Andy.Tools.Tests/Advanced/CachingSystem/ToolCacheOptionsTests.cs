using Andy.Tools.Advanced.CachingSystem;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Advanced.CachingSystem;

public class ToolCacheOptionsTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var options = new ToolCacheOptions();

        // Assert
        options.Should().NotBeNull();
        options.DefaultTimeToLive.Should().Be(TimeSpan.FromMinutes(5));
        options.MaxSizeBytes.Should().Be(100 * 1024 * 1024); // 100MB
        options.CleanupInterval.Should().Be(TimeSpan.FromMinutes(5));
        options.EnableStatistics.Should().BeTrue();
        options.EnableDetailedLogging.Should().BeFalse();
        options.MaxEntriesPerTool.Should().Be(1000);
        options.UseSlidingExpiration.Should().BeTrue();
        options.InvalidateOnToolUpdate.Should().BeTrue();
        options.MemoryPressureThreshold.Should().Be(0.9);
        options.EnableDistributedCache.Should().BeFalse();
        options.DistributedCacheConnectionString.Should().BeNull();
        options.EnableCompression.Should().BeFalse();
        options.CompressionThresholdBytes.Should().Be(1024);
    }

    [Fact]
    public void DefaultTimeToLive_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.DefaultTimeToLive = TimeSpan.FromHours(1);

        // Assert
        options.DefaultTimeToLive.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void MaxSizeBytes_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.MaxSizeBytes = 500 * 1024 * 1024; // 500MB

        // Assert
        options.MaxSizeBytes.Should().Be(500 * 1024 * 1024);
    }

    [Fact]
    public void CleanupInterval_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.CleanupInterval = TimeSpan.FromMinutes(10);

        // Assert
        options.CleanupInterval.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void EnableStatistics_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.EnableStatistics = false;

        // Assert
        options.EnableStatistics.Should().BeFalse();
    }

    [Fact]
    public void EnableDetailedLogging_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.EnableDetailedLogging = true;

        // Assert
        options.EnableDetailedLogging.Should().BeTrue();
    }

    [Fact]
    public void MaxEntriesPerTool_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.MaxEntriesPerTool = 5000;

        // Assert
        options.MaxEntriesPerTool.Should().Be(5000);
    }

    [Fact]
    public void UseSlidingExpiration_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.UseSlidingExpiration = false;

        // Assert
        options.UseSlidingExpiration.Should().BeFalse();
    }

    [Fact]
    public void InvalidateOnToolUpdate_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.InvalidateOnToolUpdate = false;

        // Assert
        options.InvalidateOnToolUpdate.Should().BeFalse();
    }

    [Fact]
    public void MemoryPressureThreshold_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.MemoryPressureThreshold = 0.75;

        // Assert
        options.MemoryPressureThreshold.Should().Be(0.75);
    }

    [Fact]
    public void EnableDistributedCache_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.EnableDistributedCache = true;

        // Assert
        options.EnableDistributedCache.Should().BeTrue();
    }

    [Fact]
    public void DistributedCacheConnectionString_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();
        var connectionString = "redis://localhost:6379";

        // Act
        options.DistributedCacheConnectionString = connectionString;

        // Assert
        options.DistributedCacheConnectionString.Should().Be(connectionString);
    }

    [Fact]
    public void EnableCompression_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.EnableCompression = true;

        // Assert
        options.EnableCompression.Should().BeTrue();
    }

    [Fact]
    public void CompressionThresholdBytes_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.CompressionThresholdBytes = 512;

        // Assert
        options.CompressionThresholdBytes.Should().Be(512);
    }

    [Fact]
    public void Options_ShouldSupportFluentConfiguration()
    {
        // Arrange & Act
        var options = new ToolCacheOptions
        {
            DefaultTimeToLive = TimeSpan.FromHours(2),
            MaxSizeBytes = 1024 * 1024 * 1024, // 1GB
            CleanupInterval = TimeSpan.FromMinutes(30),
            EnableStatistics = false,
            EnableDetailedLogging = true,
            MaxEntriesPerTool = 10000,
            UseSlidingExpiration = false,
            InvalidateOnToolUpdate = false,
            MemoryPressureThreshold = 0.95,
            EnableDistributedCache = true,
            DistributedCacheConnectionString = "redis://cache-server:6379",
            EnableCompression = true,
            CompressionThresholdBytes = 2048
        };

        // Assert
        options.DefaultTimeToLive.Should().Be(TimeSpan.FromHours(2));
        options.MaxSizeBytes.Should().Be(1024 * 1024 * 1024);
        options.CleanupInterval.Should().Be(TimeSpan.FromMinutes(30));
        options.EnableStatistics.Should().BeFalse();
        options.EnableDetailedLogging.Should().BeTrue();
        options.MaxEntriesPerTool.Should().Be(10000);
        options.UseSlidingExpiration.Should().BeFalse();
        options.InvalidateOnToolUpdate.Should().BeFalse();
        options.MemoryPressureThreshold.Should().Be(0.95);
        options.EnableDistributedCache.Should().BeTrue();
        options.DistributedCacheConnectionString.Should().Be("redis://cache-server:6379");
        options.EnableCompression.Should().BeTrue();
        options.CompressionThresholdBytes.Should().Be(2048);
    }

    [Fact]
    public void Options_ShouldBeIndependent_WhenMultipleInstancesCreated()
    {
        // Arrange
        var options1 = new ToolCacheOptions();
        var options2 = new ToolCacheOptions();

        // Act
        options1.DefaultTimeToLive = TimeSpan.FromMinutes(10);
        options1.MaxSizeBytes = 50 * 1024 * 1024;
        
        options2.DefaultTimeToLive = TimeSpan.FromMinutes(30);
        options2.MaxSizeBytes = 200 * 1024 * 1024;

        // Assert
        options1.DefaultTimeToLive.Should().Be(TimeSpan.FromMinutes(10));
        options2.DefaultTimeToLive.Should().Be(TimeSpan.FromMinutes(30));
        
        options1.MaxSizeBytes.Should().Be(50 * 1024 * 1024);
        options2.MaxSizeBytes.Should().Be(200 * 1024 * 1024);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    [InlineData(1.0)]
    public void MemoryPressureThreshold_ShouldAcceptValidRange(double threshold)
    {
        // Arrange
        var options = new ToolCacheOptions();

        // Act
        options.MemoryPressureThreshold = threshold;

        // Assert
        options.MemoryPressureThreshold.Should().Be(threshold);
    }

    [Fact]
    public void Options_ShouldHandleProductionScenario()
    {
        // Arrange - Production configuration
        var options = new ToolCacheOptions
        {
            // 1 hour default cache
            DefaultTimeToLive = TimeSpan.FromHours(1),
            // 2GB max cache size
            MaxSizeBytes = 2L * 1024 * 1024 * 1024,
            // Cleanup every 15 minutes
            CleanupInterval = TimeSpan.FromMinutes(15),
            // Enable all monitoring
            EnableStatistics = true,
            EnableDetailedLogging = false, // Too verbose for production
            // Higher limits per tool
            MaxEntriesPerTool = 5000,
            // Use sliding expiration for better cache utilization
            UseSlidingExpiration = true,
            // Invalidate on updates for consistency
            InvalidateOnToolUpdate = true,
            // Start evicting at 85% memory usage
            MemoryPressureThreshold = 0.85,
            // Enable distributed cache for scaling
            EnableDistributedCache = true,
            DistributedCacheConnectionString = "redis://prod-cache-cluster:6379",
            // Enable compression for large objects
            EnableCompression = true,
            CompressionThresholdBytes = 10 * 1024 // 10KB
        };

        // Assert
        options.DefaultTimeToLive.Should().BeGreaterThan(TimeSpan.FromMinutes(30));
        options.MaxSizeBytes.Should().BeGreaterThan(1024L * 1024 * 1024);
        options.CleanupInterval.Should().BeLessThan(TimeSpan.FromHours(1));
        options.EnableStatistics.Should().BeTrue();
        options.EnableDetailedLogging.Should().BeFalse();
        options.MemoryPressureThreshold.Should().BeLessThan(0.9);
        options.EnableDistributedCache.Should().BeTrue();
        options.DistributedCacheConnectionString.Should().NotBeNullOrEmpty();
        options.EnableCompression.Should().BeTrue();
    }

    [Fact]
    public void Options_ShouldHandleDevelopmentScenario()
    {
        // Arrange - Development configuration
        var options = new ToolCacheOptions
        {
            // Shorter cache for testing
            DefaultTimeToLive = TimeSpan.FromMinutes(1),
            // Smaller cache size for local dev
            MaxSizeBytes = 50 * 1024 * 1024,
            // Frequent cleanup for testing
            CleanupInterval = TimeSpan.FromSeconds(30),
            // Enable all debugging features
            EnableStatistics = true,
            EnableDetailedLogging = true,
            // Lower limits for testing
            MaxEntriesPerTool = 100,
            // Test both expiration types
            UseSlidingExpiration = false,
            InvalidateOnToolUpdate = true,
            // Lower threshold for testing eviction
            MemoryPressureThreshold = 0.5,
            // No distributed cache in dev
            EnableDistributedCache = false,
            // No compression for easier debugging
            EnableCompression = false
        };

        // Assert
        options.DefaultTimeToLive.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
        options.MaxSizeBytes.Should().BeLessThan(100 * 1024 * 1024);
        options.EnableDetailedLogging.Should().BeTrue();
        options.EnableDistributedCache.Should().BeFalse();
        options.EnableCompression.Should().BeFalse();
    }
}