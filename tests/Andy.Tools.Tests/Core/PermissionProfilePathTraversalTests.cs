using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Andy.Tools.Tests.Core;

/// <summary>
/// Regression tests for issue #11: profile names were concatenated directly into a file path, so a
/// name like "../../evil" allowed writing/deleting JSON outside the profile directory.
/// </summary>
public class PermissionProfilePathTraversalTests : IDisposable
{
    private readonly PermissionProfileService _service;
    private readonly string _testDirectory;
    private readonly string _outsideDirectory;

    public PermissionProfilePathTraversalTests()
    {
        _service = new PermissionProfileService(new NullLogger<PermissionProfileService>());

        var root = Path.Combine(Path.GetTempPath(), "andy-ptrav-" + Guid.NewGuid().ToString("N")[..8]);
        _testDirectory = Path.Combine(root, "profiles");
        _outsideDirectory = Path.Combine(root, "outside");
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_outsideDirectory);

        var field = typeof(PermissionProfileService).GetField("_profileDirectory",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(_service, _testDirectory);
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_testDirectory);
        try { if (root != null && Directory.Exists(root)) { Directory.Delete(root, true); } } catch { }
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("../../escape")]
    [InlineData("sub/escape")]
    [InlineData("a/../../escape")]
    public async Task SaveProfile_WithTraversalName_ThrowsAndWritesNothingOutside(string evilName)
    {
        var before = Directory.GetFiles(_outsideDirectory).Length;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveProfileAsync(evilName, new ToolPermissions()));

        // Nothing escaped into the sibling directory or its parent.
        Assert.Equal(before, Directory.GetFiles(_outsideDirectory).Length);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_testDirectory)!, "escape.json")));
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("..")]
    public async Task LoadProfile_WithTraversalName_Throws(string evilName)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.LoadProfileAsync(evilName));
    }

    [Fact]
    public async Task DeleteProfile_WithTraversalName_Throws()
    {
        // Plant a file in the sibling directory; a traversal delete must not reach it.
        var victim = Path.Combine(_outsideDirectory, "victim.json");
        await File.WriteAllTextAsync(victim, "{}");

        var relative = Path.Combine("..", "outside", "victim");
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteProfileAsync(relative));

        Assert.True(File.Exists(victim));
    }

    [Fact]
    public async Task SaveAndLoad_WithValidName_StillWorks()
    {
        var perms = new ToolPermissions { FileSystemAccess = true, NetworkAccess = false };
        await _service.SaveProfileAsync("my-profile_1.beta", perms);

        var loaded = await _service.LoadProfileAsync("my-profile_1.beta");

        Assert.Equal("my-profile_1.beta", loaded.ProfileName);
        Assert.True(loaded.FileSystemAccess);
        Assert.False(loaded.NetworkAccess);
        Assert.True(File.Exists(Path.Combine(_testDirectory, "my-profile_1.beta.json")));
    }
}
