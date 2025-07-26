using Andy.Tools.Advanced.ToolChains;
using Andy.Tools.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Andy.Tools.Tests.Advanced;

public class ToolChainTests
{
    private readonly Mock<IToolExecutor> _mockToolExecutor;
    private readonly Mock<ILogger<ToolChain>> _mockLogger;
    private readonly Mock<IToolChainStep> _mockStep;
    private readonly ToolChain _toolChain;
    private readonly ToolExecutionContext _defaultContext;

    public ToolChainTests()
    {
        _mockToolExecutor = new Mock<IToolExecutor>();
        _mockLogger = new Mock<ILogger<ToolChain>>();
        _mockStep = new Mock<IToolChainStep>();

        _toolChain = new ToolChain(
            "test-chain",
            "Test Chain",
            "Test chain for unit tests",
            _mockToolExecutor.Object,
            _mockLogger.Object);

        _defaultContext = new ToolExecutionContext
        {
            UserId = "test-user",
            CorrelationId = "test-correlation"
        };

        SetupDefaultMockStep();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Assert
        _toolChain.Id.Should().Be("test-chain");
        _toolChain.Name.Should().Be("Test Chain");
        _toolChain.Description.Should().Be("Test chain for unit tests");
        _toolChain.Steps.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullToolExecutor_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ToolChain("id", "name", "desc", null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ToolChain("id", "name", "desc", _mockToolExecutor.Object, null!));
    }

    #endregion

    #region AddStep Tests

    [Fact]
    public void AddStep_WithValidStep_ShouldAddStepToChain()
    {
        // Act
        var result = _toolChain.AddStep(_mockStep.Object);

        // Assert
        result.Should().BeSameAs(_toolChain); // Fluent interface
        _toolChain.Steps.Should().HaveCount(1);
        _toolChain.Steps[0].Should().BeSameAs(_mockStep.Object);
    }

