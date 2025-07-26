using System.Collections.Generic;
using System.Linq;
using Andy.Tools.Core;
using Andy.Tools.Registry;
using Andy.Tools.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Andy.Tools.Tests.Registry;

public class ToolRegistryTests
{
    private readonly Mock<IToolValidator> _mockValidator;
    private readonly Mock<ILogger<ToolRegistry>> _mockLogger;
    private readonly ToolRegistry _toolRegistry;
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    public ToolRegistryTests()
    {
        _mockValidator = new Mock<IToolValidator>();
        _mockLogger = new Mock<ILogger<ToolRegistry>>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _toolRegistry = new ToolRegistry(_mockValidator.Object, _mockLogger.Object);
    }

    [Fact]
    public void RegisterTool_WithValidType_ShouldRegisterSuccessfully()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(typeof(TestTool)))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        // Act
        var registration = _toolRegistry.RegisterTool<TestTool>();

        // Assert
        Assert.NotNull(registration);
        Assert.Equal("test_tool", registration.Metadata.Id);
        Assert.Equal(typeof(TestTool), registration.ToolType);
        Assert.Single(_toolRegistry.Tools);
    }

    [Fact]
    public void RegisterTool_WithInvalidType_ShouldThrowException()
    {
        // Arrange
        var invalidValidationResult = ValidationResult.Failure(new ValidationError("Test error", "Type"))
;
        _mockValidator.Setup(v => v.ValidateToolType(typeof(TestTool)))
                     .Returns(invalidValidationResult);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _toolRegistry.RegisterTool<TestTool>());
        Assert.Contains("Tool type validation failed", exception.Message);
    }

    [Fact]
    public void RegisterTool_WithDuplicateId_ShouldThrowException()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(typeof(TestTool)))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        _toolRegistry.RegisterTool<TestTool>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _toolRegistry.RegisterTool<TestTool>());
        Assert.Contains("already registered", exception.Message);
    }

    [Fact]
    public void RegisterTool_WithFactory_ShouldRegisterSuccessfully()
    {
        // Arrange
        var metadata = new ToolMetadata
        {
            Id = "factory_tool",
            Name = "Factory Tool",
            Description = "A tool created via factory",
            Category = ToolCategory.Utility
        };
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateMetadata(metadata))
                     .Returns(validationResult);

        static ITool Factory(IServiceProvider sp) => new TestTool();

        // Act
        var registration = _toolRegistry.RegisterTool(metadata, Factory);

        // Assert
        Assert.NotNull(registration);
        Assert.Equal("factory_tool", registration.Metadata.Id);
        Assert.Equal("factory", registration.Source);
        Assert.Single(_toolRegistry.Tools);
    }

    [Fact]
    public void UnregisterTool_ExistingTool_ShouldReturnTrue()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(typeof(TestTool)))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        _toolRegistry.RegisterTool<TestTool>();

        // Act
        var result = _toolRegistry.UnregisterTool("test_tool");

        // Assert
        Assert.True(result);
        Assert.Empty(_toolRegistry.Tools);
    }

    [Fact]
    public void UnregisterTool_NonExistentTool_ShouldReturnFalse()
    {
        // Act
        var result = _toolRegistry.UnregisterTool("non_existent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void UnregisterTool_NullOrEmptyId_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _toolRegistry.UnregisterTool(""));
        Assert.Throws<ArgumentException>(() => _toolRegistry.UnregisterTool(null!));
    }

    [Fact]
    public void GetTool_ExistingTool_ShouldReturnRegistration()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(typeof(TestTool)))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        _toolRegistry.RegisterTool<TestTool>();

        // Act
        var registration = _toolRegistry.GetTool("test_tool");

        // Assert
        Assert.NotNull(registration);
        Assert.Equal("test_tool", registration.Metadata.Id);
    }

    [Fact]
    public void GetTool_NonExistentTool_ShouldReturnNull()
    {
        // Act
        var registration = _toolRegistry.GetTool("non_existent");

        // Assert
        Assert.Null(registration);
    }

    [Fact]
    public void GetTool_NullOrEmptyId_ShouldReturnNull()
    {
        // Act
        var registration1 = _toolRegistry.GetTool("");
        var registration2 = _toolRegistry.GetTool(null!);

        // Assert
        Assert.Null(registration1);
        Assert.Null(registration2);
    }

    [Fact]
    public void GetTools_WithFilters_ShouldReturnFilteredResults()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        _toolRegistry.RegisterTool<TestTool>();
        _toolRegistry.RegisterTool<TestFileSystemTool>();

        // Act
        var systemTools = _toolRegistry.GetTools(category: ToolCategory.System);
        var fileSystemTools = _toolRegistry.GetTools(category: ToolCategory.FileSystem);

        // Assert
        Assert.Single(systemTools);
        Assert.Single(fileSystemTools);
        Assert.Equal("test_tool", systemTools.First().Metadata.Id);
        Assert.Equal("test_filesystem_tool", fileSystemTools.First().Metadata.Id);
    }

    [Fact]
    public void SearchTools_WithSearchTerm_ShouldReturnMatchingTools()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        _toolRegistry.RegisterTool<TestTool>();
        _toolRegistry.RegisterTool<TestFileSystemTool>();

        // Act
        var searchResults = _toolRegistry.SearchTools("test");

        // Assert
        Assert.Equal(2, searchResults.Count);
    }

    [Fact]
    public void CreateTool_ExistingEnabledTool_ShouldReturnInstance()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(typeof(TestTool)))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        _mockServiceProvider.Setup(sp => sp.GetService(typeof(TestTool)))
                           .Returns(new TestTool());

        _toolRegistry.RegisterTool<TestTool>();

        // Act
        var tool = _toolRegistry.CreateTool("test_tool", _mockServiceProvider.Object);

        // Assert
        Assert.NotNull(tool);
        Assert.IsType<TestTool>(tool);
    }

    [Fact]
    public void CreateTool_DisabledTool_ShouldReturnNull()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(typeof(TestTool)))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        _toolRegistry.RegisterTool<TestTool>();
        _toolRegistry.SetToolEnabled("test_tool", false);

        // Act
        var tool = _toolRegistry.CreateTool("test_tool", _mockServiceProvider.Object);

        // Assert
        Assert.Null(tool);
    }

    [Fact]
    public void SetToolEnabled_ExistingTool_ShouldUpdateStatus()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(typeof(TestTool)))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        _toolRegistry.RegisterTool<TestTool>();

        // Act
        var result = _toolRegistry.SetToolEnabled("test_tool", false);
        var registration = _toolRegistry.GetTool("test_tool");

        // Assert
        Assert.True(result);
        Assert.False(registration!.IsEnabled);
    }

    [Fact]
    public void UpdateToolConfiguration_ExistingTool_ShouldUpdateConfig()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(typeof(TestTool)))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        _toolRegistry.RegisterTool<TestTool>();
        var newConfig = new Dictionary<string, object?> { { "key1", "value1" } };

        // Act
        var result = _toolRegistry.UpdateToolConfiguration("test_tool", newConfig);
        var registration = _toolRegistry.GetTool("test_tool");

        // Assert
        Assert.True(result);
        Assert.Equal("value1", registration!.Configuration["key1"]);
    }

    [Fact]
    public void GetStatistics_WithTools_ShouldReturnCorrectStats()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        _toolRegistry.RegisterTool<TestTool>();
        _toolRegistry.RegisterTool<TestFileSystemTool>();
        _toolRegistry.SetToolEnabled("test_tool", false);

        // Act
        var stats = _toolRegistry.GetStatistics();

        // Assert
        Assert.Equal(2, stats.TotalTools);
        Assert.Equal(1, stats.EnabledTools);
        Assert.Equal(1, stats.DisabledTools);
        Assert.Equal(1, stats.ByCategory[ToolCategory.System]);
        Assert.Equal(1, stats.ByCategory[ToolCategory.FileSystem]);
    }

    [Fact]
    public void Clear_WithTools_ShouldRemoveAllTools()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        _toolRegistry.RegisterTool<TestTool>();
        _toolRegistry.RegisterTool<TestFileSystemTool>();

        // Act
        _toolRegistry.Clear();

        // Assert
        Assert.Empty(_toolRegistry.Tools);
    }

    [Fact]
    public void ToolRegistered_Event_ShouldBeRaised()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(typeof(TestTool)))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        ToolRegisteredEventArgs? eventArgs = null;
        _toolRegistry.ToolRegistered += (sender, args) => eventArgs = args;

        // Act
        _toolRegistry.RegisterTool<TestTool>();

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal("test_tool", eventArgs.Registration.Metadata.Id);
    }

    [Fact]
    public void ToolUnregistered_Event_ShouldBeRaised()
    {
        // Arrange
        var validationResult = ValidationResult.Success();
        _mockValidator.Setup(v => v.ValidateToolType(typeof(TestTool)))
                     .Returns(validationResult);
        _mockValidator.Setup(v => v.ValidateMetadata(It.IsAny<ToolMetadata>()))
                     .Returns(validationResult);

        _toolRegistry.RegisterTool<TestTool>();

        ToolUnregisteredEventArgs? eventArgs = null;
        _toolRegistry.ToolUnregistered += (sender, args) => eventArgs = args;

        // Act
        _toolRegistry.UnregisterTool("test_tool");

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal("test_tool", eventArgs.ToolId);
    }
}

