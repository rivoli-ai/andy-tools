using Andy.Tools.Core;
using Andy.Tools.Library.FileSystem;
using FluentAssertions;

namespace Andy.Tools.Tests.Library.FileSystem;

/// <summary>
/// Regression tests for issue #10: the destructive/disclosure file-system tools must enforce
/// <c>permissions.AllowedPaths</c>, and Delete/Move must not silently bypass the working-directory
/// boundary by catching the confinement error and retrying unconfined.
/// </summary>
public sealed class FileSystemAllowedPathsTests : IDisposable
{
    private readonly string _root;
    private readonly string _allowed;
    private readonly string _outside;

    public FileSystemAllowedPathsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "andytools_ap_" + Guid.NewGuid().ToString("N"));
        _allowed = Path.Combine(_root, "allowed");
        _outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(_allowed);
        Directory.CreateDirectory(_outside);
    }

    private static ToolExecutionContext ContextWithAllowed(string root, string allowed) => new()
    {
        WorkingDirectory = root,
        Permissions = new ToolPermissions
        {
            FileSystemAccess = true,
            AllowedPaths = new HashSet<string> { allowed }
        }
    };

    [Fact]
    public async Task Delete_OutsideAllowedPaths_IsRejected()
    {
        var victim = Path.Combine(_outside, "victim.txt");
        await File.WriteAllTextAsync(victim, "secret");

        var tool = new DeleteFileTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["target_path"] = victim },
            ContextWithAllowed(_root, _allowed));

        result.IsSuccessful.Should().BeFalse();
        File.Exists(victim).Should().BeTrue("a delete outside AllowedPaths must not occur");
    }

    [Fact]
    public async Task List_OutsideAllowedPaths_IsRejected()
    {
        var tool = new ListDirectoryTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["directory_path"] = _outside },
            ContextWithAllowed(_root, _allowed));

        result.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task Copy_DestinationOutsideAllowedPaths_IsRejected()
    {
        var source = Path.Combine(_allowed, "src.txt");
        await File.WriteAllTextAsync(source, "data");
        var dest = Path.Combine(_outside, "copy.txt");

        var tool = new CopyFileTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["source_path"] = source, ["destination_path"] = dest },
            ContextWithAllowed(_root, _allowed));

        result.IsSuccessful.Should().BeFalse();
        File.Exists(dest).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_EscapingWorkingDirectory_DoesNotSilentlySucceed()
    {
        // Working directory is _allowed; target escapes it via "..". Previously the tool caught the
        // confinement error and retried with no working directory, deleting the file anyway.
        var victim = Path.Combine(_outside, "escape.txt");
        await File.WriteAllTextAsync(victim, "secret");
        var escaping = Path.Combine("..", "outside", "escape.txt"); // relative escape from _allowed

        var tool = new DeleteFileTool();
        await tool.InitializeAsync();
        var context = new ToolExecutionContext
        {
            WorkingDirectory = _allowed,
            Permissions = new ToolPermissions { FileSystemAccess = true }
        };
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["target_path"] = escaping },
            context);

        result.IsSuccessful.Should().BeFalse();
        File.Exists(victim).Should().BeTrue("escaping the working directory must not delete the file");
    }

    [Fact]
    public async Task Delete_InsideAllowedPaths_Succeeds()
    {
        var target = Path.Combine(_allowed, "ok.txt");
        await File.WriteAllTextAsync(target, "x");

        var tool = new DeleteFileTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["target_path"] = target },
            ContextWithAllowed(_root, _allowed));

        result.IsSuccessful.Should().BeTrue();
        File.Exists(target).Should().BeFalse();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }
}
