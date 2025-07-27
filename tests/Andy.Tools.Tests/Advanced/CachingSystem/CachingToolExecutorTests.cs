using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Andy.Tools.Advanced.CachingSystem;
using Andy.Tools.Core;
using Andy.Tools.Execution;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Andy.Tools.Tests.Advanced.CachingSystem;

public class CachingToolExecutorTests
{
    private readonly Mock<IToolExecutor> _mockInnerExecutor;
    private readonly Mock<IToolExecutionCache> _mockCache;
    private readonly Mock<ILogger<CachingToolExecutor>> _mockLogger;
    private readonly CachingToolExecutor _cachingExecutor;

    public CachingToolExecutorTests()
    {
        _mockInnerExecutor = new Mock<IToolExecutor>();
        _mockCache = new Mock<IToolExecutionCache>();
        _mockLogger = new Mock<ILogger<CachingToolExecutor>>();
        _cachingExecutor = new CachingToolExecutor(_mockInnerExecutor.Object, _mockCache.Object, _mockLogger.Object);
    }

    #region Basic Functionality Tests

    [Fact]
    public async Task ExecuteAsync_WithCachingDisabled_ShouldBypassCache()
    {
        // Arrange
        var request = CreateRequest("test-tool", new Dictionary<string, object?> { ["param1"] = "value1" });
        request.Context.AdditionalData = new Dictionary<string, object?> { ["EnableCaching"] = false };

        var expectedResult = CreateSuccessResult("test-tool", "test data");
        _mockInnerExecutor.Setup(x => x.ExecuteAsync(request)).ReturnsAsync(expectedResult);

        // Act
        var result = await _cachingExecutor.ExecuteAsync(request);

        // Assert
        result.Should().BeEquivalentTo(expectedResult);
        _mockCache.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockCache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ToolResult>(), It.IsAny<CacheOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithCachingEnabled_FirstCall_ShouldExecuteTool()
    {
        // Arrange
        var request = CreateRequest("test-tool", new Dictionary<string, object?> { ["param1"] = "value1" });
        request.Context.AdditionalData = new Dictionary<string, object?> { ["EnableCaching"] = true };

        var expectedResult = CreateSuccessResult("test-tool", "test data");
        _mockInnerExecutor.Setup(x => x.ExecuteAsync(request)).ReturnsAsync(expectedResult);
        _mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CachedToolResult?)null);
        _mockCache.Setup(x => x.GenerateCacheKey(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CacheKeyContext>())).Returns("cache-key");

        // Act
        var result = await _cachingExecutor.ExecuteAsync(request);

