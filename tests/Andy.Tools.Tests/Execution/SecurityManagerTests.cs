using Andy.Tools.Core;
using Andy.Tools.Execution;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Andy.Tools.Tests.Execution;

public class SecurityManagerTests
{
    private readonly Mock<ILogger<SecurityManager>> _mockLogger;
    private readonly SecurityManager _securityManager;
    private readonly ToolMetadata _testToolMetadata;
    private readonly ToolPermissions _defaultPermissions;

    public SecurityManagerTests()
    {
        _mockLogger = new Mock<ILogger<SecurityManager>>();
        _securityManager = new SecurityManager(_mockLogger.Object);

        _testToolMetadata = new ToolMetadata
        {
            Id = "test-tool",
            Name = "Test Tool",
            Description = "Test tool for security tests",
            Category = ToolCategory.Development,
            RequiredCapabilities = ToolCapability.None
        };

        _defaultPermissions = new ToolPermissions
        {
            FileSystemAccess = true,
            NetworkAccess = true,
            ProcessExecution = true,
            EnvironmentAccess = true,
            AllowedPaths = new HashSet<string>(),
            AllowedHosts = new HashSet<string>(),
            CustomPermissions = new Dictionary<string, object?>()
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidLogger_ShouldInitializeSuccessfully()
    {
        // Arrange & Act & Assert
        _securityManager.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SecurityManager(null!));
    }

    #endregion

    #region ValidateExecution Tests

    [Fact]
    public void ValidateExecution_WithAllPermissionsGranted_ShouldReturnNoViolations()
    {
        // Arrange
        _testToolMetadata.RequiredCapabilities = ToolCapability.FileSystem | ToolCapability.Network;

        // Act
        var violations = _securityManager.ValidateExecution(_testToolMetadata, _defaultPermissions);

        // Assert
        violations.Should().BeEmpty();
    }

    [Fact]
    public void ValidateExecution_WithFileSystemCapabilityButNoPermission_ShouldReturnViolation()
    {
        // Arrange
        _testToolMetadata.RequiredCapabilities = ToolCapability.FileSystem;
        var permissions = new ToolPermissions { FileSystemAccess = false };

        // Act
        var violations = _securityManager.ValidateExecution(_testToolMetadata, permissions);

        // Assert
        violations.Should().NotBeEmpty();
        violations.Should().Contain("Tool requires file system access but it is not granted");
    }

    [Fact]
    public void ValidateExecution_WithNetworkCapabilityButNoPermission_ShouldReturnViolation()
    {
        // Arrange
        _testToolMetadata.RequiredCapabilities = ToolCapability.Network;
        var permissions = new ToolPermissions { NetworkAccess = false };

        // Act
        var violations = _securityManager.ValidateExecution(_testToolMetadata, permissions);

        // Assert
        violations.Should().NotBeEmpty();
        violations.Should().Contain("Tool requires network access but it is not granted");
    }

    [Fact]
    public void ValidateExecution_WithProcessExecutionCapabilityButNoPermission_ShouldReturnViolation()
    {
        // Arrange
        _testToolMetadata.RequiredCapabilities = ToolCapability.ProcessExecution;
        var permissions = new ToolPermissions { ProcessExecution = false };

        // Act
        var violations = _securityManager.ValidateExecution(_testToolMetadata, permissions);

        // Assert
        violations.Should().NotBeEmpty();
        violations.Should().Contain("Tool requires process execution but it is not granted");
    }

    [Fact]
    public void ValidateExecution_WithEnvironmentCapabilityButNoPermission_ShouldReturnViolation()
    {
        // Arrange
        _testToolMetadata.RequiredCapabilities = ToolCapability.Environment;
        var permissions = new ToolPermissions { EnvironmentAccess = false };

        // Act
        var violations = _securityManager.ValidateExecution(_testToolMetadata, permissions);

        // Assert
        violations.Should().NotBeEmpty();
        violations.Should().Contain("Tool requires environment access but it is not granted");
    }

    [Fact]
    public void ValidateExecution_WithDestructiveCapabilityButNoExplicitPermission_ShouldReturnViolation()
    {
        // Arrange
        _testToolMetadata.RequiredCapabilities = ToolCapability.Destructive;

        // Act
        var violations = _securityManager.ValidateExecution(_testToolMetadata, _defaultPermissions);

        // Assert
        violations.Should().NotBeEmpty();
        violations.Should().Contain("Tool performs destructive operations but explicit permission is not granted");
    }

    [Fact]
    public void ValidateExecution_WithDestructiveCapabilityAndExplicitPermission_ShouldReturnNoViolations()
    {
        // Arrange
        _testToolMetadata.RequiredCapabilities = ToolCapability.Destructive;
        _defaultPermissions.CustomPermissions["allow_destructive"] = true;

        // Act
        var violations = _securityManager.ValidateExecution(_testToolMetadata, _defaultPermissions);

        // Assert
        violations.Should().BeEmpty();
    }

    [Fact]
    public void ValidateExecution_WithElevatedCapabilityButNoExplicitPermission_ShouldReturnViolation()
    {
        // Arrange
        _testToolMetadata.RequiredCapabilities = ToolCapability.Elevated;

        // Act
        var violations = _securityManager.ValidateExecution(_testToolMetadata, _defaultPermissions);

        // Assert
        violations.Should().NotBeEmpty();
        violations.Should().Contain("Tool requires elevated privileges but explicit permission is not granted");
    }

    [Fact]
    public void ValidateExecution_WithElevatedCapabilityAndExplicitPermission_ShouldReturnNoViolations()
    {
        // Arrange
        _testToolMetadata.RequiredCapabilities = ToolCapability.Elevated;
        _defaultPermissions.CustomPermissions["allow_elevated"] = true;

        // Act
        var violations = _securityManager.ValidateExecution(_testToolMetadata, _defaultPermissions);

        // Assert
        violations.Should().BeEmpty();
    }

    [Fact]
    public void ValidateExecution_WithMultipleViolations_ShouldReturnAllViolations()
    {
        // Arrange
        _testToolMetadata.RequiredCapabilities = ToolCapability.FileSystem | ToolCapability.Network | ToolCapability.Destructive;
        var permissions = new ToolPermissions
        {
            FileSystemAccess = false,
            NetworkAccess = false,
            CustomPermissions = new Dictionary<string, object?>()
        };

        // Act
        var violations = _securityManager.ValidateExecution(_testToolMetadata, permissions);

        // Assert
        violations.Should().HaveCount(3);
        violations.Should().Contain("Tool requires file system access but it is not granted");
        violations.Should().Contain("Tool requires network access but it is not granted");
        violations.Should().Contain("Tool performs destructive operations but explicit permission is not granted");
    }

    #endregion

    #region IsFileAccessAllowed Tests

    [Fact]
    public void IsFileAccessAllowed_WithNoFileSystemPermission_ShouldReturnFalse()
    {
        // Arrange
        var permissions = new ToolPermissions { FileSystemAccess = false };

        // Act
        var result = _securityManager.IsFileAccessAllowed("/test/file.txt", permissions, FileAccessType.Read);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsFileAccessAllowed_WithValidPath_ShouldReturnTrue()
    {
        // Arrange
        var tempPath = Path.GetTempPath();

        // Act
        var result = _securityManager.IsFileAccessAllowed(Path.Combine(tempPath, "test.txt"), _defaultPermissions, FileAccessType.Read);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsFileAccessAllowed_WithInvalidPath_ShouldReturnFalse()
    {
        // Act
        var result = _securityManager.IsFileAccessAllowed("invalid<>path", _defaultPermissions, FileAccessType.Read);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsFileAccessAllowed_WithAllowedPathList_ShouldRespectAllowedPaths()
    {
        // Arrange
        var tempPath = Path.GetTempPath();
        var permissions = new ToolPermissions
        {
            FileSystemAccess = true,
            AllowedPaths = new HashSet<string> { tempPath }
        };

        // Act
        var allowedResult = _securityManager.IsFileAccessAllowed(Path.Combine(tempPath, "test.txt"), permissions, FileAccessType.Read);
        var deniedResult = _securityManager.IsFileAccessAllowed("/different/path/test.txt", permissions, FileAccessType.Read);

        // Assert
        allowedResult.Should().BeTrue();
        deniedResult.Should().BeFalse();
    }

    [Fact]
    public void IsFileAccessAllowed_WithSystemDirectoryWrite_ShouldRequireExplicitPermission()
    {
        // Arrange
        var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (string.IsNullOrEmpty(systemPath))
        {
            return; // Skip test if system path is not available
        }

        var testFile = Path.Combine(systemPath, "test.txt");

        // Act
        var resultWithoutPermission = _securityManager.IsFileAccessAllowed(testFile, _defaultPermissions, FileAccessType.Write);

        _defaultPermissions.CustomPermissions["allow_system_write"] = true;
        var resultWithPermission = _securityManager.IsFileAccessAllowed(testFile, _defaultPermissions, FileAccessType.Write);

        // Assert
        resultWithoutPermission.Should().BeFalse();
        resultWithPermission.Should().BeTrue();
    }

    [Fact]
    public void IsFileAccessAllowed_WithSystemDirectoryRead_ShouldBeAllowed()
    {
        // Arrange
        var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (string.IsNullOrEmpty(systemPath))
        {
            return; // Skip test if system path is not available
        }

        var testFile = Path.Combine(systemPath, "test.txt");

        // Act
        var result = _securityManager.IsFileAccessAllowed(testFile, _defaultPermissions, FileAccessType.Read);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsFileAccessAllowed_WithExecutableFileExecution_ShouldRequireExplicitPermission()
    {
        // Arrange
        var executablePath = "/test/app.exe";

        // Act
        var resultWithoutPermission = _securityManager.IsFileAccessAllowed(executablePath, _defaultPermissions, FileAccessType.Execute);

        _defaultPermissions.CustomPermissions["allow_executable"] = true;
        var resultWithPermission = _securityManager.IsFileAccessAllowed(executablePath, _defaultPermissions, FileAccessType.Execute);

        // Assert
        resultWithoutPermission.Should().BeFalse();
        resultWithPermission.Should().BeTrue();
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".bat")]
    [InlineData(".cmd")]
    [InlineData(".ps1")]
    [InlineData(".sh")]
    [InlineData(".py")]
    [InlineData(".js")]
    [InlineData(".vbs")]
    public void IsFileAccessAllowed_WithBlockedExecutableExtensions_ShouldRequirePermission(string extension)
    {
        // Arrange
        var executablePath = $"/test/file{extension}";

        // Act
        var result = _securityManager.IsFileAccessAllowed(executablePath, _defaultPermissions, FileAccessType.Execute);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsNetworkAccessAllowed Tests

    [Fact]
    public void IsNetworkAccessAllowed_WithNoNetworkPermission_ShouldReturnFalse()
    {
        // Arrange
        var permissions = new ToolPermissions { NetworkAccess = false };

        // Act
        var result = _securityManager.IsNetworkAccessAllowed("example.com", permissions);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsNetworkAccessAllowed_WithAllowedPublicHost_ShouldReturnTrue()
    {
        // Act
        var result = _securityManager.IsNetworkAccessAllowed("example.com", _defaultPermissions);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void IsNetworkAccessAllowed_WithBlockedLocalhost_ShouldRequireExplicitPermission(string host)
    {
        // Act
        var resultWithoutPermission = _securityManager.IsNetworkAccessAllowed(host, _defaultPermissions);

        _defaultPermissions.CustomPermissions["allow_localhost"] = true;
        var resultWithPermission = _securityManager.IsNetworkAccessAllowed(host, _defaultPermissions);

        // Assert
        resultWithoutPermission.Should().BeFalse();
        resultWithPermission.Should().BeTrue();
    }

    [Fact]
    public void IsNetworkAccessAllowed_WithAllowedHostsList_ShouldRespectAllowedHosts()
    {
        // Arrange
        var permissions = new ToolPermissions
        {
            NetworkAccess = true,
            AllowedHosts = new HashSet<string> { "allowed.com", "*.trusted.com" }
        };

        // Act
        var allowedExactResult = _securityManager.IsNetworkAccessAllowed("allowed.com", permissions);
        var allowedWildcardResult = _securityManager.IsNetworkAccessAllowed("api.trusted.com", permissions);
        var deniedResult = _securityManager.IsNetworkAccessAllowed("blocked.com", permissions);

        // Assert
        allowedExactResult.Should().BeTrue();
        allowedWildcardResult.Should().BeTrue();
        deniedResult.Should().BeFalse();
    }

    [Theory]
    [InlineData("10.0.0.1")]     // 10.0.0.0/8
    [InlineData("172.16.0.1")]   // 172.16.0.0/12
    [InlineData("172.31.255.1")] // 172.16.0.0/12
    [InlineData("192.168.1.1")]  // 192.168.0.0/16
    public void IsNetworkAccessAllowed_WithPrivateIpAddresses_ShouldRequireExplicitPermission(string ipAddress)
    {
        // Act
        var resultWithoutPermission = _securityManager.IsNetworkAccessAllowed(ipAddress, _defaultPermissions);

        _defaultPermissions.CustomPermissions["allow_private_networks"] = true;
        var resultWithPermission = _securityManager.IsNetworkAccessAllowed(ipAddress, _defaultPermissions);

        // Assert
        resultWithoutPermission.Should().BeFalse();
        resultWithPermission.Should().BeTrue();
    }

    [Theory]
    [InlineData("8.8.8.8")]      // Public DNS
    [InlineData("1.1.1.1")]      // Cloudflare DNS
    [InlineData("208.67.222.222")] // OpenDNS
    public void IsNetworkAccessAllowed_WithPublicIpAddresses_ShouldBeAllowed(string ipAddress)
    {
        // Act
        var result = _securityManager.IsNetworkAccessAllowed(ipAddress, _defaultPermissions);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsProcessExecutionAllowed Tests

    [Fact]
    public void IsProcessExecutionAllowed_WithNoProcessExecutionPermission_ShouldReturnFalse()
    {
        // Arrange
        var permissions = new ToolPermissions { ProcessExecution = false };

        // Act
        var result = _securityManager.IsProcessExecutionAllowed("notepad.exe", permissions);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsProcessExecutionAllowed_WithSafeProcess_ShouldReturnTrue()
    {
        // Act
        var result = _securityManager.IsProcessExecutionAllowed("notepad.exe", _defaultPermissions);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("cmd.exe")]
    [InlineData("powershell.exe")]
    [InlineData("bash")]
    [InlineData("sh")]
    [InlineData("python.exe")]
    [InlineData("node.exe")]
    [InlineData("ruby.exe")]
    public void IsProcessExecutionAllowed_WithDangerousProcesses_ShouldRequireExplicitPermission(string processName)
    {
        // Act
        var resultWithoutPermission = _securityManager.IsProcessExecutionAllowed(processName, _defaultPermissions);

        _defaultPermissions.CustomPermissions["allow_dangerous_processes"] = true;
        var resultWithPermission = _securityManager.IsProcessExecutionAllowed(processName, _defaultPermissions);

        // Assert
        resultWithoutPermission.Should().BeFalse();
        resultWithPermission.Should().BeTrue();
    }

    #endregion

    #region Security Violation Management Tests

    [Fact]
    public void RecordViolation_ShouldAddViolationToCollection()
    {
        // Act
        _securityManager.RecordViolation("test-tool", "correlation-1", "Test violation", SecurityViolationSeverity.High);

        // Assert
        var violations = _securityManager.GetViolations("correlation-1");
        violations.Should().NotBeEmpty();
        violations.Should().HaveCount(1);
        violations[0].ToolId.Should().Be("test-tool");
        violations[0].Description.Should().Be("Test violation");
        violations[0].Severity.Should().Be(SecurityViolationSeverity.High);
    }

    [Fact]
    public void GetViolations_WithSpecificCorrelationId_ShouldReturnMatchingViolations()
    {
        // Arrange
        _securityManager.RecordViolation("tool1", "correlation-1", "Violation 1", SecurityViolationSeverity.High);
        _securityManager.RecordViolation("tool2", "correlation-2", "Violation 2", SecurityViolationSeverity.Medium);
        _securityManager.RecordViolation("tool3", "correlation-1", "Violation 3", SecurityViolationSeverity.Low);

        // Act
        var violations = _securityManager.GetViolations("correlation-1");

        // Assert
        violations.Should().HaveCount(2);
        violations.Should().Contain(v => v.Description == "Violation 1");
        violations.Should().Contain(v => v.Description == "Violation 3");
    }

    [Fact]
    public void GetViolations_WithNonExistentCorrelationId_ShouldReturnEmpty()
    {
        // Act
        var violations = _securityManager.GetViolations("non-existent");

        // Assert
        violations.Should().BeEmpty();
    }

    [Fact]
    public void GetAllViolations_ShouldReturnAllViolations()
    {
        // Arrange
        _securityManager.RecordViolation("tool1", "correlation-1", "Violation 1", SecurityViolationSeverity.High);
        _securityManager.RecordViolation("tool2", "correlation-2", "Violation 2", SecurityViolationSeverity.Medium);

        // Act
        var violations = _securityManager.GetAllViolations();

        // Assert
        violations.Should().HaveCount(2);
    }

    [Fact]
    public void GetAllViolations_WithSinceFilter_ShouldReturnRecentViolations()
    {
        // Arrange
        var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-1);

        _securityManager.RecordViolation("tool1", "correlation-1", "Violation 1", SecurityViolationSeverity.High);
        Thread.Sleep(100); // Ensure different timestamps
        _securityManager.RecordViolation("tool2", "correlation-2", "Violation 2", SecurityViolationSeverity.Medium);

        // Act
        var violations = _securityManager.GetAllViolations(cutoffTime);

        // Assert
        violations.Should().HaveCount(2); // Both should be after cutoff
    }

    [Fact]
    public void ClearOldViolations_ShouldRemoveOldViolationsAndReturnCount()
    {
        // Arrange
        _securityManager.RecordViolation("tool1", "correlation-1", "Old Violation", SecurityViolationSeverity.High);
        
        // Ensure the violation is definitely old by waiting
        Thread.Sleep(10);

        // Act - Clear violations older than 5ms
        var clearedCount = _securityManager.ClearOldViolations(TimeSpan.FromMilliseconds(5));
        var remainingViolations = _securityManager.GetAllViolations();

        // Assert
        clearedCount.Should().BeGreaterThan(0);
        remainingViolations.Should().BeEmpty();
    }

    [Fact]
    public void ClearOldViolations_ShouldKeepRecentViolations()
    {
        // Arrange
        _securityManager.RecordViolation("tool1", "correlation-1", "Recent Violation", SecurityViolationSeverity.High);

        // Act
        var clearedCount = _securityManager.ClearOldViolations(TimeSpan.FromDays(1)); // Keep violations from last day
        var remainingViolations = _securityManager.GetAllViolations();

        // Assert
        clearedCount.Should().Be(0);
        remainingViolations.Should().HaveCount(1);
        remainingViolations[0].Description.Should().Be("Recent Violation");
    }

    #endregion
}
