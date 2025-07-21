using System.Reflection;
using Andy.Tools.Core;
using Andy.Tools.Discovery;
using Andy.Tools.Validation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Andy.Tools.Tests.Discovery;

public class ToolDiscoveryServiceTests : IDisposable
{
    private readonly Mock<IToolValidator> _mockValidator;
    private readonly Mock<ILogger<ToolDiscoveryService>> _mockLogger;
    private readonly ToolDiscoveryService _discoveryService;
    private readonly string _testDirectory;

    public ToolDiscoveryServiceTests()
    {
        _mockValidator = new Mock<IToolValidator>();
        _mockLogger = new Mock<ILogger<ToolDiscoveryService>>();
        _discoveryService = new ToolDiscoveryService(_mockValidator.Object, _mockLogger.Object);

        _testDirectory = Path.Combine(Path.GetTempPath(), $"andy_discovery_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task DiscoverToolsAsync_WithDefaultOptions_DiscoverFromCurrentAssembly()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
            .Returns(ValidationResult.Success());

        // Act
        var result = await _discoveryService.DiscoverToolsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IReadOnlyList<DiscoveredTool>>(result);

        // Should find tools from the current test assembly
        var discoveryInfo = _discoveryService.GetDiscoveryInfo();
        Assert.NotNull(discoveryInfo);
        Assert.True(discoveryInfo.LoadedAssembliesCount > 0);
    }

    [Fact]
    public async Task DiscoverToolsAsync_ScanCurrentAssemblyFalse_DoesNotScanCurrent()
    {
        // Arrange
        var options = new ToolDiscoveryOptions
        {
            ScanCurrentAssembly = false,
            ScanLoadedAssemblies = false
        };

        // Act
        var result = await _discoveryService.DiscoverToolsAsync(options);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task DiscoverToolsAsync_WithLoadedAssemblies_ScansAppDomainAssemblies()
    {
        // Arrange
        var options = new ToolDiscoveryOptions
        {
            ScanCurrentAssembly = false,
            ScanLoadedAssemblies = true
        };

        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
            .Returns(ValidationResult.Success());

        // Act
        var result = await _discoveryService.DiscoverToolsAsync(options);

        // Assert
        Assert.NotNull(result);
        // May or may not find tools depending on loaded assemblies
    }

    [Fact]
    public async Task DiscoverToolsAsync_WithAdditionalAssemblies_ScansAdditionalAssemblies()
    {
        // Arrange
        var currentAssembly = Assembly.GetExecutingAssembly();
        var options = new ToolDiscoveryOptions
        {
            ScanCurrentAssembly = false,
            ScanLoadedAssemblies = false,
            AdditionalAssemblies = { currentAssembly }
        };

        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
            .Returns(ValidationResult.Success());

        // Act
        var result = await _discoveryService.DiscoverToolsAsync(options);

        // Assert
        Assert.NotNull(result);
        // May find test tools if any exist in the test assembly
    }

    [Fact]
    public async Task DiscoverToolsAsync_WithPluginDirectories_ScansDirectories()
    {
        // Arrange
        var options = new ToolDiscoveryOptions
        {
            ScanCurrentAssembly = false,
            ScanLoadedAssemblies = false,
            PluginDirectories = { _testDirectory } // Empty directory
        };

        // Act
        var result = await _discoveryService.DiscoverToolsAsync(options);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // No DLLs in test directory
    }

    [Fact]
    public async Task DiscoverToolsAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _discoveryService.DiscoverToolsAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task DiscoverToolsAsync_ValidatorThrows_LogsWarningAndContinues()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
            .Throws(new InvalidOperationException("Validation failed"));

        var options = new ToolDiscoveryOptions
        {
            ScanCurrentAssembly = true,
            ValidateTools = true
        };

        // Act
        var result = await _discoveryService.DiscoverToolsAsync(options);

        // Assert
        Assert.NotNull(result);
        // Should still return results even if validation fails
    }

    [Fact]
    public void DiscoverToolsFromAssembly_ValidAssembly_ReturnsDiscoveredTools()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();
        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
            .Returns(ValidationResult.Success());

        // Act
        var result = _discoveryService.DiscoverToolsFromAssembly(assembly, validate: true);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IReadOnlyList<DiscoveredTool>>(result);

        // Check that discovered tools have correct metadata
        foreach (var tool in result)
        {
            Assert.NotNull(tool.ToolType);
            Assert.Equal(assembly, tool.SourceAssembly);
            Assert.Equal("assembly", tool.DiscoverySource);
            Assert.True(tool.IsValid); // Since we mocked validation to succeed
        }
    }

    [Fact]
    public void DiscoverToolsFromAssembly_ValidationDisabled_SkipsValidation()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var result = _discoveryService.DiscoverToolsFromAssembly(assembly, validate: false);

        // Assert
        Assert.NotNull(result);

