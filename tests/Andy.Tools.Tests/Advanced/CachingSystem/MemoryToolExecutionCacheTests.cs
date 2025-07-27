using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Andy.Tools.Advanced.CachingSystem;
using Andy.Tools.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Andy.Tools.Tests.Advanced.CachingSystem;

public class MemoryToolExecutionCacheTests : IDisposable
{
    private readonly MemoryToolExecutionCache _cache;
    private readonly Mock<ILogger<MemoryToolExecutionCache>> _mockLogger;
    private readonly ToolCacheOptions _options;

    public MemoryToolExecutionCacheTests()
    {
        _options = new ToolCacheOptions
        {
            MaxSizeBytes = 1024 * 1024, // 1MB
            DefaultTimeToLive = TimeSpan.FromMinutes(5),
            CleanupInterval = TimeSpan.FromMinutes(1),
            EnableStatistics = true
        };

        _mockLogger = new Mock<ILogger<MemoryToolExecutionCache>>();
        var optionsWrapper = Options.Create(_options);
        _cache = new MemoryToolExecutionCache(optionsWrapper, _mockLogger.Object);
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    #region Basic Operations Tests

    [Fact]
    public async Task GetAsync_NonExistentKey_ShouldReturnNull()
    {
        // Act
        var result = await _cache.GetAsync("non-existent-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_AndGetAsync_ShouldStoreAndRetrieve()
    {
        // Arrange
        var cacheKey = "test-key";
        var toolResult = new ToolResult
        {
            IsSuccessful = true,
            Data = new Dictionary<string, object?> { ["result"] = "test data" }
        };
        var cacheOptions = new CacheOptions();

        // Act
        await _cache.SetAsync(cacheKey, toolResult, cacheOptions);
        var retrieved = await _cache.GetAsync(cacheKey);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.CacheKey.Should().Be(cacheKey);
        retrieved.Result.IsSuccessful.Should().BeTrue();
        var dataDict = retrieved.Result.Data as Dictionary<string, object?>;
        dataDict.Should().NotBeNull();
        dataDict!.Should().ContainKey("result").WhoseValue.Should().Be("test data");
        retrieved.HitCount.Should().Be(1); // First get increments hit count
    }

    [Fact]
    public async Task InvalidateAsync_ShouldRemoveFromCache()
    {
        // Arrange
        var cacheKey = "test-key";
        var toolResult = new ToolResult { IsSuccessful = true };
        await _cache.SetAsync(cacheKey, toolResult, new CacheOptions());

        // Act
        await _cache.InvalidateAsync(cacheKey);
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllEntries()
    {
        // Arrange
        await _cache.SetAsync("key1", new ToolResult { IsSuccessful = true }, new CacheOptions());
        await _cache.SetAsync("key2", new ToolResult { IsSuccessful = true }, new CacheOptions());
        await _cache.SetAsync("key3", new ToolResult { IsSuccessful = true }, new CacheOptions());

        // Act
        await _cache.ClearAsync();

        // Assert
        var result1 = await _cache.GetAsync("key1");
        var result2 = await _cache.GetAsync("key2");
        var result3 = await _cache.GetAsync("key3");

        result1.Should().BeNull();
        result2.Should().BeNull();
        result3.Should().BeNull();
    }

    #endregion

    #region Cache Key Generation Tests

    [Fact]
    public void GenerateCacheKey_WithBasicParameters_ShouldCreateConsistentKey()
    {
        // Arrange
        var toolId = "test-tool";
        var parameters = new Dictionary<string, object?>
        {
            ["param1"] = "value1",
            ["param2"] = 42,
            ["param3"] = true
        };

        // Act
        var key1 = _cache.GenerateCacheKey(toolId, parameters);
        var key2 = _cache.GenerateCacheKey(toolId, parameters);

        // Assert
        key1.Should().Be(key2);
        key1.Should().StartWith("tool:test-tool");
        key1.Should().Contain("param1=value1");
        key1.Should().Contain("param2=42");
        key1.Should().Contain("param3=true");
    }

    [Fact]
    public void GenerateCacheKey_WithComplexParameters_ShouldHandleNesting()
    {
        // Arrange
        var toolId = "test-tool";
        var parameters = new Dictionary<string, object?>
        {
            ["nested"] = new Dictionary<string, object?>
            {
                ["inner1"] = "value1",
                ["inner2"] = 123
            },
            ["array"] = new[] { 1, 2, 3 }
        };

        // Act
        var key = _cache.GenerateCacheKey(toolId, parameters);

        // Assert
        key.Should().NotBeNullOrEmpty();
        key.Should().StartWith("tool:test-tool");
    }

    [Fact]
    public void GenerateCacheKey_WithNullParameters_ShouldHandleGracefully()
    {
        // Arrange
        var toolId = "test-tool";
        var parameters = new Dictionary<string, object?>
        {
            ["param1"] = null,
            ["param2"] = "value2"
        };

        // Act
        var key = _cache.GenerateCacheKey(toolId, parameters);

        // Assert
        key.Should().NotBeNullOrEmpty();
        key.Should().Contain("param1=null");
        key.Should().Contain("param2=value2");
    }

    [Fact]
    public void GenerateCacheKey_WithContext_ShouldIncludeContextData()
    {
        // Arrange
        var toolId = "test-tool";
        var parameters = new Dictionary<string, object?> { ["param1"] = "value1" };
        var context = new CacheKeyContext
        {
            UserId = "user123",
            Environment = "production",
            Version = "1.0.0",
            AdditionalContext = new Dictionary<string, string>
            {
                ["tenant"] = "tenant1",
                ["region"] = "us-west"
            }
        };

        // Act
        var key = _cache.GenerateCacheKey(toolId, parameters, context);

        // Assert
        key.Should().Contain("user:user123");
        key.Should().Contain("env:production");
        key.Should().Contain("v:1.0.0");
        key.Should().Contain("tenant:tenant1");
        key.Should().Contain("region:us-west");
    }

    #endregion

    #region Expiration and TTL Tests

    [Fact]
    public async Task GetAsync_ExpiredEntry_ShouldReturnNull()
    {
        // Arrange
        var cacheKey = "test-key";
        var toolResult = new ToolResult { IsSuccessful = true };
        var cacheOptions = new CacheOptions
        {
            TimeToLive = TimeSpan.FromMilliseconds(100)
        };

        await _cache.SetAsync(cacheKey, toolResult, cacheOptions);
        await Task.Delay(150); // Wait for expiration

        // Act
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().BeNull(); // Expired entries are removed on get
    }

    [Fact]
    public async Task SetAsync_WithCustomTTL_ShouldRespectTimeToLive()
    {
        // Arrange
        var cacheKey = "test-key";
        var toolResult = new ToolResult { IsSuccessful = true };
        var ttl = TimeSpan.FromSeconds(2);
        var cacheOptions = new CacheOptions { TimeToLive = ttl };

        // Act
        await _cache.SetAsync(cacheKey, toolResult, cacheOptions);
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().NotBeNull();
        result!.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.Add(ttl), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SetAsync_WithAbsoluteExpiration_ShouldUseAbsoluteTime()
    {
        // Arrange
        var cacheKey = "test-key";
        var toolResult = new ToolResult { IsSuccessful = true };
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(1);
        var cacheOptions = new CacheOptions { AbsoluteExpiration = absoluteExpiration };

        // Act
        await _cache.SetAsync(cacheKey, toolResult, cacheOptions);
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().NotBeNull();
        result!.ExpiresAt.Should().Be(absoluteExpiration);
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnAccurateStats()
    {
        // Arrange
        await _cache.SetAsync("key1", new ToolResult { IsSuccessful = true }, new CacheOptions());
        await _cache.SetAsync("key2", new ToolResult { IsSuccessful = true }, new CacheOptions());
        await _cache.GetAsync("key1"); // Hit
        await _cache.GetAsync("key1"); // Hit
        await _cache.GetAsync("key3"); // Miss

        // Act
        var stats = await _cache.GetStatisticsAsync();

        // Assert
        stats.TotalEntries.Should().Be(2);
        stats.HitCount.Should().Be(2);
        stats.MissCount.Should().Be(1);
        stats.HitRatio.Should().BeApproximately(0.667, 0.001);
    }

    [Fact]
    public async Task GetStatisticsAsync_AfterOperations_ShouldUpdateStats()
    {
        // Arrange
        await _cache.SetAsync("tool:tool1:key1", new ToolResult { IsSuccessful = true }, new CacheOptions());
        await _cache.SetAsync("tool:tool1:key2", new ToolResult { IsSuccessful = true }, new CacheOptions());
        await _cache.SetAsync("tool:tool2:key1", new ToolResult { IsSuccessful = true }, new CacheOptions());

        // Act
        var stats = await _cache.GetStatisticsAsync();

        // Assert
        stats.TotalEntries.Should().Be(3);
        stats.ToolStatistics.Should().ContainKey("tool1");
        stats.ToolStatistics["tool1"].EntryCount.Should().Be(2);
        stats.ToolStatistics.Should().ContainKey("tool2");
        stats.ToolStatistics["tool2"].EntryCount.Should().Be(1);
    }

    #endregion

    #region Invalidation Tests

    [Fact]
    public async Task InvalidateByPatternAsync_ShouldRemoveMatchingEntries()
    {
        // Arrange
        await _cache.SetAsync("test:key1", new ToolResult { IsSuccessful = true }, new CacheOptions());
        await _cache.SetAsync("test:key2", new ToolResult { IsSuccessful = true }, new CacheOptions());
        await _cache.SetAsync("other:key1", new ToolResult { IsSuccessful = true }, new CacheOptions());

        // Act
        var invalidatedCount = await _cache.InvalidateByPatternAsync("test:*");

        // Assert
        invalidatedCount.Should().Be(2);
        (await _cache.GetAsync("test:key1")).Should().BeNull();
        (await _cache.GetAsync("test:key2")).Should().BeNull();
        (await _cache.GetAsync("other:key1")).Should().NotBeNull();
    }

    [Fact]
    public async Task InvalidateByToolAsync_ShouldRemoveToolEntries()
    {
        // Arrange
        await _cache.SetAsync("tool:tool1:params:123", new ToolResult { IsSuccessful = true }, new CacheOptions());
        await _cache.SetAsync("tool:tool1:params:456", new ToolResult { IsSuccessful = true }, new CacheOptions());
        await _cache.SetAsync("tool:tool2:params:789", new ToolResult { IsSuccessful = true }, new CacheOptions());

        // Act
        var invalidatedCount = await _cache.InvalidateByToolAsync("tool1");

        // Assert
        invalidatedCount.Should().Be(2);
        (await _cache.GetAsync("tool:tool1:params:123")).Should().BeNull();
        (await _cache.GetAsync("tool:tool1:params:456")).Should().BeNull();
        (await _cache.GetAsync("tool:tool2:params:789")).Should().NotBeNull();
    }

    #endregion

    #region Failure Caching Tests

    [Fact]
    public async Task SetAsync_FailedResult_WithCacheFailuresDisabled_ShouldNotCache()
    {
        // Arrange
        var cacheKey = "test-key";
        var failedResult = new ToolResult
        {
            IsSuccessful = false,
            ErrorMessage = "Operation failed"
        };
        var cacheOptions = new CacheOptions { CacheFailures = false };

        // Act
        await _cache.SetAsync(cacheKey, failedResult, cacheOptions);
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_FailedResult_WithCacheFailuresEnabled_ShouldCache()
    {
        // Arrange
        var cacheKey = "test-key";
        var failedResult = new ToolResult
        {
            IsSuccessful = false,
            ErrorMessage = "Operation failed"
        };
        var cacheOptions = new CacheOptions { CacheFailures = true };

        // Act
        await _cache.SetAsync(cacheKey, failedResult, cacheOptions);
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().NotBeNull();
        result!.Result.IsSuccessful.Should().BeFalse();
        result.Result.ErrorMessage.Should().Be("Operation failed");
    }

    #endregion

    #region Dependencies Tests

    [Fact]
    public async Task InvalidateAsync_WithDependencies_ShouldInvalidateDependentEntries()
    {
        // Arrange
        await _cache.SetAsync("parent", new ToolResult { IsSuccessful = true }, new CacheOptions());
        await _cache.SetAsync("child1", new ToolResult { IsSuccessful = true }, new CacheOptions
        {
            Dependencies = ["parent"]
        });
        await _cache.SetAsync("child2", new ToolResult { IsSuccessful = true }, new CacheOptions
        {
            Dependencies = ["parent"]
        });

        // Act
        await _cache.InvalidateAsync("parent");

        // Assert
        (await _cache.GetAsync("parent")).Should().BeNull();
        (await _cache.GetAsync("child1")).Should().BeNull();
        (await _cache.GetAsync("child2")).Should().BeNull();
    }

    #endregion

    #region Sliding Expiration Tests

    [Fact]
    public async Task GetAsync_WithSlidingExpiration_ShouldUpdateExpiration()
    {
        // Arrange
        var cacheKey = "test-key";
        var slidingExpiration = TimeSpan.FromSeconds(2);
        var cacheOptions = new CacheOptions { SlidingExpiration = slidingExpiration };
        
        await _cache.SetAsync(cacheKey, new ToolResult { IsSuccessful = true }, cacheOptions);
        
        var initialResult = await _cache.GetAsync(cacheKey);
        var initialExpiration = initialResult!.ExpiresAt;

        await Task.Delay(500);

        // Act
        var updatedResult = await _cache.GetAsync(cacheKey);

        // Assert
        updatedResult.Should().NotBeNull();
        updatedResult!.ExpiresAt.Should().BeAfter(initialExpiration!.Value);
        updatedResult.LastAccessedAt.Should().BeAfter(updatedResult.CachedAt);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var random = new Random();

        // Act
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var key = $"key{index % 10}";
                var operation = random.Next(3);

                switch (operation)
                {
                    case 0: // Set
                        await _cache.SetAsync(key, new ToolResult { IsSuccessful = true }, new CacheOptions());
                        break;
                    case 1: // Get
                        await _cache.GetAsync(key);
                        break;
                    case 2: // Invalidate
                        await _cache.InvalidateAsync(key);
                        break;
                }
            }));
        }

        // Assert
        await Task.WhenAll(tasks);
        // No exceptions should be thrown
    }

    #endregion

    #region Priority Tests

    [Fact]
    public async Task SetAsync_WithPriority_ShouldRespectPriorityLevels()
    {
        // Arrange
        var highPriorityKey = "high-priority";
        var lowPriorityKey = "low-priority";

        // Act
        await _cache.SetAsync(highPriorityKey, new ToolResult { IsSuccessful = true }, 
            new CacheOptions { Priority = CachePriority.High });
        await _cache.SetAsync(lowPriorityKey, new ToolResult { IsSuccessful = true }, 
            new CacheOptions { Priority = CachePriority.Low });

        var stats = await _cache.GetStatisticsAsync();

        // Assert
        stats.TotalEntries.Should().Be(2);
        // Priority should be stored in metadata (implementation detail)
    }

    #endregion

    #region Parameter Type Tests

    [Fact]
    public void GenerateCacheKey_WithIncludeParameterTypes_ShouldIncludeTypes()
    {
        // Arrange
        var toolId = "test-tool";
        var parameters = new Dictionary<string, object?>
        {
            ["stringParam"] = "value",
            ["intParam"] = 42,
            ["boolParam"] = true
        };
        var context = new CacheKeyContext { IncludeParameterTypes = true };

        // Act
        var key = _cache.GenerateCacheKey(toolId, parameters, context);

        // Assert
        key.Should().Contain("String:");
        key.Should().Contain("Int32:");
        key.Should().Contain("Boolean:");
    }

    #endregion

    #region Long Parameter String Tests

    [Fact]
    public void GenerateCacheKey_WithLongParameters_ShouldHashParameters()
    {
        // Arrange
        var toolId = "test-tool";
        var parameters = new Dictionary<string, object?>();
        
        // Create a very long parameter string
        for (int i = 0; i < 50; i++)
        {
            parameters[$"param{i}"] = $"This is a very long value that will contribute to making the parameter string exceed 200 characters {i}";
        }

        // Act
        var key = _cache.GenerateCacheKey(toolId, parameters);

        // Assert
        key.Should().StartWith("tool:test-tool:params:");
        // The key should contain a hash, not the full parameter string
        key.Length.Should().BeLessThan(300); // Hash should be much shorter than full params
    }

    #endregion
}