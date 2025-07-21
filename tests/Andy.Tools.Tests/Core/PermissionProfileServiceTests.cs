using System;
using System.IO;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Andy.Tools.Tests.Core;

/// <summary>
/// Tests for the PermissionProfileService.
/// </summary>
public class PermissionProfileServiceTests : IDisposable
{
    private readonly PermissionProfileService _service;
    private readonly string _testDirectory;

    public PermissionProfileServiceTests()
    {
        var logger = new NullLogger<PermissionProfileService>();
        _service = new PermissionProfileService(logger);

        // Create a temporary directory for test profiles
        _testDirectory = Path.Combine(Path.GetTempPath(), "andy-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);

        // Use reflection to set the profile directory for testing
        var field = typeof(PermissionProfileService).GetField("_profileDirectory",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_service, _testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task GetCurrentPermissionsAsync_ShouldReturnDefaultPermissions()
    {
        // Act
        var permissions = await _service.GetCurrentPermissionsAsync();

        // Assert
        Assert.NotNull(permissions);
        Assert.Equal("default", permissions.ProfileName);
        Assert.True(permissions.FileSystemAccess);
        Assert.True(permissions.NetworkAccess);
        Assert.False(permissions.ProcessExecution);
        Assert.True(permissions.EnvironmentAccess);
    }

    [Fact]
    public async Task SaveAndLoadProfileAsync_ShouldPersistProfile()
    {
        // Arrange
        var testPermissions = new ToolPermissions
        {
            ProfileName = "test-profile",
            Description = "Test profile for unit tests",
            FileSystemAccess = false,
            NetworkAccess = true,
            ProcessExecution = true,
            EnvironmentAccess = false
        };

        // Act
        await _service.SaveProfileAsync("test-profile", testPermissions);
        var loadedPermissions = await _service.LoadProfileAsync("test-profile");

        // Assert
        Assert.NotNull(loadedPermissions);
        Assert.Equal("test-profile", loadedPermissions.ProfileName);
        Assert.Equal("Test profile for unit tests", loadedPermissions.Description);
        Assert.False(loadedPermissions.FileSystemAccess);
        Assert.True(loadedPermissions.NetworkAccess);
        Assert.True(loadedPermissions.ProcessExecution);
        Assert.False(loadedPermissions.EnvironmentAccess);
    }

    [Fact]
    public async Task ListProfilesAsync_ShouldReturnSavedProfiles()
    {
        // Arrange
        var profile1 = new ToolPermissions { ProfileName = "profile1" };
        var profile2 = new ToolPermissions { ProfileName = "profile2" };

        await _service.SaveProfileAsync("profile1", profile1);
        await _service.SaveProfileAsync("profile2", profile2);

        // Act
        var profiles = await _service.ListProfilesAsync();

        // Assert
        Assert.Contains("profile1", profiles);
        Assert.Contains("profile2", profiles);
    }

    [Fact]
    public async Task ProfileExistsAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var testPermissions = new ToolPermissions { ProfileName = "existing" };
        await _service.SaveProfileAsync("existing", testPermissions);

        // Act & Assert
        Assert.True(await _service.ProfileExistsAsync("existing"));
        Assert.False(await _service.ProfileExistsAsync("non-existing"));
        Assert.False(await _service.ProfileExistsAsync(""));
        Assert.False(await _service.ProfileExistsAsync(null));
    }

    [Fact]
    public async Task DeleteProfileAsync_ShouldRemoveProfile()
    {
        // Arrange
        var testPermissions = new ToolPermissions { ProfileName = "to-delete" };
        await _service.SaveProfileAsync("to-delete", testPermissions);
        Assert.True(await _service.ProfileExistsAsync("to-delete"));

        // Act
        await _service.DeleteProfileAsync("to-delete");

        // Assert
        Assert.False(await _service.ProfileExistsAsync("to-delete"));
    }

    [Fact]
    public async Task DeleteProfileAsync_ShouldThrowForDefaultProfile()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DeleteProfileAsync("default"));
    }

    [Fact]
    public async Task SetCurrentPermissionsAsync_ShouldUpdateCurrentPermissions()
    {
        // Arrange
        var newPermissions = new ToolPermissions
        {
            ProfileName = "custom",
            FileSystemAccess = false,
            NetworkAccess = false
        };

        // Act
        await _service.SetCurrentPermissionsAsync(newPermissions);
        var currentPermissions = await _service.GetCurrentPermissionsAsync();

        // Assert
        Assert.Equal("custom", currentPermissions.ProfileName);
        Assert.False(currentPermissions.FileSystemAccess);
        Assert.False(currentPermissions.NetworkAccess);
    }

    [Fact]
    public async Task LoadProfileAsync_ShouldThrowForNonExistentProfile()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.LoadProfileAsync("non-existent"));
    }

    [Fact]
    public async Task SaveProfileAsync_ShouldThrowForNullProfile()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SaveProfileAsync("test", null!));
    }

    [Fact]
    public async Task SaveProfileAsync_ShouldThrowForEmptyProfileName()
    {
        // Arrange
        var permissions = new ToolPermissions();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveProfileAsync("", permissions));
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveProfileAsync("   ", permissions));
    }
}