// Test tool implementations
public class TestTool : ITool
{
    public ToolMetadata Metadata { get; } = new()
    {
        Id = "test_tool",
        Name = "Test Tool",
        Description = "A test tool",
        Category = ToolCategory.System,
        Tags = ["test", "system"]
    };

    public Task InitializeAsync(Dictionary<string, object?>? configuration = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        return Task.FromResult(ToolResult.Success("Test result"));
    }

    public IList<string> ValidateParameters(Dictionary<string, object?> parameters)
    {
        return new List<string>();
    }

    public bool CanExecuteWithPermissions(ToolPermissions permissions)
    {
        return true;
    }

    public Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class TestFileSystemTool : ITool
{
    public ToolMetadata Metadata { get; } = new()
    {
        Id = "test_filesystem_tool",
        Name = "Test FileSystem Tool",
        Description = "A test filesystem tool",
        Category = ToolCategory.FileSystem,
        Tags = ["test", "filesystem"]
    };

    public Task InitializeAsync(Dictionary<string, object?>? configuration = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        return Task.FromResult(ToolResult.Success("Test filesystem result"));
    }

    public IList<string> ValidateParameters(Dictionary<string, object?> parameters)
    {
        return new List<string>();
    }

    public bool CanExecuteWithPermissions(ToolPermissions permissions)
    {
        return true;
    }

    public Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
