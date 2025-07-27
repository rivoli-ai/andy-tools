using Andy.Tools.Advanced.CachingSystem;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Advanced.CachingSystem;

public class SimpleCacheEntryOptionsTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var options = new SimpleCacheEntryOptions();

        // Assert
        options.Should().NotBeNull();
        options.AbsoluteExpiration.Should().BeNull();
        options.SlidingExpiration.Should().BeNull();
        options.Priority.Should().Be(CachePriority.Normal);
        options.SizeBytes.Should().Be(0);
        options.PostEvictionCallbacks.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void AbsoluteExpiration_ShouldBeSettable()
    {
        // Arrange
        var options = new SimpleCacheEntryOptions();
        var expiration = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        options.AbsoluteExpiration = expiration;

        // Assert
        options.AbsoluteExpiration.Should().Be(expiration);
    }

    [Fact]
    public void SlidingExpiration_ShouldBeSettable()
    {
        // Arrange
        var options = new SimpleCacheEntryOptions();
        var expiration = TimeSpan.FromMinutes(30);

        // Act
        options.SlidingExpiration = expiration;

        // Assert
        options.SlidingExpiration.Should().Be(expiration);
    }

    [Fact]
    public void Priority_ShouldBeSettable()
    {
        // Arrange
        var options = new SimpleCacheEntryOptions();

        // Act
        options.Priority = CachePriority.High;

        // Assert
        options.Priority.Should().Be(CachePriority.High);
    }

    [Fact]
    public void SizeBytes_ShouldBeSettable()
    {
        // Arrange
        var options = new SimpleCacheEntryOptions();

        // Act
        options.SizeBytes = 1024;

        // Assert
        options.SizeBytes.Should().Be(1024);
    }

    [Fact]
    public void PostEvictionCallbacks_ShouldBeSettable()
    {
        // Arrange
        var options = new SimpleCacheEntryOptions();
        var callbacks = new List<PostEvictionCallbackRegistration>
        {
            new() { State = "Callback1" },
            new() { State = "Callback2" }
        };

        // Act
        options.PostEvictionCallbacks = callbacks;

        // Assert
        options.PostEvictionCallbacks.Should().BeSameAs(callbacks);
    }

    [Fact]
    public void PostEvictionCallbacks_ShouldSupportAddingItems()
    {
        // Arrange
        var options = new SimpleCacheEntryOptions();
        var callback = new PostEvictionCallbackRegistration
        {
            EvictionCallback = (k, v, r, s) => { },
            State = "TestState"
        };

        // Act
        options.PostEvictionCallbacks.Add(callback);

        // Assert
        options.PostEvictionCallbacks.Should().ContainSingle();
        options.PostEvictionCallbacks.First().Should().BeSameAs(callback);
    }

    [Fact]
    public void Options_ShouldSupportFluentConfiguration()
    {
        // Arrange & Act
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(2);
        var slidingExpiration = TimeSpan.FromMinutes(15);
        
        var options = new SimpleCacheEntryOptions
        {
            AbsoluteExpiration = absoluteExpiration,
            SlidingExpiration = slidingExpiration,
            Priority = CachePriority.Low,
            SizeBytes = 2048,
            PostEvictionCallbacks = new List<PostEvictionCallbackRegistration>
            {
                new() { State = "Callback1" }
            }
        };

        // Assert
        options.AbsoluteExpiration.Should().Be(absoluteExpiration);
        options.SlidingExpiration.Should().Be(slidingExpiration);
        options.Priority.Should().Be(CachePriority.Low);
        options.SizeBytes.Should().Be(2048);
        options.PostEvictionCallbacks.Should().HaveCount(1);
    }

    [Fact]
    public void Options_ShouldBeIndependent_WhenMultipleInstancesCreated()
    {
        // Arrange
        var options1 = new SimpleCacheEntryOptions();
        var options2 = new SimpleCacheEntryOptions();

        // Act
        options1.Priority = CachePriority.High;
        options1.SizeBytes = 1000;
        options1.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration());
        
        options2.Priority = CachePriority.Low;
        options2.SizeBytes = 2000;

        // Assert
        options1.Priority.Should().Be(CachePriority.High);
        options2.Priority.Should().Be(CachePriority.Low);
        
        options1.SizeBytes.Should().Be(1000);
        options2.SizeBytes.Should().Be(2000);
        
        options1.PostEvictionCallbacks.Should().HaveCount(1);
        options2.PostEvictionCallbacks.Should().BeEmpty();
    }

    [Fact]
    public void Options_ShouldAllowBothExpirationTypes()
    {
        // Arrange
        var options = new SimpleCacheEntryOptions();

        // Act
        options.AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1);
        options.SlidingExpiration = TimeSpan.FromMinutes(10);

        // Assert
        options.AbsoluteExpiration.Should().NotBeNull();
        options.SlidingExpiration.Should().NotBeNull();
    }

    [Fact]
    public void Options_ShouldHandleNegativeSlidingExpiration()
    {
        // Arrange
        var options = new SimpleCacheEntryOptions();

        // Act
        options.SlidingExpiration = TimeSpan.FromMinutes(-5);

        // Assert
        options.SlidingExpiration.Should().Be(TimeSpan.FromMinutes(-5));
    }

    [Fact]
    public void Options_ShouldHandlePastAbsoluteExpiration()
    {
        // Arrange
        var options = new SimpleCacheEntryOptions();
        var pastExpiration = DateTimeOffset.UtcNow.AddHours(-1);

        // Act
        options.AbsoluteExpiration = pastExpiration;

        // Assert
        options.AbsoluteExpiration.Should().Be(pastExpiration);
    }

    [Theory]
    [InlineData(CachePriority.Low)]
    [InlineData(CachePriority.Normal)]
    [InlineData(CachePriority.High)]
    [InlineData(CachePriority.NeverEvict)]
    public void Priority_ShouldAcceptAllValidValues(CachePriority priority)
    {
        // Arrange
        var options = new SimpleCacheEntryOptions();

        // Act
        options.Priority = priority;

        // Assert
        options.Priority.Should().Be(priority);
    }

    [Fact]
    public void Options_ShouldHandleRealWorldScenario()
    {
        // Arrange - Configure options for a frequently accessed but large object
        var options = new SimpleCacheEntryOptions
        {
            // Keep for 1 hour max
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1),
            // Reset expiration on access, expire after 15 min of inactivity
            SlidingExpiration = TimeSpan.FromMinutes(15),
            // High priority to avoid eviction
            Priority = CachePriority.High,
            // 10MB object
            SizeBytes = 10 * 1024 * 1024,
            PostEvictionCallbacks = new List<PostEvictionCallbackRegistration>
            {
                new()
                {
                    EvictionCallback = (key, value, reason, state) =>
                    {
                        // Log eviction
                        Console.WriteLine($"Cache entry {key} evicted due to {reason}");
                    },
                    State = new { Logger = "CacheLogger" }
                }
            }
        };

        // Assert
        options.AbsoluteExpiration.Should().BeAfter(DateTimeOffset.UtcNow);
        options.SlidingExpiration.Should().BePositive();
        options.Priority.Should().Be(CachePriority.High);
        options.SizeBytes.Should().BeGreaterThan(1024 * 1024);
        options.PostEvictionCallbacks.Should().NotBeEmpty();
    }
}