        // Assert
        result.Should().BeEquivalentTo(expectedResult);
        _mockInnerExecutor.Verify(x => x.ExecuteAsync(request), Times.Once);
        _mockCache.Verify(x => x.SetAsync("cache-key", It.IsAny<ToolResult>(), It.IsAny<CacheOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithCachingEnabled_SecondCall_ShouldReturnCachedResult()
    {
        // Arrange
        var request = CreateRequest("test-tool", new Dictionary<string, object?> { ["param1"] = "value1" });
        request.Context.AdditionalData = new Dictionary<string, object?> { ["EnableCaching"] = true };

        var cachedResult = new CachedToolResult
        {
            Result = new ToolResult
            {
                IsSuccessful = true,
                Data = new Dictionary<string, object?> { ["cached"] = "data" }
            },
            CachedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            HitCount = 1
        };

        _mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(cachedResult);
        _mockCache.Setup(x => x.GenerateCacheKey(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CacheKeyContext>())).Returns("cache-key");

        // Act
        var result = await _cachingExecutor.ExecuteAsync(request);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(cachedResult.Result.Data);
        result.Metadata.Should().ContainKey("cache_hit").WhoseValue.Should().Be(true);
        result.Metadata.Should().ContainKey("cached_at").WhoseValue.Should().Be(cachedResult.CachedAt);
        result.Metadata.Should().ContainKey("hit_count").WhoseValue.Should().Be(1L);
        _mockInnerExecutor.Verify(x => x.ExecuteAsync(It.IsAny<ToolExecutionRequest>()), Times.Never);
    }

    #endregion

    #region Cache Key Generation Tests

    [Fact]
    public async Task ExecuteAsync_DifferentParameters_ShouldGenerateDifferentCacheKeys()
    {
        // Arrange
        var request1 = CreateRequest("test-tool", new Dictionary<string, object?> { ["param1"] = "value1" });
        request1.Context.AdditionalData = new Dictionary<string, object?> { ["EnableCaching"] = true };

        var request2 = CreateRequest("test-tool", new Dictionary<string, object?> { ["param1"] = "value2" });
        request2.Context.AdditionalData = new Dictionary<string, object?> { ["EnableCaching"] = true };

        _mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CachedToolResult?)null);
        _mockCache.Setup(x => x.GenerateCacheKey("test-tool", It.Is<Dictionary<string, object?>>(d => d["param1"]!.Equals("value1")), It.IsAny<CacheKeyContext>())).Returns("cache-key-1");
        _mockCache.Setup(x => x.GenerateCacheKey("test-tool", It.Is<Dictionary<string, object?>>(d => d["param1"]!.Equals("value2")), It.IsAny<CacheKeyContext>())).Returns("cache-key-2");

        var result = CreateSuccessResult("test-tool", "data");
        _mockInnerExecutor.Setup(x => x.ExecuteAsync(It.IsAny<ToolExecutionRequest>())).ReturnsAsync(result);

        // Act
        await _cachingExecutor.ExecuteAsync(request1);
        await _cachingExecutor.ExecuteAsync(request2);

        // Assert
        _mockCache.Verify(x => x.SetAsync("cache-key-1", It.IsAny<ToolResult>(), It.IsAny<CacheOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(x => x.SetAsync("cache-key-2", It.IsAny<ToolResult>(), It.IsAny<CacheOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithUserContext_ShouldIncludeInCacheKey()
    {
        // Arrange
        var request = CreateRequest("test-tool", new Dictionary<string, object?> { ["param1"] = "value1" });
        request.Context.UserId = "user123";
        request.Context.AdditionalData = new Dictionary<string, object?> { ["EnableCaching"] = true };

        CacheKeyContext? capturedContext = null;
        _mockCache.Setup(x => x.GenerateCacheKey(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CacheKeyContext>()))
            .Callback<string, Dictionary<string, object?>, CacheKeyContext>((_, _, ctx) => capturedContext = ctx)
            .Returns("cache-key");

        _mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CachedToolResult?)null);
        _mockInnerExecutor.Setup(x => x.ExecuteAsync(It.IsAny<ToolExecutionRequest>())).ReturnsAsync(CreateSuccessResult("test-tool", "data"));

        // Act
        await _cachingExecutor.ExecuteAsync(request);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.UserId.Should().Be("user123");
    }

    #endregion

    #region Cache Options Tests

    [Fact]
    public async Task ExecuteAsync_WithTTL_ShouldRespectTimeToLive()
    {
        // Arrange
        var request = CreateRequest("test-tool", new Dictionary<string, object?> { ["param1"] = "value1" });
        request.Context.AdditionalData = new Dictionary<string, object?>
        {
            ["EnableCaching"] = true,
            ["CacheTimeToLive"] = TimeSpan.FromMinutes(30)
        };

        CacheOptions? capturedOptions = null;
        _mockCache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ToolResult>(), It.IsAny<CacheOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, ToolResult, CacheOptions, CancellationToken>((_, _, options, _) => capturedOptions = options)
            .Returns(Task.CompletedTask);

        _mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CachedToolResult?)null);
        _mockCache.Setup(x => x.GenerateCacheKey(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CacheKeyContext>())).Returns("cache-key");
        _mockInnerExecutor.Setup(x => x.ExecuteAsync(It.IsAny<ToolExecutionRequest>())).ReturnsAsync(CreateSuccessResult("test-tool", "data"));

        // Act
        await _cachingExecutor.ExecuteAsync(request);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.TimeToLive.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task ExecuteAsync_WithCachePriority_ShouldSetPriority()
    {
        // Arrange
        var request = CreateRequest("test-tool", new Dictionary<string, object?> { ["param1"] = "value1" });
        request.Context.AdditionalData = new Dictionary<string, object?>
        {
            ["EnableCaching"] = true,
            ["CachePriority"] = "High"
        };

        CacheOptions? capturedOptions = null;
        _mockCache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ToolResult>(), It.IsAny<CacheOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, ToolResult, CacheOptions, CancellationToken>((_, _, options, _) => capturedOptions = options)
            .Returns(Task.CompletedTask);

        _mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CachedToolResult?)null);
        _mockCache.Setup(x => x.GenerateCacheKey(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CacheKeyContext>())).Returns("cache-key");
        _mockInnerExecutor.Setup(x => x.ExecuteAsync(It.IsAny<ToolExecutionRequest>())).ReturnsAsync(CreateSuccessResult("test-tool", "data"));

        // Act
        await _cachingExecutor.ExecuteAsync(request);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Priority.Should().Be(CachePriority.High);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_FailedExecution_WithCacheFailuresDisabled_ShouldNotCache()
    {
        // Arrange
        var request = CreateRequest("test-tool", new Dictionary<string, object?> { ["param1"] = "value1" });
        request.Context.AdditionalData = new Dictionary<string, object?> { ["EnableCaching"] = true };

        var failedResult = CreateFailureResult("test-tool", "Operation failed");
        _mockInnerExecutor.Setup(x => x.ExecuteAsync(request)).ReturnsAsync(failedResult);
        _mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CachedToolResult?)null);
        _mockCache.Setup(x => x.GenerateCacheKey(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CacheKeyContext>())).Returns("cache-key");

