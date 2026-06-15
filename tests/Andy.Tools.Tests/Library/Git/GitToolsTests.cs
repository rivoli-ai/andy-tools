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

public class GitToolsTests : IDisposable
{
    private readonly string _repoDir;
    private readonly bool _gitAvailable;

    public GitToolsTests()
    {
        _gitAvailable = IsGitAvailable();
        _repoDir = Path.Combine(Path.GetTempPath(), "andy-git-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_repoDir);

        if (_gitAvailable)
        {
            RunGit("init -q");
            RunGit("config user.name \"Test User\"");
            RunGit("config user.email \"test@example.com\"");
            RunGit("config commit.gpgsign false");
            File.WriteAllText(Path.Combine(_repoDir, "file.txt"), "hello world\n");
            RunGit("add file.txt");
            RunGit("commit -q -m \"Initial commit\"");
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_repoDir))
            {
                Directory.Delete(_repoDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
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

    private void RunGit(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _repoDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {arguments} failed: {process.StandardError.ReadToEnd()}");
        }
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

    [Fact]
    public async Task GitStatus_ReportsUntrackedFile()
    {
        if (!_gitAvailable)
        {
            return;
        }

        File.WriteAllText(Path.Combine(_repoDir, "untracked.txt"), "new\n");

        var tool = new GitStatusTool();
        var result = await ExecuteAsync(tool, [], CreateContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["branch"].Should().NotBeNull();
        var untracked = data["untracked"] as List<string>;
        untracked.Should().NotBeNull();
        untracked!.Should().Contain("untracked.txt");
        ((bool)data["is_clean"]!).Should().BeFalse();
    }

    [Fact]
    public async Task GitStatus_CleanRepo_IsClean()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var tool = new GitStatusTool();
        var result = await ExecuteAsync(tool, [], CreateContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var data = (Dictionary<string, object?>)result.Data!;
        ((bool)data["is_clean"]!).Should().BeTrue();
    }

    [Fact]
    public async Task GitLog_ReturnsSingleCommit()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var tool = new GitLogTool();
        var result = await ExecuteAsync(tool, [], CreateContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var data = (Dictionary<string, object?>)result.Data!;
        var items = data["items"] as List<Dictionary<string, object?>>;
        items.Should().NotBeNull();
        items!.Should().HaveCount(1);

        var commit = items![0];
        commit["hash"].Should().BeOfType<string>().Which.Should().HaveLength(40);
        commit["author"].Should().Be("Test User");
        commit["subject"].Should().Be("Initial commit");
        commit["date"].Should().NotBeNull();
    }

    [Fact]
    public async Task GitShow_Head_ReturnsMetadataAndDiff()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var tool = new GitShowTool();
        var result = await ExecuteAsync(tool, new Dictionary<string, object?> { ["ref"] = "HEAD" }, CreateContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var data = (Dictionary<string, object?>)result.Data!;
        data["hash"].Should().BeOfType<string>().Which.Should().HaveLength(40);
        data["author"].Should().Be("Test User");
        data["author_email"].Should().Be("test@example.com");
        data["subject"].Should().Be("Initial commit");
        ((string)data["diff"]!).Should().Contain("file.txt");
        ((string)data["diff"]!).Should().Contain("hello world");
    }

    [Fact]
    public async Task GitBlame_ReturnsPerLineAttribution()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var tool = new GitBlameTool();
        var result = await ExecuteAsync(tool, new Dictionary<string, object?> { ["file_path"] = "file.txt" }, CreateContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var data = (Dictionary<string, object?>)result.Data!;
        var items = data["items"] as List<Dictionary<string, object?>>;
        items.Should().NotBeNull();
        items!.Should().HaveCount(1);
        items![0]["author"].Should().Be("Test User");
        items[0]["line"].Should().Be(1);
        items[0]["content"].Should().Be("hello world");
    }

    [Fact]
    public async Task GitStatus_NotARepo_Fails()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var nonRepo = Path.Combine(Path.GetTempPath(), "andy-git-nonrepo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(nonRepo);
        try
        {
            var tool = new GitStatusTool();
            var context = new ToolExecutionContext
            {
                WorkingDirectory = nonRepo,
                Permissions = new ToolPermissions { ProcessExecution = true }
            };
            var result = await ExecuteAsync(tool, [], context);

            result.IsSuccessful.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Not in a git repository");
        }
        finally
        {
            Directory.Delete(nonRepo, recursive: true);
        }
    }
}
