using System.Collections.Concurrent;
using System.Diagnostics;
using Andy.Tools.Core;
using Andy.Tools.Core.OutputLimiting;
using Andy.Tools.Execution;
using Andy.Tools.Observability;
using Andy.Tools.Validation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Andy.Tools.Tests.Execution;

public class ToolExecutorTests : IDisposable
{
    private readonly Mock<IToolRegistry> _mockRegistry;
    private readonly Mock<IToolValidator> _mockValidator;
    private readonly Mock<ISecurityManager> _mockSecurityManager;
    private readonly Mock<IResourceMonitor> _mockResourceMonitor;
    private readonly Mock<IToolOutputLimiter> _mockOutputLimiter;
    private readonly Mock<IToolObservabilityService> _mockObservabilityService;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ILogger<ToolExecutor>> _mockLogger;
    private readonly Mock<ITool> _mockTool;
    private readonly Mock<IResourceMonitoringSession> _mockMonitoringSession;
    private readonly ToolExecutor _executor;
    private readonly ToolRegistration _testRegistration;
    private readonly ToolExecutionRequest _testRequest;

    public ToolExecutorTests()
    {
        _mockRegistry = new Mock<IToolRegistry>();
        _mockValidator = new Mock<IToolValidator>();
        _mockSecurityManager = new Mock<ISecurityManager>();
        _mockResourceMonitor = new Mock<IResourceMonitor>();
        _mockOutputLimiter = new Mock<IToolOutputLimiter>();
        _mockObservabilityService = new Mock<IToolObservabilityService>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogger = new Mock<ILogger<ToolExecutor>>();
        _mockTool = new Mock<ITool>();
        _mockMonitoringSession = new Mock<IResourceMonitoringSession>();

        // Setup service provider to return observability service
        _mockServiceProvider.Setup(x => x.GetService(typeof(IToolObservabilityService)))
            .Returns(_mockObservabilityService.Object);

        // Setup default behavior for security manager
        _mockSecurityManager.Setup(x => x.GetViolations(It.IsAny<string>()))
            .Returns(new List<SecurityViolation>());

        _executor = new ToolExecutor(
            _mockRegistry.Object,
            _mockValidator.Object,
            _mockSecurityManager.Object,
            _mockResourceMonitor.Object,
            _mockOutputLimiter.Object,
            _mockServiceProvider.Object,
            _mockLogger.Object);

        _testRegistration = new ToolRegistration
        {
            Metadata = new ToolMetadata
            {
                Id = "test-tool",
                Name = "Test Tool",
                Description = "Test tool for unit tests",
                Category = ToolCategory.Development
            },
            IsEnabled = true,
            Configuration = new Dictionary<string, object?>()
        };

        _testRequest = new ToolExecutionRequest
        {
            ToolId = "test-tool",
            Parameters = new Dictionary<string, object?> { ["input"] = "test" },
            Context = new ToolExecutionContext
            {
                UserId = "test-user",
                CorrelationId = "test-correlation-id"
            }
        };
    }