        // Act
        var result = await _cachingExecutor.ExecuteAsync(request);

        // Assert
        result.IsSuccessful.Should().BeFalse();
        _mockCache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ToolResult>(), It.IsAny<CacheOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_FailedExecution_WithCacheFailuresEnabled_ShouldCache()
    {
        // Arrange
        var request = CreateRequest("test-tool", new Dictionary<string, object?> { ["param1"] = "value1" });
        request.Context.AdditionalData = new Dictionary<string, object?>
        {
            ["EnableCaching"] = true,
            ["CacheFailures"] = true
        };

        var failedResult = CreateFailureResult("test-tool", "Operation failed");
        _mockInnerExecutor.Setup(x => x.ExecuteAsync(request)).ReturnsAsync(failedResult);
        _mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CachedToolResult?)null);
        _mockCache.Setup(x => x.GenerateCacheKey(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CacheKeyContext>())).Returns("cache-key");

        // Act
        var result = await _cachingExecutor.ExecuteAsync(request);

        // Assert
        result.IsSuccessful.Should().BeFalse();
        _mockCache.Verify(x => x.SetAsync("cache-key", It.IsAny<ToolResult>(), It.IsAny<CacheOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CacheError_ShouldContinueWithExecution()
    {
        // Arrange
        var request = CreateRequest("test-tool", new Dictionary<string, object?> { ["param1"] = "value1" });
        request.Context.AdditionalData = new Dictionary<string, object?> { ["EnableCaching"] = true };

        // First GetAsync call throws, but GenerateCacheKey should work
        _mockCache.Setup(x => x.GenerateCacheKey(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CacheKeyContext>())).Returns("cache-key");
        _mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CachedToolResult?)null);

        var expectedResult = CreateSuccessResult("test-tool", "test data");
        _mockInnerExecutor.Setup(x => x.ExecuteAsync(request)).ReturnsAsync(expectedResult);

        // Act
        var result = await _cachingExecutor.ExecuteAsync(request);

        // Assert
        result.Should().BeEquivalentTo(expectedResult);
        _mockInnerExecutor.Verify(x => x.ExecuteAsync(request), Times.Once);
    }

    #endregion

    #region Event Forwarding Tests

    [Fact]
    public void Constructor_ShouldForwardExecutionStartedEvent()
    {
        // Arrange
        var eventRaised = false;
        _cachingExecutor.ExecutionStarted += (sender, args) => eventRaised = true;

        // Act
        _mockInnerExecutor.Raise(x => x.ExecutionStarted += null, new ToolExecutionStartedEventArgs("test", "correlation-123", new ToolExecutionContext()));

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldForwardExecutionCompletedEvent()
    {
        // Arrange
        var eventRaised = false;
        _cachingExecutor.ExecutionCompleted += (sender, args) => eventRaised = true;

        // Act
        _mockInnerExecutor.Raise(x => x.ExecutionCompleted += null, new ToolExecutionCompletedEventArgs(CreateSuccessResult("test", "data")));

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldForwardSecurityViolationEvent()
    {
        // Arrange
        var eventRaised = false;
        _cachingExecutor.SecurityViolation += (sender, args) => eventRaised = true;

        // Act
        _mockInnerExecutor.Raise(x => x.SecurityViolation += null, new SecurityViolationEventArgs("test", "correlation-123", "test violation", SecurityViolationSeverity.High));

        // Assert
        eventRaised.Should().BeTrue();
    }

    #endregion

    #region Other Interface Methods Tests

    [Fact]
    public async Task ValidateExecutionRequestAsync_ShouldDelegateToInnerExecutor()
    {
        // Arrange
        var request = CreateRequest("test-tool", new Dictionary<string, object?> { ["param1"] = "value1" });
        var expectedErrors = new List<string> { "Error 1", "Error 2" };
        _mockInnerExecutor.Setup(x => x.ValidateExecutionRequestAsync(request)).ReturnsAsync(expectedErrors);

        // Act
        var result = await _cachingExecutor.ValidateExecutionRequestAsync(request);

        // Assert
        result.Should().BeEquivalentTo(expectedErrors);
    }

    [Fact]
    public async Task EstimateResourceUsageAsync_ShouldDelegateToInnerExecutor()
    {
        // Arrange
        var toolId = "test-tool";
        var parameters = new Dictionary<string, object?> { ["param1"] = "value1" };
        var expectedUsage = new ToolResourceUsage { PeakMemoryBytes = 1024, CpuTimeMs = 100 };
        _mockInnerExecutor.Setup(x => x.EstimateResourceUsageAsync(toolId, parameters)).ReturnsAsync(expectedUsage);

        // Act
        var result = await _cachingExecutor.EstimateResourceUsageAsync(toolId, parameters);

        // Assert
        result.Should().BeEquivalentTo(expectedUsage);
    }

    [Fact]
    public void GetStatistics_ShouldDelegateToInnerExecutor()
    {
        // Arrange
        var expectedStats = new ToolExecutionStatistics();
        _mockInnerExecutor.Setup(x => x.GetStatistics()).Returns(expectedStats);

        // Act
        var result = _cachingExecutor.GetStatistics();

        // Assert
        result.Should().BeSameAs(expectedStats);
    }

    [Fact]
    public void Dispose_ShouldDisposeInnerExecutorIfDisposable()
    {
        // Arrange
        var mockDisposableExecutor = new Mock<IToolExecutor>();
        mockDisposableExecutor.As<IDisposable>();
        var cachingExecutor = new CachingToolExecutor(mockDisposableExecutor.Object, _mockCache.Object, _mockLogger.Object);

        // Act
        cachingExecutor.Dispose();

        // Assert
        mockDisposableExecutor.As<IDisposable>().Verify(x => x.Dispose(), Times.Once);
    }

    #endregion

    #region Helper Methods

    private static ToolExecutionRequest CreateRequest(string toolId, Dictionary<string, object?> parameters)
    {
        return new ToolExecutionRequest
        {
            ToolId = toolId,
            Parameters = parameters,
            Context = new ToolExecutionContext
            {
                CorrelationId = Guid.NewGuid().ToString("N")[..8]
            }
        };
    }

    private static ToolExecutionResult CreateSuccessResult(string toolId, object data)
    {
        return new ToolExecutionResult
        {
            ToolId = toolId,
            IsSuccessful = true,
            Data = new Dictionary<string, object?> { ["result"] = data },
            StartTime = DateTimeOffset.UtcNow.AddSeconds(-1),
            EndTime = DateTimeOffset.UtcNow,
            DurationMs = 1000
        };
    }

    private static ToolExecutionResult CreateFailureResult(string toolId, string errorMessage)
    {
        return new ToolExecutionResult
        {
            ToolId = toolId,
            IsSuccessful = false,
            ErrorMessage = errorMessage,
            StartTime = DateTimeOffset.UtcNow.AddSeconds(-1),
            EndTime = DateTimeOffset.UtcNow,
            DurationMs = 1000
        };
    }

    #endregion
}