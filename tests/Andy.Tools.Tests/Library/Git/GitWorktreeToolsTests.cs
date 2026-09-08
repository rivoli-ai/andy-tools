using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Andy.Tools.Library;
using Andy.Tools.Library.Git;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.Git;

public class GitWorktreeToolsTests : IDisposable
{
    private readonly string _repoDir;
    private readonly string _worktreeRoot;
    private readonly bool _gitAvailable;
    private readonly string _defaultBranch = string.Empty;

    public GitWorktreeToolsTests()
    {
        _gitAvailable = IsGitAvailable();
        var suffix = Guid.NewGuid().ToString("N");
        _repoDir = Path.Combine(Path.GetTempPath(), "andy-git-worktree-repo-" + suffix);
        _worktreeRoot = Path.Combine(Path.GetTempPath(), "andy-git-worktree-lanes-" + suffix);
        Directory.CreateDirectory(_repoDir);
        Directory.CreateDirectory(_worktreeRoot);

        if (_gitAvailable)
        {
            RunGit("init -q");
            RunGit("config user.name \"Test User\"");
            RunGit("config user.email \"test@example.com\"");
            RunGit("config commit.gpgsign false");
            File.WriteAllText(Path.Combine(_repoDir, "file.txt"), "hello world\n");
            RunGit("add file.txt");
            RunGit("commit -q -m \"Initial commit\"");
            _defaultBranch = RunGit("branch --show-current").Trim();
        }
    }

    public void Dispose()
    {
        foreach (var dir in new[] { _worktreeRoot, _repoDir })
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        GC.SuppressFinalize(this);
    }

    private static bool IsGitAvailable()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private string RunGit(string arguments, string? workingDirectory = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? _repoDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {arguments} failed: {stderr}");
        }

