using Andy.Tools.Advanced.CachingSystem;
using Andy.Tools.Advanced.Configuration;
using Andy.Tools.Advanced.MetricsCollection;
using Andy.Tools.Advanced.ToolChains;
using Andy.Tools.Core;
using Andy.Tools.Core.OutputLimiting;
using Andy.Tools.Execution;
using Andy.Tools.Library.FileSystem;
using Andy.Tools.Validation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Andy.Tools.Tests.Advanced.Configuration;

public class ServiceCollectionExtensionsTests : IDisposable
{
    private readonly ServiceCollection _services;
    private readonly Mock<IToolRegistry> _registryMock;
    private readonly Mock<IToolValidator> _validatorMock;
    private readonly Mock<ISecurityManager> _securityManagerMock;
    private readonly Mock<IResourceMonitor> _resourceMonitorMock;
    private readonly Mock<IToolOutputLimiter> _outputLimiterMock;

    public ServiceCollectionExtensionsTests()
    {
        _services = new ServiceCollection();
        _registryMock = new Mock<IToolRegistry>();
        _validatorMock = new Mock<IToolValidator>();
        _securityManagerMock = new Mock<ISecurityManager>();
        _resourceMonitorMock = new Mock<IResourceMonitor>();
        _outputLimiterMock = new Mock<IToolOutputLimiter>();

        // Add required dependencies
        _services.AddLogging();
        _services.AddSingleton(_registryMock.Object);
        _services.AddSingleton(_validatorMock.Object);
        _services.AddSingleton(_securityManagerMock.Object);
        _services.AddSingleton(_resourceMonitorMock.Object);
        _services.AddSingleton(_outputLimiterMock.Object);
    }

    public void Dispose()
    {
        _services.Clear();
    }

    [Fact]
    public void AddAdvancedToolFeatures_ShouldRegisterRequiredServices()
    {
        // Act
        _services.AddAdvancedToolFeatures();
        var provider = _services.BuildServiceProvider();

        // Assert
        provider.GetService<IToolExecutionCache>().Should().NotBeNull();
        provider.GetService<IToolMetricsCollector>().Should().NotBeNull();
        provider.GetService<AdvancedToolOptions>().Should().NotBeNull();
        provider.GetService<ToolChainBuilder>().Should().NotBeNull();
        provider.GetService<IToolChain>().Should().NotBeNull();
    }

    [Fact]
    public void AddAdvancedToolFeatures_ShouldConfigureOptions()
    {
        // Arrange
        var cacheTimeToLive = TimeSpan.FromMinutes(10);
        var maxCacheSize = 200L * 1024 * 1024;

        // Act
        _services.AddAdvancedToolFeatures(options =>
        {
            options.CacheTimeToLive = cacheTimeToLive;
            options.MaxCacheSizeBytes = maxCacheSize;
            options.EnableMetrics = false;
        });
        var provider = _services.BuildServiceProvider();

        // Assert
        var cacheOptions = provider.GetRequiredService<IOptions<ToolCacheOptions>>().Value;
        cacheOptions.DefaultTimeToLive.Should().Be(cacheTimeToLive);
        cacheOptions.MaxSizeBytes.Should().Be(maxCacheSize);
        cacheOptions.EnableStatistics.Should().BeFalse();
    }

    [Fact]
    public void AddAdvancedToolFeatures_ShouldRegisterCachingExecutor_WhenCachingEnabled()
    {
        // Arrange
        _services.AddSingleton<IToolExecutor, ToolExecutor>();

        // Act
        _services.AddAdvancedToolFeatures(options =>
        {
            options.EnableCaching = true;
        });
        var provider = _services.BuildServiceProvider();

        // Assert
        var executor = provider.GetRequiredService<IToolExecutor>();
        executor.Should().BeOfType<CachingToolExecutor>();
    }

    [Fact]
    public void AddAdvancedToolFeatures_ShouldNotRegisterCachingExecutor_WhenCachingDisabled()
    {
        // Arrange
        _services.AddSingleton<IToolExecutor, ToolExecutor>();

        // Act
        _services.AddAdvancedToolFeatures(options =>
        {
            options.EnableCaching = false;
        });
        var provider = _services.BuildServiceProvider();

        // Assert
        var executor = provider.GetRequiredService<IToolExecutor>();
        executor.Should().BeOfType<ToolExecutor>();
    }

    [Fact]
    public void AddAdvancedToolFeatures_ShouldConfigureMetricsOptions()
    {
        // Arrange
        var maxMetrics = 1000;
        var retentionPeriod = TimeSpan.FromDays(30);

        // Act
        _services.AddAdvancedToolFeatures(options =>
        {
            options.MaxMetricsPerTool = maxMetrics;
            options.MetricsRetentionPeriod = retentionPeriod;
            options.EnableDetailedMetrics = true;
        });
        var provider = _services.BuildServiceProvider();

        // Assert
        var metricsOptions = provider.GetRequiredService<IOptions<ToolMetricsOptions>>().Value;
        metricsOptions.MaxMetricsPerTool.Should().Be(maxMetrics);
        metricsOptions.MetricsRetentionPeriod.Should().Be(retentionPeriod);
        metricsOptions.EnableDetailedTracking.Should().BeTrue();
    }