    public void Dispose()
    {
        _executor?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithAllValidDependencies_ShouldInitializeSuccessfully()
    {
        // Arrange & Act & Assert
        _executor.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullRegistry_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ToolExecutor(
                null!,
                _mockValidator.Object,
                _mockSecurityManager.Object,
                _mockResourceMonitor.Object,
                _mockOutputLimiter.Object,
                _mockServiceProvider.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullValidator_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ToolExecutor(
                _mockRegistry.Object,
                null!,
                _mockSecurityManager.Object,
                _mockResourceMonitor.Object,
                _mockOutputLimiter.Object,
                _mockServiceProvider.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullSecurityManager_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ToolExecutor(
                _mockRegistry.Object,
                _mockValidator.Object,
                null!,
                _mockResourceMonitor.Object,
                _mockOutputLimiter.Object,
                _mockServiceProvider.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullResourceMonitor_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ToolExecutor(
                _mockRegistry.Object,
                _mockValidator.Object,
                _mockSecurityManager.Object,
                null!,
                _mockOutputLimiter.Object,
                _mockServiceProvider.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullOutputLimiter_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ToolExecutor(
                _mockRegistry.Object,
                _mockValidator.Object,
                _mockSecurityManager.Object,
                _mockResourceMonitor.Object,
                null!,
                _mockServiceProvider.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ToolExecutor(
                _mockRegistry.Object,
                _mockValidator.Object,
                _mockSecurityManager.Object,
                _mockResourceMonitor.Object,
                _mockOutputLimiter.Object,
                null!,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ToolExecutor(
                _mockRegistry.Object,
                _mockValidator.Object,
                _mockSecurityManager.Object,
                _mockResourceMonitor.Object,
                _mockOutputLimiter.Object,
                _mockServiceProvider.Object,
                null!));
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulExecution_ShouldReturnSuccessResult()
    {
        // Arrange
        var expectedResult = new ToolResult
        {
            IsSuccessful = true,
            Data = "test result",
            DurationMs = 100
        };

        SetupSuccessfulExecution(expectedResult);

        // Act
        var result = await _executor.ExecuteAsync(_testRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().Be("test result");
        result.ToolId.Should().Be("test-tool");
        result.CorrelationId.Should().Be("test-correlation-id");
        result.DurationMs.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteAsync_WithToolNotFound_ShouldReturnFailureResult()
    {
        // Arrange
        _mockRegistry.Setup(x => x.GetTool("test-tool")).Returns((ToolRegistration?)null);

        // Act
        var result = await _executor.ExecuteAsync(_testRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("Tool 'test-tool' not found");
    }

    [Fact]
    public async Task ExecuteAsync_WithDisabledTool_ShouldReturnFailureResult()
    {
        // Arrange
        var disabledRegistration = new ToolRegistration
        {
            Metadata = _testRegistration.Metadata,
            IsEnabled = false
        };
        _mockRegistry.Setup(x => x.GetTool("test-tool")).Returns(disabledRegistration);

        // Act
        var result = await _executor.ExecuteAsync(_testRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("Tool 'test-tool' is disabled");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidationFailure_ShouldReturnFailureResult()
    {
        // Arrange
        _mockRegistry.Setup(x => x.GetTool("test-tool")).Returns(_testRegistration);

        var validationResult = new ValidationResult
        {
            IsValid = false,
            Errors = new List<ValidationError>
            {
                new ValidationError("ValidationError", "Test validation error")
            }
        };
        _mockValidator.Setup(x => x.ValidateExecutionRequest(It.IsAny<ToolExecutionRequest>(), It.IsAny<ToolMetadata>()))
            .Returns(validationResult);

        var request = new ToolExecutionRequest
        {
            ToolId = "test-tool",
            Parameters = new Dictionary<string, object?>(),
            Context = new ToolExecutionContext(),
            ValidateParameters = true
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Validation failed");
        result.ErrorMessage.Should().Contain("Test validation error");
    }

    [Fact]
    public async Task ExecuteAsync_WithSecurityViolation_ShouldReturnFailureResult()
    {
        // Arrange
        _mockRegistry.Setup(x => x.GetTool("test-tool")).Returns(_testRegistration);

        var securityViolations = new List<string> { "Permission denied", "Insufficient privileges" };
        _mockSecurityManager.Setup(x => x.ValidateExecution(It.IsAny<ToolMetadata>(), It.IsAny<ToolPermissions>()))
            .Returns(securityViolations);

        var request = new ToolExecutionRequest
        {
            ToolId = "test-tool",
            Parameters = new Dictionary<string, object?>(),
            Context = new ToolExecutionContext(),
            EnforcePermissions = true
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Security validation failed");
        result.SecurityViolations.Should().NotBeEmpty();
        result.SecurityViolations.Should().Contain("Permission denied");
    }

    [Fact]
    public async Task ExecuteAsync_WithToolCreationFailure_ShouldReturnFailureResult()
    {
        // Arrange
        _mockRegistry.Setup(x => x.GetTool("test-tool")).Returns(_testRegistration);
        _mockRegistry.Setup(x => x.CreateTool("test-tool", It.IsAny<IServiceProvider>())).Returns((ITool?)null);

        // Act
        var result = await _executor.ExecuteAsync(_testRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("Failed to create instance of tool 'test-tool'");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldReturnCancelledResult()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var cancelledRequest = new ToolExecutionRequest
        {
            ToolId = "test-tool",
            Parameters = new Dictionary<string, object?>(),
            Context = new ToolExecutionContext
            {
                CancellationToken = cts.Token
            }
        };

        _mockRegistry.Setup(x => x.GetTool("test-tool")).Returns(_testRegistration);
        _mockRegistry.Setup(x => x.CreateTool("test-tool", It.IsAny<IServiceProvider>())).Returns(_mockTool.Object);

        _mockTool.Setup(x => x.ExecuteAsync(It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .ThrowsAsync(new OperationCanceledException());

        cts.Cancel();

        // Act
        var result = await _executor.ExecuteAsync(cancelledRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.WasCancelled.Should().BeTrue();
        result.ErrorMessage.Should().Be("Tool execution was cancelled");
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ShouldReturnFailureResult()
    {
        // Arrange
        _mockRegistry.Setup(x => x.GetTool("test-tool")).Returns(_testRegistration);
        _mockRegistry.Setup(x => x.CreateTool("test-tool", It.IsAny<IServiceProvider>())).Returns(_mockTool.Object);

        _mockTool.Setup(x => x.ExecuteAsync(It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        var result = await _executor.ExecuteAsync(_testRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("Test exception");
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_ShouldRespectTimeout()
    {
        // Arrange
        var timeoutRequest = new ToolExecutionRequest
        {
            ToolId = "test-tool",
            Parameters = new Dictionary<string, object?>(),
            Context = new ToolExecutionContext(),
            TimeoutMs = 100
        };

        _mockRegistry.Setup(x => x.GetTool("test-tool")).Returns(_testRegistration);
        _mockRegistry.Setup(x => x.CreateTool("test-tool", It.IsAny<IServiceProvider>())).Returns(_mockTool.Object);

        // Simulate a long-running operation
        _mockTool.Setup(x => x.ExecuteAsync(It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .Returns(async (Dictionary<string, object?> parameters, ToolExecutionContext context) =>
            {
                await Task.Delay(500, context.CancellationToken); // Will be cancelled by timeout
                return new ToolResult { IsSuccessful = true };
            });

        // Act
        var result = await _executor.ExecuteAsync(timeoutRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.WasCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithResourceMonitoring_ShouldStartAndStopMonitoring()
    {
        // Arrange
        var monitoringRequest = new ToolExecutionRequest
        {
            ToolId = "test-tool",
            Parameters = new Dictionary<string, object?>(),
            Context = new ToolExecutionContext
            {
                ResourceLimits = new ToolResourceLimits { MaxMemoryBytes = 100 }
            },
            EnforceResourceLimits = true
        };

        var resourceUsage = new ToolResourceUsage
        {
            PeakMemoryBytes = 50,
            CpuTimeMs = 100
        };

        _mockResourceMonitor.Setup(x => x.StartMonitoring(It.IsAny<string>(), It.IsAny<ToolResourceLimits>()))
            .Returns(_mockMonitoringSession.Object);
        _mockResourceMonitor.Setup(x => x.StopMonitoring(It.IsAny<string>()))
            .Returns(resourceUsage);

        SetupSuccessfulExecution();

        // Act
        var result = await _executor.ExecuteAsync(monitoringRequest);

        // Assert
        result.Should().NotBeNull();
        result.ResourceUsage.Should().NotBeNull();
        result.ResourceUsage.PeakMemoryBytes.Should().Be(50);

        _mockResourceMonitor.Verify(x => x.StartMonitoring(It.IsAny<string>(), It.IsAny<ToolResourceLimits>()), Times.Once);
        _mockResourceMonitor.Verify(x => x.StopMonitoring(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRaiseExecutionStartedAndCompletedEvents()
    {
        // Arrange
        ToolExecutionStartedEventArgs? startedEventArgs = null;
        ToolExecutionCompletedEventArgs? completedEventArgs = null;

        _executor.ExecutionStarted += (sender, args) => startedEventArgs = args;
        _executor.ExecutionCompleted += (sender, args) => completedEventArgs = args;

        SetupSuccessfulExecution();

        // Act
        var result = await _executor.ExecuteAsync(_testRequest);

        // Assert
        startedEventArgs.Should().NotBeNull();
        startedEventArgs.ToolId.Should().Be("test-tool");
        startedEventArgs.CorrelationId.Should().Be("test-correlation-id");

        completedEventArgs.Should().NotBeNull();
        completedEventArgs.Result.Should().NotBeNull();
        completedEventArgs.Result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithObservabilityService_ShouldTrackExecution()
    {
        // Arrange
        var activity = new Activity("test-activity");
        _mockObservabilityService.Setup(x => x.StartToolExecution(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .Returns(activity);

        SetupSuccessfulExecution();

        // Act
        var result = await _executor.ExecuteAsync(_testRequest);

        // Assert
        _mockObservabilityService.Verify(x => x.StartToolExecution("test-tool", It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()), Times.Once);
        _mockObservabilityService.Verify(x => x.CompleteToolExecution(activity, It.IsAny<ToolExecutionResult>()), Times.Once);
    }

    #endregion

    #region ExecuteAsync Simple Overload Tests

    [Fact]
    public async Task ExecuteAsync_SimpleOverload_ShouldCreateRequestAndExecute()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { ["input"] = "test" };
        var context = new ToolExecutionContext { UserId = "test-user" };

        SetupSuccessfulExecution();

        // Act
        var result = await _executor.ExecuteAsync("test-tool", parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.ToolId.Should().Be("test-tool");
    }

    [Fact]
    public async Task ExecuteAsync_SimpleOverloadWithNullContext_ShouldCreateDefaultContext()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { ["input"] = "test" };

        SetupSuccessfulExecution();

        // Act
        var result = await _executor.ExecuteAsync("test-tool", parameters, null);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.ToolId.Should().Be("test-tool");
    }

    #endregion

    #region ValidateExecutionRequestAsync Tests

    [Fact]
    public async Task ValidateExecutionRequestAsync_WithValidRequest_ShouldReturnNoErrors()
    {
        // Arrange
        _mockRegistry.Setup(x => x.GetTool("test-tool")).Returns(_testRegistration);

        var validationResult = new ValidationResult { IsValid = true, Errors = new List<ValidationError>() };
        _mockValidator.Setup(x => x.ValidateExecutionRequest(It.IsAny<ToolExecutionRequest>(), It.IsAny<ToolMetadata>()))
            .Returns(validationResult);

        // Act
        var errors = await _executor.ValidateExecutionRequestAsync(_testRequest);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateExecutionRequestAsync_WithInvalidRequest_ShouldReturnErrors()
    {
        // Arrange
        _mockRegistry.Setup(x => x.GetTool("test-tool")).Returns(_testRegistration);

        var validationResult = new ValidationResult
        {
            IsValid = false,
            Errors = new List<ValidationError>
            {
                new ValidationError("ValidationError", "Test validation error")
            }
        };
        _mockValidator.Setup(x => x.ValidateExecutionRequest(It.IsAny<ToolExecutionRequest>(), It.IsAny<ToolMetadata>()))
            .Returns(validationResult);

        // Act
        var errors = await _executor.ValidateExecutionRequestAsync(_testRequest);

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain("Test validation error");
    }

    [Fact]
    public async Task ValidateExecutionRequestAsync_WithToolNotFound_ShouldReturnError()
    {
        // Arrange
        _mockRegistry.Setup(x => x.GetTool("test-tool")).Returns((ToolRegistration?)null);

        // Act
        var errors = await _executor.ValidateExecutionRequestAsync(_testRequest);

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain("Tool 'test-tool' not found");
    }

    #endregion

    #region EstimateResourceUsageAsync Tests

    [Fact]
    public async Task EstimateResourceUsageAsync_ShouldReturnNull()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { ["input"] = "test" };

        // Act
        var result = await _executor.EstimateResourceUsageAsync("test-tool", parameters);

        // Assert
        result.Should().BeNull(); // Current implementation returns null
    }

    #endregion

    #region CancelExecutionsAsync Tests

    [Fact]
    public async Task CancelExecutionsAsync_WithExistingExecution_ShouldCancelAndReturnCount()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var runningExecutions = new ConcurrentDictionary<string, CancellationTokenSource>();
        runningExecutions.TryAdd("test-correlation-id", cts);

        // Use reflection to set the private field for testing
        var field = typeof(ToolExecutor).GetField("_runningExecutions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_executor, runningExecutions);

        // Act
        var cancelledCount = await _executor.CancelExecutionsAsync("test-correlation-id");

        // Assert
        cancelledCount.Should().Be(1);
        cts.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task CancelExecutionsAsync_WithNoMatchingExecution_ShouldReturnZero()
    {
        // Act
        var cancelledCount = await _executor.CancelExecutionsAsync("non-existent-id");

        // Assert
        cancelledCount.Should().Be(0);
    }

    [Fact]
    public async Task CancelExecutionsAsync_WithEmptyCorrelationId_ShouldReturnZero()
    {
        // Act
        var cancelledCount = await _executor.CancelExecutionsAsync("");

        // Assert
        cancelledCount.Should().Be(0);
    }

    #endregion

    #region GetRunningExecutions Tests

    [Fact]
    public void GetRunningExecutions_ShouldReturnActiveExecutions()
    {
        // Arrange
        var activeSessions = new List<IResourceMonitoringSession>
        {
            Mock.Of<IResourceMonitoringSession>(s =>
                s.CorrelationId == "session1" &&
                s.StartTime == DateTimeOffset.UtcNow.AddMinutes(-1) &&
                s.CurrentUsage == new ToolResourceUsage { PeakMemoryBytes = 50 })
        };

        _mockResourceMonitor.Setup(x => x.GetActiveSessions()).Returns(activeSessions);

        // Act
        var runningExecutions = _executor.GetRunningExecutions();

        // Assert
        runningExecutions.Should().NotBeNull();
        runningExecutions.Should().HaveCount(1);
        runningExecutions[0].CorrelationId.Should().Be("session1");
    }

    #endregion

    #region GetStatistics Tests

    [Fact]
    public void GetStatistics_ShouldReturnInitialStatistics()
    {
        // Act
        var statistics = _executor.GetStatistics();

        // Assert
        statistics.Should().NotBeNull();
        statistics.TotalExecutions.Should().Be(0);
        statistics.SuccessfulExecutions.Should().Be(0);
        statistics.FailedExecutions.Should().Be(0);
        statistics.CancelledExecutions.Should().Be(0);
        statistics.AverageExecutionTimeMs.Should().Be(0);
    }

    [Fact]
    public async Task GetStatistics_AfterSuccessfulExecution_ShouldUpdateStatistics()
    {
        // Arrange
        SetupSuccessfulExecution();

        // Act
        await _executor.ExecuteAsync(_testRequest);
        var statistics = _executor.GetStatistics();

        // Assert
        statistics.TotalExecutions.Should().Be(1);
        statistics.SuccessfulExecutions.Should().Be(1);
        statistics.FailedExecutions.Should().Be(0);
        statistics.ExecutionsByTool.Should().ContainKey("test-tool");
        statistics.ExecutionsByUser.Should().ContainKey("test-user");
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task ExecuteAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _executor.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _executor.ExecuteAsync(_testRequest));
    }

    [Fact]
    public void Dispose_ShouldCancelAllRunningExecutions()
    {
        // Arrange
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();
        var runningExecutions = new ConcurrentDictionary<string, CancellationTokenSource>();
        runningExecutions.TryAdd("execution1", cts1);
        runningExecutions.TryAdd("execution2", cts2);

        // Use reflection to set the private field for testing
        var field = typeof(ToolExecutor).GetField("_runningExecutions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_executor, runningExecutions);

        // Act
        _executor.Dispose();

        // Assert
        cts1.Token.IsCancellationRequested.Should().BeTrue();
        cts2.Token.IsCancellationRequested.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private void SetupSuccessfulExecution(ToolResult? toolResult = null)
    {
        toolResult ??= new ToolResult
        {
            IsSuccessful = true,
            Data = "test result",
            DurationMs = 100
        };

        _mockRegistry.Setup(x => x.GetTool("test-tool")).Returns(_testRegistration);
        _mockRegistry.Setup(x => x.CreateTool("test-tool", It.IsAny<IServiceProvider>())).Returns(_mockTool.Object);

        _mockTool.Setup(x => x.ExecuteAsync(It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(toolResult);

        _mockValidator.Setup(x => x.ValidateExecutionRequest(It.IsAny<ToolExecutionRequest>(), It.IsAny<ToolMetadata>()))
            .Returns(new ValidationResult { IsValid = true, Errors = new List<ValidationError>() });

        _mockSecurityManager.Setup(x => x.ValidateExecution(It.IsAny<ToolMetadata>(), It.IsAny<ToolPermissions>()))
            .Returns(new List<string>());

        _mockSecurityManager.Setup(x => x.GetViolations(It.IsAny<string>()))
            .Returns(new List<SecurityViolation>());
    }

    #endregion
}