        return stdout;
    }

    private ToolExecutionContext CreateContext() => new()
    {
        WorkingDirectory = _repoDir,
        Permissions = new ToolPermissions { ProcessExecution = true }
    };

    private static async Task<ToolResult> ExecuteAsync(ToolBase tool, Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        await tool.InitializeAsync();
        return await tool.ExecuteAsync(parameters, context);
    }

    private async Task<string> AddWorktreeAsync(string name, string branch)
    {
        var path = Path.Combine(_worktreeRoot, name);
        var result = await ExecuteAsync(new GitWorktreeAddTool(), new Dictionary<string, object?>
        {
            ["path"] = path,
            ["branch"] = branch
        }, CreateContext());
        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        return path;
    }

    private static List<Dictionary<string, object?>> Items(ToolResult result)
    {
        var data = (Dictionary<string, object?>)result.Data!;
        var items = data["items"] as List<Dictionary<string, object?>>;
        items.Should().NotBeNull();
        return items!;
    }

    [Fact]
    public void Metadata_DeclaresWriteAndConfirmationFlags()
    {
        var add = new GitWorktreeAddTool().Metadata;
        add.RequiredPermissions.Should().Be(ToolPermissionFlags.ProcessExecution | ToolPermissionFlags.FileSystemWrite);

        var remove = new GitWorktreeRemoveTool().Metadata;
        remove.RequiredPermissions.Should().Be(ToolPermissionFlags.ProcessExecution | ToolPermissionFlags.FileSystemWrite);
        remove.RequiredCapabilities.Should().Be(ToolCapability.Destructive);
        remove.RequiresConfirmation.Should().BeTrue();

        var list = new GitWorktreeListTool().Metadata;
        list.RequiredPermissions.Should().Be(ToolPermissionFlags.ProcessExecution);

        var prune = new GitWorktreePruneTool().Metadata;
        prune.RequiredPermissions.Should().Be(ToolPermissionFlags.ProcessExecution | ToolPermissionFlags.FileSystemWrite);
    }

    [Fact]
    public async Task List_FreshRepo_ReturnsOnlyMainWorktree()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var result = await ExecuteAsync(new GitWorktreeListTool(), [], CreateContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var items = Items(result);
        items.Should().HaveCount(1);
        items[0]["is_main"].Should().Be(true);
        items[0]["branch"].Should().Be(_defaultBranch);
        items[0]["head"].Should().BeOfType<string>().Which.Should().HaveLength(40);
    }

    [Fact]
    public async Task Add_NewBranch_CreatesWorktreeVisibleInList()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var path = Path.Combine(_worktreeRoot, "feature-a");
        var result = await ExecuteAsync(new GitWorktreeAddTool(), new Dictionary<string, object?>
        {
            ["path"] = path,
            ["branch"] = "feature/a"
        }, CreateContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var data = (Dictionary<string, object?>)result.Data!;
        data["branch"].Should().Be("feature/a");
        data["is_detached"].Should().Be(false);
        Directory.Exists(path).Should().BeTrue();
        File.Exists(Path.Combine(path, "file.txt")).Should().BeTrue();

        var list = await ExecuteAsync(new GitWorktreeListTool(), [], CreateContext());
        var items = Items(list);
        items.Should().HaveCount(2);
        items[1]["branch"].Should().Be("feature/a");
        items[1]["is_main"].Should().Be(false);
    }

    [Fact]
    public async Task Add_Detached_HasNoBranch()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var path = Path.Combine(_worktreeRoot, "detached");
        var result = await ExecuteAsync(new GitWorktreeAddTool(), new Dictionary<string, object?>
        {
            ["path"] = path,
            ["detach"] = true
        }, CreateContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var data = (Dictionary<string, object?>)result.Data!;
        data["is_detached"].Should().Be(true);
        data["branch"].Should().BeNull();
    }

    [Fact]
    public async Task Add_RelativePath_ResolvesAgainstWorkingDirectory()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var result = await ExecuteAsync(new GitWorktreeAddTool(), new Dictionary<string, object?>
        {
            ["path"] = Path.Combine("..", Path.GetFileName(_worktreeRoot), "relative-wt"),
            ["branch"] = "feature/relative"
        }, CreateContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Directory.Exists(Path.Combine(_worktreeRoot, "relative-wt")).Should().BeTrue();
    }

    [Fact]
    public async Task Add_BranchAlreadyCheckedOut_FailsWithoutForce()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var result = await ExecuteAsync(new GitWorktreeAddTool(), new Dictionary<string, object?>
        {
            ["path"] = Path.Combine(_worktreeRoot, "duplicate"),
            ["commit_ish"] = _defaultBranch
        }, CreateContext());

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to add worktree");
    }

    [Fact]
    public async Task Add_DetachWithBranch_IsRejected()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var result = await ExecuteAsync(new GitWorktreeAddTool(), new Dictionary<string, object?>
        {
            ["path"] = Path.Combine(_worktreeRoot, "invalid"),
            ["branch"] = "feature/x",
            ["detach"] = true
        }, CreateContext());

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("detach");
    }

    [Fact]
    public async Task Add_MissingPath_FailsValidation()
    {
        var result = await ExecuteAsync(new GitWorktreeAddTool(), [], CreateContext());

        result.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task Remove_CleanWorktree_Succeeds()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var path = await AddWorktreeAsync("removable", "feature/removable");

        var result = await ExecuteAsync(new GitWorktreeRemoveTool(), new Dictionary<string, object?>
        {
            ["path"] = path
        }, CreateContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Directory.Exists(path).Should().BeFalse();

        var list = await ExecuteAsync(new GitWorktreeListTool(), [], CreateContext());
        Items(list).Should().HaveCount(1);
    }

    [Fact]
    public async Task Remove_DirtyWorktree_RequiresForce()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var path = await AddWorktreeAsync("dirty", "feature/dirty");
        File.WriteAllText(Path.Combine(path, "uncommitted.txt"), "dirty\n");

        var withoutForce = await ExecuteAsync(new GitWorktreeRemoveTool(), new Dictionary<string, object?>
        {
            ["path"] = path
        }, CreateContext());
        withoutForce.IsSuccessful.Should().BeFalse();
        Directory.Exists(path).Should().BeTrue();

        var withForce = await ExecuteAsync(new GitWorktreeRemoveTool(), new Dictionary<string, object?>
        {
            ["path"] = path,
            ["force"] = true
        }, CreateContext());
        withForce.IsSuccessful.Should().BeTrue(withForce.ErrorMessage);
        Directory.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task Remove_MainWorktree_Fails()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var result = await ExecuteAsync(new GitWorktreeRemoveTool(), new Dictionary<string, object?>
        {
            ["path"] = _repoDir
        }, CreateContext());

        result.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task Prune_AfterDirectoryDeleted_CleansRegistration()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var path = await AddWorktreeAsync("stale", "feature/stale");
        Directory.Delete(path, recursive: true);

        var dryRun = await ExecuteAsync(new GitWorktreePruneTool(), new Dictionary<string, object?>
        {
            ["dry_run"] = true
        }, CreateContext());
        dryRun.IsSuccessful.Should().BeTrue(dryRun.ErrorMessage);
        var dryData = (Dictionary<string, object?>)dryRun.Data!;
        ((List<string>)dryData["pruned"]!).Should().NotBeEmpty();
        RunGit("worktree list --porcelain").Should().Contain("stale");

        var prune = await ExecuteAsync(new GitWorktreePruneTool(), [], CreateContext());
        prune.IsSuccessful.Should().BeTrue(prune.ErrorMessage);
        RunGit("worktree list --porcelain").Should().NotContain("stale");

        var list = await ExecuteAsync(new GitWorktreeListTool(), [], CreateContext());
        Items(list).Should().HaveCount(1);
    }

    [Fact]
    public async Task Tools_OutsideRepository_Fail()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var nonRepo = Path.Combine(Path.GetTempPath(), "andy-worktree-nonrepo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(nonRepo);
        try
        {
            var context = new ToolExecutionContext
            {
                WorkingDirectory = nonRepo,
                Permissions = new ToolPermissions { ProcessExecution = true }
            };
            var result = await ExecuteAsync(new GitWorktreeListTool(), [], context);

            result.IsSuccessful.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Not in a git repository");
        }
        finally
        {
            Directory.Delete(nonRepo, recursive: true);
        }
    }
}
