using Andy.Tools.Core;
using Andy.Tools.Discovery;
using Andy.Tools.Execution;
using Andy.Tools.Framework;
using Andy.Tools.Library;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Andy.Tools.Tests.Framework;

public class ToolLifecycleManagerTests : IDisposable
{
    private readonly Mock<IToolRegistry> _registryMock;
    private readonly Mock<IToolDiscovery> _discoveryMock;
    private readonly Mock<ISecurityManager> _securityManagerMock;
    private readonly Mock<IToolExecutor> _executorMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<ToolLifecycleManager>> _loggerMock;
    private readonly ToolFrameworkOptions _options;
    private readonly List<ToolRegistrationInfo> _registrationInfos;
    private readonly ToolLifecycleManager _lifecycleManager;

    public ToolLifecycleManagerTests()
    {
        _registryMock = new Mock<IToolRegistry>();
        _discoveryMock = new Mock<IToolDiscovery>();
        _securityManagerMock = new Mock<ISecurityManager>();
        _executorMock = new Mock<IToolExecutor>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<ToolLifecycleManager>>();
        _options = new ToolFrameworkOptions();
        _registrationInfos = new List<ToolRegistrationInfo>();

        _registryMock.Setup(r => r.Tools).Returns(new List<ToolRegistration>());
        _executorMock.Setup(e => e.GetRunningExecutions()).Returns(new List<RunningExecutionInfo>());
        _executorMock.Setup(e => e.GetStatistics()).Returns(new ToolExecutionStatistics());

        _lifecycleManager = new ToolLifecycleManager(
            _options,
            _registryMock.Object,
            _discoveryMock.Object,
            _securityManagerMock.Object,
            _executorMock.Object,
            _serviceProviderMock.Object,
            _registrationInfos,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        // Cleanup
    }

    #region InitializeAsync Tests

    [Fact]
    public async Task InitializeAsync_ShouldRegisterExplicitTools()
    {
        // Arrange
        var toolType = typeof(SampleTool);
        var config = new Dictionary<string, object?> { ["key"] = "value" };
        _registrationInfos.Add(new ToolRegistrationInfo
        {
            ToolType = toolType,
            Configuration = config
        });

        _options.AutoDiscoverTools = false;

        // Act
        await _lifecycleManager.InitializeAsync();

        // Assert
        _registryMock.Verify(r => r.RegisterTool(toolType, config), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_ShouldDiscoverTools_WhenAutoDiscoverEnabled()
    {
        // Arrange
        _options.AutoDiscoverTools = true;
        var discoveredTools = new List<DiscoveredTool>
        {
            new() { ToolType = typeof(SampleTool), IsValid = true }
        };
        _discoveryMock.Setup(d => d.DiscoverToolsAsync(_options.DiscoveryOptions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredTools);

        // Act
        await _lifecycleManager.InitializeAsync();

        // Assert
        _discoveryMock.Verify(d => d.DiscoverToolsAsync(_options.DiscoveryOptions, It.IsAny<CancellationToken>()), Times.Once);
        _registryMock.Verify(r => r.RegisterTool(typeof(SampleTool), null), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_ShouldNotDiscoverTools_WhenAutoDiscoverDisabled()
    {
        // Arrange
        _options.AutoDiscoverTools = false;

        // Act
        await _lifecycleManager.InitializeAsync();

        // Assert
        _discoveryMock.Verify(d => d.DiscoverToolsAsync(It.IsAny<ToolDiscoveryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_ShouldUpdateStatus_WhenSuccessful()
    {
        // Arrange
        var registrations = new List<ToolRegistration>
        {
            new() { ToolType = typeof(SampleTool) },
            new() { ToolType = typeof(AnotherSampleTool) }
        };
        _registryMock.Setup(r => r.Tools).Returns(registrations);

        // Act
        await _lifecycleManager.InitializeAsync();
        var status = _lifecycleManager.GetStatus();

        // Assert
        status.IsInitialized.Should().BeTrue();
        status.InitializedAt.Should().NotBeNull();
        status.RegisteredToolsCount.Should().Be(2);
        status.StartupErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_ShouldLogError_WhenInitializationFails()
    {
        // Arrange
        var exception = new InvalidOperationException("Init failed");
        _registryMock.Setup(r => r.RegisterTool(It.IsAny<Type>(), It.IsAny<Dictionary<string, object?>>()))
            .Throws(exception);
        _registrationInfos.Add(new ToolRegistrationInfo { ToolType = typeof(SampleTool) });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _lifecycleManager.InitializeAsync());

        var status = _lifecycleManager.GetStatus();
        status.StartupErrors.Should().Contain("Init failed");
    }

    [Fact]
    public async Task InitializeAsync_ShouldContinue_WhenExplicitToolRegistrationFails()
    {
        // Arrange
        _registrationInfos.Add(new ToolRegistrationInfo { ToolType = typeof(SampleTool) });
        _registrationInfos.Add(new ToolRegistrationInfo { ToolType = typeof(AnotherSampleTool) });

        _registryMock.Setup(r => r.RegisterTool(typeof(SampleTool), It.IsAny<Dictionary<string, object?>>()))
            .Throws(new InvalidOperationException("Registration failed"));

        // Act
        await _lifecycleManager.InitializeAsync();

        // Assert
        _registryMock.Verify(r => r.RegisterTool(typeof(SampleTool), It.IsAny<Dictionary<string, object?>>()), Times.Once);
        _registryMock.Verify(r => r.RegisterTool(typeof(AnotherSampleTool), It.IsAny<Dictionary<string, object?>>()), Times.Once);
    }

    #endregion

    #region DiscoverAndRegisterToolsAsync Tests

    [Fact]
    public async Task DiscoverAndRegisterToolsAsync_ShouldRegisterValidTools()
    {
        // Arrange
        var discoveredTools = new List<DiscoveredTool>
        {
            new() { ToolType = typeof(SampleTool), IsValid = true },
            new() { ToolType = typeof(AnotherSampleTool), IsValid = true },
            new() { ToolType = typeof(InvalidTool), IsValid = false }
        };
        _discoveryMock.Setup(d => d.DiscoverToolsAsync(_options.DiscoveryOptions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredTools);

        // Act
        var count = await _lifecycleManager.DiscoverAndRegisterToolsAsync();

        // Assert
        count.Should().Be(2);
        _registryMock.Verify(r => r.RegisterTool(typeof(SampleTool), null), Times.Once);
        _registryMock.Verify(r => r.RegisterTool(typeof(AnotherSampleTool), null), Times.Once);
        _registryMock.Verify(r => r.RegisterTool(typeof(InvalidTool), null), Times.Never);
    }

    [Fact]
    public async Task DiscoverAndRegisterToolsAsync_ShouldSkipAlreadyRegisteredTools()
    {
        // Arrange
        var existingRegistration = new ToolRegistration { ToolType = typeof(SampleTool) };
        _registryMock.Setup(r => r.Tools).Returns(new List<ToolRegistration> { existingRegistration });

        var discoveredTools = new List<DiscoveredTool>
        {
            new() { ToolType = typeof(SampleTool), IsValid = true },
            new() { ToolType = typeof(AnotherSampleTool), IsValid = true }
        };
        _discoveryMock.Setup(d => d.DiscoverToolsAsync(_options.DiscoveryOptions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredTools);

        // Act
        var count = await _lifecycleManager.DiscoverAndRegisterToolsAsync();

        // Assert
        count.Should().Be(1);
        _registryMock.Verify(r => r.RegisterTool(typeof(SampleTool), null), Times.Never);
        _registryMock.Verify(r => r.RegisterTool(typeof(AnotherSampleTool), null), Times.Once);
    }

    [Fact]
    public async Task DiscoverAndRegisterToolsAsync_ShouldContinue_WhenRegistrationFails()
    {
        // Arrange
        var discoveredTools = new List<DiscoveredTool>
        {
            new() { ToolType = typeof(SampleTool), IsValid = true },
            new() { ToolType = typeof(AnotherSampleTool), IsValid = true }
        };
        _discoveryMock.Setup(d => d.DiscoverToolsAsync(_options.DiscoveryOptions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredTools);

        _registryMock.Setup(r => r.RegisterTool(typeof(SampleTool), null))
            .Throws(new InvalidOperationException("Registration failed"));

        // Act
        var count = await _lifecycleManager.DiscoverAndRegisterToolsAsync();

        // Assert
        count.Should().Be(1);
        _registryMock.Verify(r => r.RegisterTool(typeof(SampleTool), null), Times.Once);
        _registryMock.Verify(r => r.RegisterTool(typeof(AnotherSampleTool), null), Times.Once);
    }

    [Fact]
    public async Task DiscoverAndRegisterToolsAsync_ShouldReturnZero_WhenDiscoveryFails()
    {
        // Arrange
        _discoveryMock.Setup(d => d.DiscoverToolsAsync(_options.DiscoveryOptions, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Discovery failed"));

        // Act
        var count = await _lifecycleManager.DiscoverAndRegisterToolsAsync();

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region ShutdownAsync Tests

    [Fact]
    public async Task ShutdownAsync_ShouldCancelRunningExecutions()
    {
        // Arrange
        var runningExecutions = new List<RunningExecutionInfo>
        {
            new() { CorrelationId = Guid.NewGuid().ToString() },
            new() { CorrelationId = Guid.NewGuid().ToString() }
        };
        _executorMock.Setup(e => e.GetRunningExecutions()).Returns(runningExecutions);

        // Act
        await _lifecycleManager.ShutdownAsync();

        // Assert
        foreach (var execution in runningExecutions)
        {
            _executorMock.Verify(e => e.CancelExecutionsAsync(execution.CorrelationId), Times.Once);
        }
    }

    [Fact]
    public async Task ShutdownAsync_ShouldUpdateStatus()
    {
        // Arrange
        await _lifecycleManager.InitializeAsync();

        // Act
        await _lifecycleManager.ShutdownAsync();
        var status = _lifecycleManager.GetStatus();

        // Assert
        status.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public async Task ShutdownAsync_ShouldContinue_WhenCancellationFails()
    {
        // Arrange
        var runningExecutions = new List<RunningExecutionInfo>
        {
            new() { CorrelationId = "execution1" },
            new() { CorrelationId = "execution2" }
        };
        _executorMock.Setup(e => e.GetRunningExecutions()).Returns(runningExecutions);
        _executorMock.Setup(e => e.CancelExecutionsAsync("execution1"))
            .ThrowsAsync(new InvalidOperationException("Cancel failed"));

        // Act
        await _lifecycleManager.ShutdownAsync();

        // Assert
        _executorMock.Verify(e => e.CancelExecutionsAsync("execution1"), Times.Once);
        _executorMock.Verify(e => e.CancelExecutionsAsync("execution2"), Times.Once);
    }

    #endregion

    #region PerformMaintenanceAsync Tests

    [Fact]
    public async Task PerformMaintenanceAsync_ShouldClearOldSecurityViolations()
    {
        // Arrange
        _securityManagerMock.Setup(s => s.ClearOldViolations(_options.SecurityViolationMaxAge))
            .Returns(5);

        // Act
        await _lifecycleManager.PerformMaintenanceAsync();

        // Assert
        _securityManagerMock.Verify(s => s.ClearOldViolations(_options.SecurityViolationMaxAge), Times.Once);
    }

    [Fact]
    public async Task PerformMaintenanceAsync_ShouldUpdateLastMaintenanceTime()
    {
        // Arrange
        await _lifecycleManager.InitializeAsync();

        // Act
        await _lifecycleManager.PerformMaintenanceAsync();
        var status = _lifecycleManager.GetStatus();

        // Assert
        status.LastMaintenanceAt.Should().NotBeNull();
        status.LastMaintenanceAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task PerformMaintenanceAsync_ShouldNotThrow_WhenMaintenanceFails()
    {
        // Arrange
        _securityManagerMock.Setup(s => s.ClearOldViolations(It.IsAny<TimeSpan>()))
            .Throws(new InvalidOperationException("Maintenance failed"));

        // Act & Assert
        await _lifecycleManager.PerformMaintenanceAsync();
        // Should not throw
    }

    #endregion

    #region GetStatus Tests

    [Fact]
    public void GetStatus_ShouldReturnCurrentStatus()
    {
        // Arrange
        var registrations = new List<ToolRegistration>
        {
            new() { ToolType = typeof(SampleTool) },
            new() { ToolType = typeof(AnotherSampleTool) },
            new() { ToolType = typeof(InvalidTool) }
        };
        _registryMock.Setup(r => r.Tools).Returns(registrations);

        var runningExecutions = new List<RunningExecutionInfo>
        {
            new() { CorrelationId = "exec1" },
            new() { CorrelationId = "exec2" }
        };
        _executorMock.Setup(e => e.GetRunningExecutions()).Returns(runningExecutions);

        var stats = new ToolExecutionStatistics
        {
            TotalExecutions = 100,
            SuccessfulExecutions = 90,
            FailedExecutions = 10
        };
        _executorMock.Setup(e => e.GetStatistics()).Returns(stats);

        // Act
        var status = _lifecycleManager.GetStatus();

        // Assert
        status.RegisteredToolsCount.Should().Be(3);
        status.ActiveExecutionsCount.Should().Be(2);
        status.TotalExecutions.Should().Be(100);
    }

    [Fact]
    public async Task GetStatus_ShouldReflectInitializationState()
    {
        // Arrange & Act
        var statusBefore = _lifecycleManager.GetStatus();
        await _lifecycleManager.InitializeAsync();
        var statusAfter = _lifecycleManager.GetStatus();

        // Assert
        statusBefore.IsInitialized.Should().BeFalse();
        statusBefore.InitializedAt.Should().BeNull();

        statusAfter.IsInitialized.Should().BeTrue();
        statusAfter.InitializedAt.Should().NotBeNull();
    }

    [Fact]
    public void GetStatus_ShouldReturnIndependentInstance()
    {
        // Act
        var status1 = _lifecycleManager.GetStatus();
        var status2 = _lifecycleManager.GetStatus();

        // Assert
        status1.Should().NotBeSameAs(status2);
        
        // Modifying one should not affect the other
        status1.StartupErrors.Add("Test error");
        status2.StartupErrors.Should().BeEmpty();
    }

    #endregion

    // Sample test classes
    private class SampleTool : ToolBase
    {
        public override ToolMetadata Metadata => new()
        {
            Id = "sample_tool",
            Name = "Sample Tool",
            Description = "A sample tool for testing"
        };

        protected override Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
        {
            return Task.FromResult(ToolResult.Success());
        }
    }

    private class AnotherSampleTool : ToolBase
    {
        public override ToolMetadata Metadata => new()
        {
            Id = "another_tool",
            Name = "Another Tool",
            Description = "Another sample tool for testing"
        };

        protected override Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
        {
            return Task.FromResult(ToolResult.Success());
        }
    }

    private class InvalidTool : ToolBase
    {
        public override ToolMetadata Metadata => new()
        {
            Id = "invalid_tool",
            Name = "Invalid Tool",
            Description = "An invalid tool for testing"
        };

        protected override Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
        {
            throw new NotImplementedException();
        }
    }
}