    [Fact]
    public void AddAdvancedToolFeatures_ShouldBeIdempotent()
    {
        // Act
        _services.AddAdvancedToolFeatures();
        _services.AddAdvancedToolFeatures(); // Add again
        var provider = _services.BuildServiceProvider();

        // Assert
        var caches = provider.GetServices<IToolExecutionCache>();
        caches.Should().HaveCount(1);
    }

    [Fact]
    public void AddToolChains_ShouldRegisterToolChainBuilder()
    {
        // Act
        _services.AddToolChains();
        var provider = _services.BuildServiceProvider();

        // Assert
        provider.GetService<ToolChainBuilder>().Should().NotBeNull();
    }

    [Fact]
    public void AddToolChain_ShouldRegisterPrebuiltChain()
    {
        // Arrange
        var chainId = "test-chain";
        var chainConfigured = false;

        // Act
        _services.AddToolChains();
        _services.AddToolChain(chainId, chain =>
        {
            chainConfigured = true;
        });
        var provider = _services.BuildServiceProvider();

        // Assert
        var chain = provider.GetRequiredService<IToolChain>();
        chain.Should().NotBeNull();
        chainConfigured.Should().BeTrue();
    }

    [Fact]
    public void AddAdvancedToolFeatures_ShouldReplaceExistingExecutor_WhenCachingEnabled()
    {
        // Arrange
        var mockExecutor = new Mock<IToolExecutor>();
        _services.AddSingleton(mockExecutor.Object);

        // Act
        _services.AddAdvancedToolFeatures(options =>
        {
            options.EnableCaching = true;
        });
        var provider = _services.BuildServiceProvider();

        // Assert
        var executor = provider.GetRequiredService<IToolExecutor>();
        executor.Should().BeOfType<CachingToolExecutor>();
        executor.Should().NotBeSameAs(mockExecutor.Object);
    }

    [Fact]
    public void AddAdvancedToolFeatures_ShouldHandleDefaultConfiguration()
    {
        // Act
        _services.AddAdvancedToolFeatures(); // No configuration
        var provider = _services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<AdvancedToolOptions>();
        options.Should().NotBeNull();
        options.EnableCaching.Should().BeTrue(); // Default value
        options.EnableMetrics.Should().BeTrue(); // Default value
    }

    [Fact]
    public void AddAdvancedToolFeatures_ShouldRegisterServicesAsSingleton()
    {
        // Act
        _services.AddAdvancedToolFeatures();
        var provider = _services.BuildServiceProvider();

        // Assert - Get same instance multiple times
        var cache1 = provider.GetRequiredService<IToolExecutionCache>();
        var cache2 = provider.GetRequiredService<IToolExecutionCache>();
        cache1.Should().BeSameAs(cache2);

        var metrics1 = provider.GetRequiredService<IToolMetricsCollector>();
        var metrics2 = provider.GetRequiredService<IToolMetricsCollector>();
        metrics1.Should().BeSameAs(metrics2);
    }

    [Fact]
    public void AddToolChain_ShouldCreateChainWithCorrectId()
    {
        // Arrange
        var expectedId = "my-tool-chain";
        string? actualId = null;

        // Act
        _services.AddToolChains();
        _services.AddToolChain(expectedId, chain =>
        {
            actualId = chain.Id;
        });
        var provider = _services.BuildServiceProvider();

        // Assert
        var chain = provider.GetRequiredService<IToolChain>();
        actualId.Should().Be(expectedId);
    }

    [Fact]
    public void AddAdvancedToolFeatures_ShouldIntegrateWithRealServices()
    {
        // Arrange - Use real implementations
        var services = new ServiceCollection();
        services.AddLogging();
        
        // Add minimal required services
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>());
        services.AddSingleton(registry.Object);
        services.AddSingleton(new Mock<IToolValidator>().Object);
        services.AddSingleton(new Mock<ISecurityManager>().Object);
        services.AddSingleton(new Mock<IResourceMonitor>().Object);
        services.AddSingleton(new Mock<IToolOutputLimiter>().Object);

        // Act
        services.AddAdvancedToolFeatures(options =>
        {
            options.EnableCaching = true;
            options.CacheTimeToLive = TimeSpan.FromMinutes(30);
            options.MaxCacheSizeBytes = 100 * 1024 * 1024;
            options.EnableMetrics = true;
            options.MaxMetricsPerTool = 500;
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var executor = provider.GetRequiredService<IToolExecutor>();
        executor.Should().BeOfType<CachingToolExecutor>();
        
        var cache = provider.GetRequiredService<IToolExecutionCache>();
        cache.Should().BeOfType<MemoryToolExecutionCache>();
        
        var metrics = provider.GetRequiredService<IToolMetricsCollector>();
        metrics.Should().BeOfType<InMemoryToolMetricsCollector>();
    }
}