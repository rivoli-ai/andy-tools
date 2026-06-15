using Andy.Tools.Core;
using Andy.Tools.Library.FileSystem;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.FileSystem;

public class EditFileToolTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly EditFileTool _tool;
    private readonly ToolExecutionContext _context;

    public EditFileToolTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"andy_edit_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _tool = new EditFileTool();
        _tool.InitializeAsync().GetAwaiter().GetResult();
        _context = new ToolExecutionContext
        {
            Permissions = new ToolPermissions { AllowedPaths = [_testDirectory] }
        };
    }

    private async Task<string> CreateFileAsync(string name, string content)
    {
        var path = Path.Combine(_testDirectory, name);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private Dictionary<string, object?> Params(string filePath, string oldString, string newString, bool? replaceAll = null, bool? createBackup = null)
    {
        var p = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["old_string"] = oldString,
            ["new_string"] = newString
        };
        if (replaceAll.HasValue)
        {
            p["replace_all"] = replaceAll.Value;
        }

        if (createBackup.HasValue)
        {
            p["create_backup"] = createBackup.Value;
        }

        return p;
    }

    [Fact]
    public async Task ExecuteAsync_UniqueReplace_Succeeds()
    {
        var path = await CreateFileAsync("a.txt", "hello world\nfoo bar\n");

        var result = await _tool.ExecuteAsync(Params(path, "foo bar", "baz qux", createBackup: false), _context);

        result.IsSuccessful.Should().BeTrue();
        (await File.ReadAllTextAsync(path)).Should().Be("hello world\nbaz qux\n");

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["replacements"].Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_StringNotFound_Fails()
    {
        var path = await CreateFileAsync("b.txt", "hello world\n");

        var result = await _tool.ExecuteAsync(Params(path, "not present", "x", createBackup: false), _context);

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
        (await File.ReadAllTextAsync(path)).Should().Be("hello world\n");
    }

    [Fact]
    public async Task ExecuteAsync_AmbiguousWithoutReplaceAll_Fails()
    {
        var path = await CreateFileAsync("c.txt", "dup\ndup\ndup\n");

        var result = await _tool.ExecuteAsync(Params(path, "dup", "x", createBackup: false), _context);

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("3 occurrences");
        // File must be untouched.
        (await File.ReadAllTextAsync(path)).Should().Be("dup\ndup\ndup\n");
    }

    [Fact]
    public async Task ExecuteAsync_ReplaceAll_ReplacesEveryOccurrence()
    {
        var path = await CreateFileAsync("d.txt", "dup\ndup\ndup\n");

        var result = await _tool.ExecuteAsync(Params(path, "dup", "x", replaceAll: true, createBackup: false), _context);

        result.IsSuccessful.Should().BeTrue();
        (await File.ReadAllTextAsync(path)).Should().Be("x\nx\nx\n");

        var data = result.Data as Dictionary<string, object?>;
        data!["replacements"].Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_PathOutsideAllowed_Fails()
    {
        // A path within the temp root but outside the AllowedPaths directory.
        var outsidePath = Path.Combine(Path.GetTempPath(), $"andy_outside_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(outsidePath, "data");

        try
        {
            var result = await _tool.ExecuteAsync(Params(outsidePath, "data", "x", createBackup: false), _context);

            result.IsSuccessful.Should().BeFalse();
            result.ErrorMessage.Should().Contain("not within allowed paths");
            (await File.ReadAllTextAsync(outsidePath)).Should().Be("data");
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_CreateBackup_CreatesBackupFile()
    {
        var path = await CreateFileAsync("e.txt", "original content here");

        var result = await _tool.ExecuteAsync(Params(path, "original", "modified", createBackup: true), _context);

        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        (data!["created_backup"] as bool?).Should().Be(true);

        var backupPath = data["backup_path"] as string;
        backupPath.Should().NotBeNull();
        File.Exists(backupPath!).Should().BeTrue();
        (await File.ReadAllTextAsync(backupPath!)).Should().Be("original content here");
        (await File.ReadAllTextAsync(path)).Should().Be("modified content here");
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_Fails()
    {
        var path = Path.Combine(_testDirectory, "missing.txt");

        var result = await _tool.ExecuteAsync(Params(path, "a", "b", createBackup: false), _context);

        result.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_IdenticalStrings_NoOpSuccess()
    {
        var path = await CreateFileAsync("f.txt", "same content");

        var result = await _tool.ExecuteAsync(Params(path, "same", "same", createBackup: true), _context);

        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data!["replacements"].Should().Be(0);
        // No backup should be created for a no-op.
        (data["created_backup"] as bool?).Should().Be(false);
        (await File.ReadAllTextAsync(path)).Should().Be("same content");
    }

    [Fact]
    public void Metadata_HasCorrectConfiguration()
    {
        _tool.Metadata.Id.Should().Be("edit_file");
        _tool.Metadata.Category.Should().Be(ToolCategory.FileSystem);
        _tool.Metadata.RequiredPermissions.Should().Be(ToolPermissionFlags.FileSystemRead | ToolPermissionFlags.FileSystemWrite);
        _tool.Metadata.Parameters.First(p => p.Name == "file_path").Required.Should().BeTrue();
        _tool.Metadata.Parameters.First(p => p.Name == "old_string").Required.Should().BeTrue();
        _tool.Metadata.Parameters.First(p => p.Name == "new_string").Required.Should().BeTrue();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
