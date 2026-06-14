using Andy.Tools.Core;
using Andy.Tools.Library.FileSystem;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.FileSystem;

public class ApplyPatchToolTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ApplyPatchTool _tool;
    private readonly ToolExecutionContext _context;

    public ApplyPatchToolTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"andy_patch_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _tool = new ApplyPatchTool();
        _tool.InitializeAsync().GetAwaiter().GetResult();
        _context = new ToolExecutionContext
        {
            WorkingDirectory = _testDirectory,
            Permissions = new ToolPermissions { AllowedPaths = [_testDirectory] }
        };
    }

    private async Task<ToolResult> RunAsync(string patch)
    {
        return await _tool.ExecuteAsync(new Dictionary<string, object?> { ["patch"] = patch }, _context);
    }

    [Fact]
    public void Metadata_HasCorrectConfiguration()
    {
        _tool.Metadata.Id.Should().Be("apply_patch");
        _tool.Metadata.Category.Should().Be(ToolCategory.FileSystem);
        _tool.Metadata.RequiredPermissions.Should()
            .Be(ToolPermissionFlags.FileSystemRead | ToolPermissionFlags.FileSystemWrite);
        _tool.Metadata.Parameters.Should().ContainSingle(p => p.Name == "patch" && p.Required);
    }

    [Fact]
    public async Task UpdateFile_HunkApplies()
    {
        var file = Path.Combine(_testDirectory, "code.txt");
        await File.WriteAllTextAsync(file, "line one\nline two\nline three\n");

        var patch =
            "*** Begin Patch\n"
            + "*** Update File: code.txt\n"
            + "@@\n"
            + " line one\n"
            + "-line two\n"
            + "+line TWO changed\n"
            + " line three\n"
            + "*** End Patch\n";

        var result = await RunAsync(patch);

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        (await File.ReadAllTextAsync(file)).Should().Be("line one\nline TWO changed\nline three\n");

        var data = result.Data as Dictionary<string, object?>;
        data!["files_changed"].As<List<string>>().Should().Contain("code.txt");
    }

    [Fact]
    public async Task UpdateFile_MultipleHunks_Apply()
    {
        var file = Path.Combine(_testDirectory, "multi.txt");
        await File.WriteAllTextAsync(file, "a\nb\nc\nd\ne\n");

        var patch =
            "*** Begin Patch\n"
            + "*** Update File: multi.txt\n"
            + "@@\n"
            + "-a\n"
            + "+A\n"
            + "@@\n"
            + "-e\n"
            + "+E\n"
            + "*** End Patch\n";

        var result = await RunAsync(patch);

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        (await File.ReadAllTextAsync(file)).Should().Be("A\nb\nc\nd\nE\n");
    }

    [Fact]
    public async Task AddFile_CreatesFile()
    {
        var patch =
            "*** Begin Patch\n"
            + "*** Add File: sub/new.txt\n"
            + "+hello\n"
            + "+world\n"
            + "*** End Patch\n";

        var result = await RunAsync(patch);

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var created = Path.Combine(_testDirectory, "sub", "new.txt");
        File.Exists(created).Should().BeTrue();
        (await File.ReadAllTextAsync(created)).Should().Be("hello\nworld");

        var data = result.Data as Dictionary<string, object?>;
        data!["files_added"].As<List<string>>().Should().Contain("sub/new.txt");
    }

    [Fact]
    public async Task AddFile_ExistingFile_Fails()
    {
        var file = Path.Combine(_testDirectory, "exists.txt");
        await File.WriteAllTextAsync(file, "already here");

        var patch =
            "*** Begin Patch\n"
            + "*** Add File: exists.txt\n"
            + "+new content\n"
            + "*** End Patch\n";

        var result = await RunAsync(patch);

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already exists");
        (await File.ReadAllTextAsync(file)).Should().Be("already here");
    }

    [Fact]
    public async Task DeleteFile_RemovesFile()
    {
        var file = Path.Combine(_testDirectory, "gone.txt");
        await File.WriteAllTextAsync(file, "delete me");

        var patch =
            "*** Begin Patch\n"
            + "*** Delete File: gone.txt\n"
            + "*** End Patch\n";

        var result = await RunAsync(patch);

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        File.Exists(file).Should().BeFalse();

        var data = result.Data as Dictionary<string, object?>;
        data!["files_deleted"].As<List<string>>().Should().Contain("gone.txt");
    }

    [Fact]
    public async Task DeleteFile_Missing_Fails()
    {
        var patch =
            "*** Begin Patch\n"
            + "*** Delete File: nope.txt\n"
            + "*** End Patch\n";

        var result = await RunAsync(patch);

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not exist");
    }

    [Fact]
    public async Task ContextMismatch_FailsAndLeavesAllFilesUntouched()
    {
        // First op (add) is valid; second op (update with bad context) must fail.
        // Atomicity: the add must NOT have happened either.
        var updateTarget = Path.Combine(_testDirectory, "target.txt");
        await File.WriteAllTextAsync(updateTarget, "real line\n");

        var patch =
            "*** Begin Patch\n"
            + "*** Add File: should_not_exist.txt\n"
            + "+nope\n"
            + "*** Update File: target.txt\n"
            + "@@\n"
            + " this context does not match\n"
            + "-real line\n"
            + "+changed\n"
            + "*** End Patch\n";

        var result = await RunAsync(patch);

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not apply");

        // No file touched.
        File.Exists(Path.Combine(_testDirectory, "should_not_exist.txt")).Should().BeFalse();
        (await File.ReadAllTextAsync(updateTarget)).Should().Be("real line\n");
    }

    [Fact]
    public async Task PathOutsideAllowed_Rejected()
    {
        // Reference a sibling directory outside the working directory via ../
        var patch =
            "*** Begin Patch\n"
            + "*** Add File: ../escape.txt\n"
            + "+pwned\n"
            + "*** End Patch\n";

        var result = await RunAsync(patch);

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().MatchRegex("not within allowed paths|outside the allowed");
        File.Exists(Path.Combine(_testDirectory, "..", "escape.txt")).Should().BeFalse();
    }

    [Fact]
    public async Task MalformedPatch_NoEnvelope_Fails()
    {
        var result = await RunAsync("just some text\nwith no envelope\n");

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Begin Patch");
    }

    [Fact]
    public async Task MixedOperations_AllApply()
    {
        var toUpdate = Path.Combine(_testDirectory, "u.txt");
        var toDelete = Path.Combine(_testDirectory, "d.txt");
        await File.WriteAllTextAsync(toUpdate, "keep\nold\n");
        await File.WriteAllTextAsync(toDelete, "bye");

        var patch =
            "*** Begin Patch\n"
            + "*** Update File: u.txt\n"
            + "@@\n"
            + " keep\n"
            + "-old\n"
            + "+new\n"
            + "*** Add File: a.txt\n"
            + "+added\n"
            + "*** Delete File: d.txt\n"
            + "*** End Patch\n";

        var result = await RunAsync(patch);

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        (await File.ReadAllTextAsync(toUpdate)).Should().Be("keep\nnew\n");
        File.Exists(Path.Combine(_testDirectory, "a.txt")).Should().BeTrue();
        File.Exists(toDelete).Should().BeFalse();
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
