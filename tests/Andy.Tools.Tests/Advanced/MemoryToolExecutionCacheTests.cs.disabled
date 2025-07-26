using Andy.Tools.Advanced;
using Andy.Tools.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Andy.Tools.Tests.Advanced;

public class MemoryToolExecutionCacheTests : IDisposable
{
    private readonly Mock<ILogger<MemoryToolExecutionCache>> _mockLogger;
    private readonly IOptions<ToolCacheOptions> _options;
    private readonly MemoryToolExecutionCache _cache;
    private readonly ToolResult _testResult;
    private readonly CacheOptions _defaultCacheOptions;

    public MemoryToolExecutionCacheTests()
    {
        _mockLogger = new Mock<ILogger<MemoryToolExecutionCache>>();

        _options = Options.Create(new ToolCacheOptions
        {
            DefaultTimeToLive = TimeSpan.FromMinutes(5),
            CleanupInterval = TimeSpan.FromHours(1), // Long interval for tests
            MaxSizeBytes = 1024 * 1024,
            EnableStatistics = true
        });

        _cache = new MemoryToolExecutionCache(_options, _mockLogger.Object);

        _testResult = new ToolResult
        {
            IsSuccessful = true,
            Data = "test data",
            DurationMs = 100,
            Metadata = new Dictionary<string, object?> { ["test"] = "value" }
        };

        _defaultCacheOptions = new CacheOptions
        {
            TimeToLive = TimeSpan.FromMinutes(10),
            Priority = CachePriority.Normal,
            CacheFailures = false
        };
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeSuccessfully()
    {
        // Assert
        _cache.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullMemoryCache_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MemoryToolExecutionCache(null!, _options, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MemoryToolExecutionCache(_memoryCache, null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MemoryToolExecutionCache(_memoryCache, _options, null!));
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WithNonExistentKey_ShouldReturnNull()
    {
        // Act
        var result = await _cache.GetAsync("non-existent-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WithValidCachedEntry_ShouldReturnCachedResult()
    {
        // Arrange
        var cacheKey = "test-key";
        await _cache.SetAsync(cacheKey, _testResult, _defaultCacheOptions);

        // Act
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().NotBeNull();
        result!.Result.Should().BeEquivalentTo(_testResult);
        result.CacheKey.Should().Be(cacheKey);
        result.HitCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_WithExpiredEntry_ShouldReturnNull()
    {
        // Arrange
        var cacheKey = "expired-key";
        var expiredOptions = new CacheOptions
        {
            TimeToLive = TimeSpan.FromMilliseconds(1), // Very short TTL
            Priority = CachePriority.Normal
        };

        await _cache.SetAsync(cacheKey, _testResult, expiredOptions);
        await Task.Delay(50); // Wait for expiration

        // Act
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WithSlidingExpiration_ShouldUpdateExpiration()
    {
        // Arrange
        var cacheKey = "sliding-key";
        var slidingOptions = new CacheOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Priority = CachePriority.Normal
        };

        await _cache.SetAsync(cacheKey, _testResult, slidingOptions);
        var firstResult = await _cache.GetAsync(cacheKey);
        var firstExpiration = firstResult!.ExpiresAt;

        await Task.Delay(10); // Small delay

        // Act
        var secondResult = await _cache.GetAsync(cacheKey);

        // Assert
        secondResult.Should().NotBeNull();
        secondResult!.ExpiresAt.Should().BeAfter(firstExpiration!.Value);
    }

    [Fact]
    public async Task GetAsync_MultipleHits_ShouldIncrementHitCount()
    {
        // Arrange
        var cacheKey = "hit-count-key";
        await _cache.SetAsync(cacheKey, _testResult, _defaultCacheOptions);

        // Act
        var firstHit = await _cache.GetAsync(cacheKey);
        var secondHit = await _cache.GetAsync(cacheKey);
        var thirdHit = await _cache.GetAsync(cacheKey);

        // Assert
        firstHit!.HitCount.Should().Be(1);
        secondHit!.HitCount.Should().Be(2);
        thirdHit!.HitCount.Should().Be(3);
    }

    #endregion

    #region SetAsync Tests

    [Fact]
    public async Task SetAsync_WithSuccessfulResult_ShouldCacheResult()
    {
        // Arrange
        var cacheKey = "success-key";

        // Act
        await _cache.SetAsync(cacheKey, _testResult, _defaultCacheOptions);
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().NotBeNull();
        result!.Result.Should().BeEquivalentTo(_testResult);
    }

    [Fact]
    public async Task SetAsync_WithFailedResultAndCacheFailuresFalse_ShouldNotCache()
    {
        // Arrange
        var cacheKey = "failed-key";
        var failedResult = new ToolResult
        {
            IsSuccessful = false,
            ErrorMessage = "Test error"
        };
        var options = new CacheOptions
        {
            CacheFailures = false,
            TimeToLive = TimeSpan.FromMinutes(5)
        };

        // Act
        await _cache.SetAsync(cacheKey, failedResult, options);
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_WithFailedResultAndCacheFailuresTrue_ShouldCache()
    {
        // Arrange
        var cacheKey = "failed-cached-key";
        var failedResult = new ToolResult
        {
            IsSuccessful = false,
            ErrorMessage = "Test error"
        };
        var options = new CacheOptions
        {
            CacheFailures = true,
            TimeToLive = TimeSpan.FromMinutes(5)
        };

        // Act
        await _cache.SetAsync(cacheKey, failedResult, options);
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().NotBeNull();
        result!.Result.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task SetAsync_WithAbsoluteExpiration_ShouldRespectAbsoluteExpiration()
    {
        // Arrange
        var cacheKey = "absolute-exp-key";
        var futureTime = DateTimeOffset.UtcNow.AddMinutes(10);
        var options = new CacheOptions
        {
            AbsoluteExpiration = futureTime,
            Priority = CachePriority.Normal
        };

        // Act
        await _cache.SetAsync(cacheKey, _testResult, options);
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().NotBeNull();
        result!.ExpiresAt.Should().Be(futureTime);
    }

    [Fact]
    public async Task SetAsync_WithDependencies_ShouldTrackDependencies()
    {
        // Arrange
        var cacheKey = "dependent-key";
        var dependencyKey = "dependency-key";
        var options = new CacheOptions
        {
            Dependencies = new List<string> { dependencyKey },
            TimeToLive = TimeSpan.FromMinutes(5)
        };

        // Act
        await _cache.SetAsync(cacheKey, _testResult, options);
        await _cache.InvalidateAsync(dependencyKey);
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().BeNull(); // Should be invalidated due to dependency
    }

    #endregion

    #region InvalidateAsync Tests

    [Fact]
    public async Task InvalidateAsync_WithExistingKey_ShouldRemoveEntry()
    {
        // Arrange
        var cacheKey = "invalidate-key";
        await _cache.SetAsync(cacheKey, _testResult, _defaultCacheOptions);

        // Act
        await _cache.InvalidateAsync(cacheKey);
        var result = await _cache.GetAsync(cacheKey);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateAsync_WithNonExistentKey_ShouldNotThrow()
    {
        // Act & Assert
        var act = async () => await _cache.InvalidateAsync("non-existent-key");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvalidateAsync_WithDependentEntries_ShouldInvalidateAllDependents()
    {
        // Arrange
        var dependencyKey = "dependency";
        var dependent1Key = "dependent1";
        var dependent2Key = "dependent2";

        var optionsWithDep = new CacheOptions
        {
            Dependencies = new List<string> { dependencyKey },
            TimeToLive = TimeSpan.FromMinutes(5)
        };

        await _cache.SetAsync(dependent1Key, _testResult, optionsWithDep);
        await _cache.SetAsync(dependent2Key, _testResult, optionsWithDep);

        // Act
        await _cache.InvalidateAsync(dependencyKey);

        // Assert
        var result1 = await _cache.GetAsync(dependent1Key);
        var result2 = await _cache.GetAsync(dependent2Key);

        result1.Should().BeNull();
        result2.Should().BeNull();
    }

    #endregion

    #region InvalidateByPatternAsync Tests

    [Fact]
    public async Task InvalidateByPatternAsync_WithMatchingPattern_ShouldInvalidateMatchingEntries()
    {
        // Arrange
        await _cache.SetAsync("tool:file-reader:params:path=/test1", _testResult, _defaultCacheOptions);
        await _cache.SetAsync("tool:file-reader:params:path=/test2", _testResult, _defaultCacheOptions);
        await _cache.SetAsync("tool:web-scraper:params:url=http://test.com", _testResult, _defaultCacheOptions);

        // Act
        var invalidatedCount = await _cache.InvalidateByPatternAsync("tool:file-reader:*");

        // Assert
        invalidatedCount.Should().Be(2);

        var result1 = await _cache.GetAsync("tool:file-reader:params:path=/test1");
        var result2 = await _cache.GetAsync("tool:file-reader:params:path=/test2");
        var result3 = await _cache.GetAsync("tool:web-scraper:params:url=http://test.com");

        result1.Should().BeNull();
        result2.Should().BeNull();
        result3.Should().NotBeNull(); // Should not be affected
    }

    [Fact]
    public async Task InvalidateByPatternAsync_WithNoMatches_ShouldReturnZero()
    {
        // Arrange
        await _cache.SetAsync("tool:test:params:data=1", _testResult, _defaultCacheOptions);

        // Act
        var invalidatedCount = await _cache.InvalidateByPatternAsync("tool:nonexistent:*");

        // Assert
        invalidatedCount.Should().Be(0);
    }

    #endregion

    #region InvalidateByToolAsync Tests

    [Fact]
    public async Task InvalidateByToolAsync_WithMatchingTool_ShouldInvalidateToolEntries()
    {
        // Arrange
        var fileReaderKey1 = "tool:file-reader:params:path=/test1";
        var fileReaderKey2 = "tool:file-reader:params:path=/test2";
        var webScraperKey = "tool:web-scraper:params:url=http://test.com";

        await _cache.SetAsync(fileReaderKey1, _testResult, _defaultCacheOptions);
        await _cache.SetAsync(fileReaderKey2, _testResult, _defaultCacheOptions);
        await _cache.SetAsync(webScraperKey, _testResult, _defaultCacheOptions);

        // Act
        var invalidatedCount = await _cache.InvalidateByToolAsync("file-reader");

        // Assert
        invalidatedCount.Should().Be(2);

        var result1 = await _cache.GetAsync(fileReaderKey1);
        var result2 = await _cache.GetAsync(fileReaderKey2);
        var result3 = await _cache.GetAsync(webScraperKey);

        result1.Should().BeNull();
        result2.Should().BeNull();
        result3.Should().NotBeNull(); // Different tool, should remain
    }

    #endregion

    #region ClearAsync Tests

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllEntries()
    {
        // Arrange
        await _cache.SetAsync("key1", _testResult, _defaultCacheOptions);
        await _cache.SetAsync("key2", _testResult, _defaultCacheOptions);
        await _cache.SetAsync("key3", _testResult, _defaultCacheOptions);

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

    #region GetStatisticsAsync Tests

    [Fact]
    public async Task GetStatisticsAsync_WithCacheActivity_ShouldReturnAccurateStatistics()
    {
        // Arrange
        await _cache.SetAsync("key1", _testResult, _defaultCacheOptions);
        await _cache.SetAsync("key2", _testResult, _defaultCacheOptions);

        await _cache.GetAsync("key1"); // Hit
        await _cache.GetAsync("key1"); // Hit
        await _cache.GetAsync("nonexistent"); // Miss

        // Act
        var stats = await _cache.GetStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalEntries.Should().Be(2);
        stats.HitCount.Should().Be(2);
        stats.MissCount.Should().Be(1);
        stats.TotalSizeBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithMultipleTools_ShouldGroupByTool()
    {
        // Arrange
        await _cache.SetAsync("tool:file-reader:params:data=1", _testResult, _defaultCacheOptions);
        await _cache.SetAsync("tool:file-reader:params:data=2", _testResult, _defaultCacheOptions);
        await _cache.SetAsync("tool:web-scraper:params:url=test", _testResult, _defaultCacheOptions);

        // Act
        var stats = await _cache.GetStatisticsAsync();

        // Assert
        stats.ToolStatistics.Should().HaveCount(2);
        stats.ToolStatistics.Should().ContainKey("file-reader");
        stats.ToolStatistics.Should().ContainKey("web-scraper");
        stats.ToolStatistics["file-reader"].EntryCount.Should().Be(2);
        stats.ToolStatistics["web-scraper"].EntryCount.Should().Be(1);
    }

    #endregion

    #region GenerateCacheKey Tests

    [Fact]
    public void GenerateCacheKey_WithBasicParameters_ShouldGenerateConsistentKey()
    {
        // Arrange
        var toolId = "test-tool";
        var parameters = new Dictionary<string, object?>
        {
            ["param1"] = "value1",
            ["param2"] = 42
        };

        // Act
        var key1 = _cache.GenerateCacheKey(toolId, parameters);
        var key2 = _cache.GenerateCacheKey(toolId, parameters);

        // Assert
        key1.Should().Be(key2);
        key1.Should().StartWith("tool:test-tool");
        key1.Should().Contain("params:");
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentParameterOrder_ShouldGenerateSameKey()
    {
        // Arrange
        var toolId = "test-tool";
        var parameters1 = new Dictionary<string, object?>
        {
            ["param1"] = "value1",
            ["param2"] = 42
        };
        var parameters2 = new Dictionary<string, object?>
        {
            ["param2"] = 42,
            ["param1"] = "value1"
        };

        // Act
        var key1 = _cache.GenerateCacheKey(toolId, parameters1);
        var key2 = _cache.GenerateCacheKey(toolId, parameters2);

        // Assert
        key1.Should().Be(key2);
    }

    [Fact]
    public void GenerateCacheKey_WithContext_ShouldIncludeContextInKey()
    {
        // Arrange
        var toolId = "test-tool";
        var parameters = new Dictionary<string, object?> { ["param"] = "value" };
        var context = new CacheKeyContext
        {
            UserId = "user123",
            Environment = "production",
            Version = "1.0"
        };

        // Act
        var keyWithContext = _cache.GenerateCacheKey(toolId, parameters, context);
        var keyWithoutContext = _cache.GenerateCacheKey(toolId, parameters);

        // Assert
        keyWithContext.Should().NotBe(keyWithoutContext);
        keyWithContext.Should().Contain("user:user123");
        keyWithContext.Should().Contain("env:production");
        keyWithContext.Should().Contain("v:1.0");
    }

    [Fact]
    public void GenerateCacheKey_WithLongParameters_ShouldHashParameters()
    {
        // Arrange
        var toolId = "test-tool";
        var longValue = new string('a', 300); // Long parameter value
        var parameters = new Dictionary<string, object?>
        {
            ["longParam"] = longValue
        };

        // Act
        var key = _cache.GenerateCacheKey(toolId, parameters);

        // Assert
        key.Should().StartWith("tool:test-tool");
        key.Length.Should().BeLessThan(500); // Should be hashed and shorter
    }

    [Fact]
    public void GenerateCacheKey_WithExcludedParameters_ShouldExcludeSpecifiedParams()
    {
        // Arrange
        var toolId = "test-tool";
        var parameters = new Dictionary<string, object?>
        {
            ["includedParam"] = "include",
            ["excludedParam"] = "exclude"
        };
        var context = new CacheKeyContext
        {
            ExcludedParameters = new HashSet<string> { "excludedParam" }
        };

        // Act
        var key = _cache.GenerateCacheKey(toolId, parameters, context);

        // Assert
        key.Should().Contain("includedParam=include");
        key.Should().NotContain("excludedParam");
    }

    [Fact]
    public void GenerateCacheKey_WithComplexObjects_ShouldSerializeCorrectly()
    {
        // Arrange
        var toolId = "test-tool";
        var complexObject = new { Name = "Test", Values = new[] { 1, 2, 3 } };
        var parameters = new Dictionary<string, object?>
        {
            ["simpleParam"] = "value",
            ["complexParam"] = complexObject
        };

        // Act
        var key = _cache.GenerateCacheKey(toolId, parameters);

        // Assert
        key.Should().StartWith("tool:test-tool");
        key.Should().Contain("params:");
        // Should not throw during serialization
    }

    #endregion

    #region Cache Eviction Tests

    [Fact]
    public async Task CacheEviction_ShouldUpdateStatistics()
    {
        // Arrange
        var shortLivedOptions = new CacheOptions
        {
            TimeToLive = TimeSpan.FromMilliseconds(10),
            Priority = CachePriority.Low
        };

        await _cache.SetAsync("eviction-test", _testResult, shortLivedOptions);

        // Wait for eviction
        await Task.Delay(100);

        // Force garbage collection to trigger eviction
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Act
        var stats = await _cache.GetStatisticsAsync();

        // Assert
        stats.EvictionCount.Should().BeGreaterThan(0);
    }

    #endregion
}