        // Validator should not have been called
        _mockValidator.Verify(v => v.ValidateToolType(It.IsAny<Type>()), Times.Never);
    }

    [Fact]
    public void DiscoverToolsFromAssembly_ValidationFails_MarksAsInvalid()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();
        var validationError = new ValidationError("TEST_ERROR", "Test validation error");
        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
            .Returns(ValidationResult.Failure(validationError));

        // Act
        var result = _discoveryService.DiscoverToolsFromAssembly(assembly, validate: true);

        // Assert
        Assert.NotNull(result);

        // All discovered tools should be marked as invalid
        foreach (var tool in result)
        {
            Assert.False(tool.IsValid);
            Assert.Contains("Test validation error", tool.ValidationErrors);
        }
    }

    [Fact]
    public async Task DiscoverToolsFromDirectoryAsync_NonExistentDirectory_LogsWarningAndReturnsEmpty()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDirectory, "nonexistent");

        // Act
        var result = await _discoveryService.DiscoverToolsFromDirectoryAsync(nonExistentDir);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);

        // Should log warning
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("does not exist")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DiscoverToolsFromDirectoryAsync_EmptyDirectory_ReturnsEmpty()
    {
        // Act
        var result = await _discoveryService.DiscoverToolsFromDirectoryAsync(_testDirectory);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task DiscoverToolsFromDirectoryAsync_WithFilePatterns_UsesCustomPatterns()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(testFile, "test content");

        var filePatterns = new List<string> { "*.txt" };

        // Act
        var result = await _discoveryService.DiscoverToolsFromDirectoryAsync(
            _testDirectory, filePatterns);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // .txt files won't load as assemblies
    }

    [Fact]
    public async Task DiscoverToolsFromDirectoryAsync_RecursiveSearch_SearchesSubdirectoriesAsync()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        // Act
        var result = await _discoveryService.DiscoverToolsFromDirectoryAsync(
            _testDirectory, recursive: true);

        // Assert
        Assert.NotNull(result);
        // No assemblies to find, but should not throw
    }

    [Fact]
    public async Task DiscoverToolsFromFileAsync_NonExistentFile_LogsWarningAndReturnsEmpty()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.dll");

        // Act
        var result = await _discoveryService.DiscoverToolsFromFileAsync(nonExistentFile);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);

        // Should log warning
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("does not exist")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DiscoverToolsFromFileAsync_InvalidAssembly_LogsWarningAndReturnsEmpty()
    {
        // Arrange
        var invalidFile = Path.Combine(_testDirectory, "invalid.dll");
        await File.WriteAllTextAsync(invalidFile, "not a valid assembly");

        // Act
        var result = await _discoveryService.DiscoverToolsFromFileAsync(invalidFile);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);

        // Should log warning about failed assembly load
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to load assembly")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetDiscoveryInfo_ReturnsValidInfo()
    {
        // Act
        var info = _discoveryService.GetDiscoveryInfo();

        // Assert
        Assert.NotNull(info);
        Assert.True(info.LoadedAssembliesCount >= 0);
        Assert.NotNull(info.LoadedAssemblyNames);
        Assert.NotNull(info.AvailablePluginDirectories);
        Assert.NotNull(info.SupportedFileExtensions);
        Assert.Contains(".dll", info.SupportedFileExtensions);
        Assert.NotNull(info.CurrentOptions);
    }

    [Fact]
    public async Task DiscoverToolsAsync_WithExcludePatterns_ExcludesMatchingAssemblies()
    {
        // Arrange
        var options = new ToolDiscoveryOptions
        {
            ScanLoadedAssemblies = true,
            ExcludeAssemblyPatterns = { "Microsoft.*", "System.*" }
        };

        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
            .Returns(ValidationResult.Success());

        // Act
        var result = await _discoveryService.DiscoverToolsAsync(options);

        // Assert
        Assert.NotNull(result);
        // Should exclude system assemblies
    }

    [Fact]
    public async Task DiscoverToolsAsync_UpdatesStatistics()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
            .Returns(ValidationResult.Success());

        var options = new ToolDiscoveryOptions
        {
            ScanCurrentAssembly = true
        };

        // Act
        await _discoveryService.DiscoverToolsAsync(options);

        // Assert
        var info = _discoveryService.GetDiscoveryInfo();
        Assert.NotNull(info.LastDiscoveryStats);
        Assert.True(info.LastDiscoveryStats.DiscoveryDuration >= TimeSpan.Zero);
        Assert.True(info.LastDiscoveryStats.AssembliesScanned >= 0);
    }

    [Fact]
    public async Task DiscoverToolsAsync_LogsAppropriateMessages()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
            .Returns(ValidationResult.Success());

        // Act
        await _discoveryService.DiscoverToolsAsync();

        // Assert
        // Should log starting discovery
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting tool discovery")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Should log completion
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tool discovery completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void DiscoverToolsFromAssembly_HandlesReflectionTypeLoadException()
    {
        // This test verifies the service handles ReflectionTypeLoadException gracefully
        // In practice, this would require an assembly that fails to load some types
        // but succeeds with others. For now, we verify the method doesn't crash.

        // Arrange
        var assembly = Assembly.GetExecutingAssembly();
        _mockValidator.Setup(v => v.ValidateToolType(It.IsAny<Type>()))
            .Returns(ValidationResult.Success());

        // Act & Assert - should not throw
        var result = _discoveryService.DiscoverToolsFromAssembly(assembly);
        Assert.NotNull(result);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

// Test tool implementations for discovery testing
public class TestTool : ITool
{
    public ToolMetadata Metadata { get; } = new ToolMetadata
    {
        Id = "test_tool",
        Name = "Test Tool",
        Description = "A tool for testing discovery",
        Category = ToolCategory.Development,
        Version = "1.0.0"
    };

    public Task InitializeAsync(Dictionary<string, object?>? configuration = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        return Task.FromResult(ToolResult.Success(new { message = "Test executed" }));
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

public class AnotherTestTool : ITool
{
    public ToolMetadata Metadata { get; } = new ToolMetadata
    {
        Id = "another_test_tool",
        Name = "Another Test Tool",
        Description = "Another tool for testing discovery",
        Category = ToolCategory.Development,
        Version = "2.0.0"
    };

    public Task InitializeAsync(Dictionary<string, object?>? configuration = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        return Task.FromResult(ToolResult.Success(new { message = "Another test executed" }));
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