    [Fact]
    public void AddStep_WithNullStep_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _toolChain.AddStep(null!));
    }

    [Fact]
    public void AddStep_WithMultipleSteps_ShouldMaintainOrder()
    {
        // Arrange
        var step1 = new Mock<IToolChainStep>();
        var step2 = new Mock<IToolChainStep>();

        step1.Setup(x => x.Id).Returns("step1");
        step2.Setup(x => x.Id).Returns("step2");

        // Act
        _toolChain.AddStep(step1.Object).AddStep(step2.Object);

        // Assert
        _toolChain.Steps.Should().HaveCount(2);
        _toolChain.Steps[0].Id.Should().Be("step1");
        _toolChain.Steps[1].Id.Should().Be("step2");
    }

    #endregion

    #region AddToolStep Tests

    [Fact]
    public void AddToolStep_WithValidParameters_ShouldCreateAndAddToolStep()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { ["input"] = "test" };

        // Act
        var result = _toolChain.AddToolStep("test-tool", parameters, "Test Tool Step");

        // Assert
        result.Should().BeSameAs(_toolChain);
        _toolChain.Steps.Should().HaveCount(1);
        _toolChain.Steps[0].Name.Should().Be("Test Tool Step");
    }

    [Fact]
    public void AddToolStep_WithoutName_ShouldUseDefaultName()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>();

        // Act
        _toolChain.AddToolStep("test-tool", parameters);

        // Assert
        _toolChain.Steps[0].Name.Should().Be("Execute test-tool");
    }

    #endregion

    #region AddConditionalStep Tests

    [Fact]
    public void AddConditionalStep_WithValidCondition_ShouldCreateAndAddConditionalStep()
    {
        // Arrange
        var thenStep = new Mock<IToolChainStep>();
        var elseStep = new Mock<IToolChainStep>();

        // Act
        var result = _toolChain.AddConditionalStep(
            context => true,
            thenStep.Object,
            elseStep.Object);

        // Assert
        result.Should().BeSameAs(_toolChain);
        _toolChain.Steps.Should().HaveCount(1);
        _toolChain.Steps[0].Name.Should().Be("Conditional");
    }

    [Fact]
    public void AddConditionalStep_WithoutElseStep_ShouldCreateConditionalStepWithNullElse()
    {
        // Arrange
        var thenStep = new Mock<IToolChainStep>();

        // Act
        _toolChain.AddConditionalStep(context => true, thenStep.Object);

        // Assert
        _toolChain.Steps.Should().HaveCount(1);
    }

    #endregion

    #region AddParallelStep Tests

    [Fact]
    public void AddParallelStep_WithValidSteps_ShouldCreateAndAddParallelStep()
    {
        // Arrange
        var steps = new List<IToolChainStep>
        {
            new Mock<IToolChainStep>().Object,
            new Mock<IToolChainStep>().Object
        };

        // Act
        var result = _toolChain.AddParallelStep(steps, "Parallel Test");

        // Assert
        result.Should().BeSameAs(_toolChain);
        _toolChain.Steps.Should().HaveCount(1);
        _toolChain.Steps[0].Name.Should().Be("Parallel Test");
    }

    [Fact]
    public void AddParallelStep_WithEmptySteps_ShouldThrowArgumentException()
    {
        // Arrange
        var emptySteps = new List<IToolChainStep>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _toolChain.AddParallelStep(emptySteps));
    }

    [Fact]
    public void AddParallelStep_WithoutName_ShouldUseDefaultName()
    {
        // Arrange
        var steps = new List<IToolChainStep> { new Mock<IToolChainStep>().Object };

        // Act
        _toolChain.AddParallelStep(steps);

        // Assert
        _toolChain.Steps[0].Name.Should().Be("Parallel Execution");
    }

    #endregion

    #region AddTransformStep Tests

    [Fact]
    public void AddTransformStep_WithValidTransform_ShouldCreateAndAddTransformStep()
    {
        // Arrange
        Func<object?, ToolChainContext, Task<object?>> transform =
            (input, context) => Task.FromResult<object?>("transformed");

        // Act
        var result = _toolChain.AddTransformStep(transform, "Transform Test");

        // Assert
        result.Should().BeSameAs(_toolChain);
        _toolChain.Steps.Should().HaveCount(1);
        _toolChain.Steps[0].Name.Should().Be("Transform Test");
    }

    [Fact]
    public void AddTransformStep_WithoutName_ShouldUseDefaultName()
    {
        // Arrange
        Func<object?, ToolChainContext, Task<object?>> transform =
            (input, context) => Task.FromResult<object?>(input);

        // Act
        _toolChain.AddTransformStep(transform);

        // Assert
        _toolChain.Steps[0].Name.Should().Be("Transform");
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithValidChain_ShouldReturnNoErrors()
    {
        // Arrange
        _toolChain.AddStep(_mockStep.Object);

        // Act
        var errors = _toolChain.Validate();

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithEmptySteps_ShouldReturnError()
    {
        // Act
        var errors = _toolChain.Validate();

        // Assert
        errors.Should().Contain("Tool chain must contain at least one step");
    }

    [Fact]
    public void Validate_WithInvalidDependency_ShouldReturnError()
    {
        // Arrange
        var stepWithBadDep = new Mock<IToolChainStep>();
        stepWithBadDep.Setup(x => x.Id).Returns("step1");
        stepWithBadDep.Setup(x => x.Name).Returns("Step 1");
        stepWithBadDep.Setup(x => x.Dependencies).Returns(new List<string> { "non-existent-step" });

        _toolChain.AddStep(stepWithBadDep.Object);

        // Act
        var errors = _toolChain.Validate();

        // Assert
        errors.Should().Contain("Step 'Step 1' has dependency on unknown step 'non-existent-step'");
    }

    [Fact]
    public void Validate_WithCircularDependency_ShouldReturnError()
    {
        // Arrange
        var step1 = new Mock<IToolChainStep>();
        var step2 = new Mock<IToolChainStep>();

        step1.Setup(x => x.Id).Returns("step1");
        step1.Setup(x => x.Name).Returns("Step 1");
        step1.Setup(x => x.Dependencies).Returns(new List<string> { "step2" });

        step2.Setup(x => x.Id).Returns("step2");
        step2.Setup(x => x.Name).Returns("Step 2");
        step2.Setup(x => x.Dependencies).Returns(new List<string> { "step1" });

        _toolChain.AddStep(step1.Object).AddStep(step2.Object);

        // Act
        var errors = _toolChain.Validate();

        // Assert
        errors.Should().Contain(e => e.Contains("Circular dependency detected"));
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WithValidationErrors_ShouldReturnFailedResult()
    {
        // Arrange - empty chain will have validation errors

        // Act
        var result = await _toolChain.ExecuteAsync(null, _defaultContext);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ToolChainExecutionStatus.Failed);
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR");
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulStep_ShouldReturnCompletedResult()
    {
        // Arrange
        var stepResult = new ToolChainStepResult
        {
            StepId = "test-step",
            StepName = "Test Step",
            IsSuccessful = true,
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow
        };

        _mockStep.Setup(x => x.ExecuteAsync(It.IsAny<ToolChainContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stepResult);

        _toolChain.AddStep(_mockStep.Object);

        // Act
        var result = await _toolChain.ExecuteAsync(null, _defaultContext);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ToolChainExecutionStatus.Completed);
        result.StepResults.Should().ContainKey("test-step");
        result.SuccessfulSteps.Should().Be(1);
        result.FailedSteps.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedStep_ShouldReturnFailedResult()
    {
        // Arrange
        var stepResult = new ToolChainStepResult
        {
            StepId = "test-step",
            StepName = "Test Step",
            IsSuccessful = false,
            ErrorMessage = "Step failed",
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow
        };

        _mockStep.Setup(x => x.ExecuteAsync(It.IsAny<ToolChainContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stepResult);

        _toolChain.AddStep(_mockStep.Object);

        // Act
        var result = await _toolChain.ExecuteAsync(null, _defaultContext);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ToolChainExecutionStatus.Failed);
        result.Errors.Should().Contain(e => e.Code == "STEP_FAILED");
        result.FailedSteps.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithStepException_ShouldHandleException()
    {
        // Arrange
        _mockStep.Setup(x => x.ExecuteAsync(It.IsAny<ToolChainContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        _toolChain.AddStep(_mockStep.Object);

        // Act
        var result = await _toolChain.ExecuteAsync(null, _defaultContext);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ToolChainExecutionStatus.Failed);
        result.Errors.Should().Contain(e => e.Code == "STEP_EXCEPTION" && e.Message == "Test exception");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldReturnCancelledResult()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockStep.Setup(x => x.ExecuteAsync(It.IsAny<ToolChainContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        _toolChain.AddStep(_mockStep.Object);

        // Act
        var result = await _toolChain.ExecuteAsync(null, _defaultContext, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ToolChainExecutionStatus.Cancelled);
    }

    [Fact]
    public async Task ExecuteAsync_WithInitialParameters_ShouldPassParametersToContext()
    {
        // Arrange
        var initialParams = new Dictionary<string, object?> { ["test"] = "value" };
        ToolChainContext? capturedContext = null;

        _mockStep.Setup(x => x.ExecuteAsync(It.IsAny<ToolChainContext>(), It.IsAny<CancellationToken>()))
            .Callback<ToolChainContext, CancellationToken>((context, token) => capturedContext = context)
            .ReturnsAsync(new ToolChainStepResult
            {
                StepId = "test-step",
                StepName = "Test Step",
                IsSuccessful = true,
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow
            });

        _toolChain.AddStep(_mockStep.Object);

        // Act
        await _toolChain.ExecuteAsync(initialParams, _defaultContext);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.InitialParameters.Should().ContainKey("test");
        capturedContext.InitialParameters["test"].Should().Be("value");
    }

    [Fact]
    public async Task ExecuteAsync_WithRetryableFailedStep_ShouldRetryStep()
    {
        // Arrange
        var retryableStep = new Mock<IToolChainStep>();
        retryableStep.Setup(x => x.Id).Returns("retry-step");
        retryableStep.Setup(x => x.Name).Returns("Retry Step");
        retryableStep.Setup(x => x.Dependencies).Returns(new List<string>());
        retryableStep.Setup(x => x.IsRetryable).Returns(true);
        retryableStep.Setup(x => x.MaxRetries).Returns(2);

        var callCount = 0;
        retryableStep.Setup(x => x.ExecuteAsync(It.IsAny<ToolChainContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 3)
                {
                    return new ToolChainStepResult
                    {
                        StepId = "retry-step",
                        StepName = "Retry Step",
                        IsSuccessful = false,
                        ErrorMessage = "Temporary failure",
                        StartTime = DateTimeOffset.UtcNow,
                        EndTime = DateTimeOffset.UtcNow
                    };
                }

                return new ToolChainStepResult
                {
                    StepId = "retry-step",
                    StepName = "Retry Step",
                    IsSuccessful = true,
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = DateTimeOffset.UtcNow
                };
            });

        _toolChain.AddStep(retryableStep.Object);

        // Act
        var result = await _toolChain.ExecuteAsync(null, _defaultContext);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ToolChainExecutionStatus.Completed);
        callCount.Should().Be(3); // Initial attempt + 2 retries
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRespectStepDependencies()
    {
        // Arrange
        var step1 = new Mock<IToolChainStep>();
        var step2 = new Mock<IToolChainStep>();

        step1.Setup(x => x.Id).Returns("step1");
        step1.Setup(x => x.Name).Returns("Step 1");
        step1.Setup(x => x.Dependencies).Returns(new List<string>());

        step2.Setup(x => x.Id).Returns("step2");
        step2.Setup(x => x.Name).Returns("Step 2");
        step2.Setup(x => x.Dependencies).Returns(new List<string> { "step1" });

        var executionOrder = new List<string>();

        step1.Setup(x => x.ExecuteAsync(It.IsAny<ToolChainContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add("step1"))
            .ReturnsAsync(new ToolChainStepResult
            {
                StepId = "step1",
                StepName = "Step 1",
                IsSuccessful = true,
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow
            });

        step2.Setup(x => x.ExecuteAsync(It.IsAny<ToolChainContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add("step2"))
            .ReturnsAsync(new ToolChainStepResult
            {
                StepId = "step2",
                StepName = "Step 2",
                IsSuccessful = true,
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow
            });

        _toolChain.AddStep(step2.Object).AddStep(step1.Object); // Add in reverse order

        // Act
        var result = await _toolChain.ExecuteAsync(null, _defaultContext);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ToolChainExecutionStatus.Completed);
        executionOrder.Should().Equal("step1", "step2"); // Should execute in dependency order
    }

    #endregion

    #region Helper Methods

    private void SetupDefaultMockStep()
    {
        _mockStep.Setup(x => x.Id).Returns("test-step");
        _mockStep.Setup(x => x.Name).Returns("Test Step");
        _mockStep.Setup(x => x.Dependencies).Returns(new List<string>());
        _mockStep.Setup(x => x.IsRetryable).Returns(false);
        _mockStep.Setup(x => x.MaxRetries).Returns(0);
        _mockStep.Setup(x => x.Type).Returns(ToolChainStepType.Tool);
    }

    #endregion
}

public class ToolChainBuilderTests
{
    private readonly Mock<IToolExecutor> _mockToolExecutor;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger<ToolChain>> _mockLogger;
    private readonly ToolChainBuilder _builder;

    public ToolChainBuilderTests()
    {
        _mockToolExecutor = new Mock<IToolExecutor>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger<ToolChain>>();

        // Use the non-generic CreateLogger method which is not an extension method
        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);

        _builder = new ToolChainBuilder(_mockToolExecutor.Object, _mockLoggerFactory.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeBuilder()
    {
        // Assert
        _builder.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullToolExecutor_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ToolChainBuilder(null!, _mockLoggerFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ToolChainBuilder(_mockToolExecutor.Object, null!));
    }

    #endregion

    #region Builder Method Tests

    [Fact]
    public void WithId_ShouldSetChainId()
    {
        // Act
        var result = _builder.WithId("custom-id");
        var chain = result.Build();

        // Assert
        result.Should().BeSameAs(_builder); // Fluent interface
        chain.Id.Should().Be("custom-id");
    }

    [Fact]
    public void WithName_ShouldSetChainName()
    {
        // Act
        var result = _builder.WithName("Custom Name");
        var chain = result.Build();

        // Assert
        result.Should().BeSameAs(_builder);
        chain.Name.Should().Be("Custom Name");
    }

    [Fact]
    public void WithDescription_ShouldSetChainDescription()
    {
        // Act
        var result = _builder.WithDescription("Custom Description");
        var chain = result.Build();

        // Assert
        result.Should().BeSameAs(_builder);
        chain.Description.Should().Be("Custom Description");
    }

    [Fact]
    public void Build_WithDefaultValues_ShouldCreateChainWithDefaults()
    {
        // Act
        var chain = _builder.Build();

        // Assert
        chain.Should().NotBeNull();
        chain.Id.Should().NotBeNullOrEmpty();
        chain.Name.Should().Be("Unnamed Chain");
        chain.Description.Should().BeEmpty();
    }

    [Fact]
    public void FluentInterface_ShouldAllowChaining()
    {
        // Act
        var chain = _builder
            .WithId("fluent-test")
            .WithName("Fluent Test Chain")
            .WithDescription("Testing fluent interface")
            .Build();

        // Assert
        chain.Id.Should().Be("fluent-test");
        chain.Name.Should().Be("Fluent Test Chain");
        chain.Description.Should().Be("Testing fluent interface");
    }

    #endregion
}
