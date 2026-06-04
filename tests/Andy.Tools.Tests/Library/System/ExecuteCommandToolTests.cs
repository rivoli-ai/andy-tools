using Andy.Tools.Core;
using Andy.Tools.Library.System;
using Xunit;

namespace Andy.Tools.Tests.Library.System;

public class ExecuteCommandToolTests
{
    private readonly ExecuteCommandTool _tool;

    public ExecuteCommandToolTests()
    {
        _tool = new ExecuteCommandTool();
        _tool.InitializeAsync().GetAwaiter().GetResult();
    }

    private static ToolExecutionContext Context(string? workingDir = null) => new()
    {
        WorkingDirectory = workingDir,
        Permissions = new ToolPermissions { ProcessExecution = true },
    };

    private static Dictionary<string, object?> P(params (string, object?)[] kv) =>
        kv.ToDictionary(x => x.Item1, x => x.Item2);

    [Fact]
    public void Metadata_declares_process_execution_and_confirmation()
    {
        Assert.Equal("execute_command", _tool.Metadata.Id);
        Assert.True(_tool.Metadata.RequiresConfirmation);
        Assert.True(_tool.Metadata.RequiredCapabilities.HasFlag(ToolCapability.ProcessExecution));
        Assert.True(_tool.Metadata.RequiredPermissions.HasFlag(ToolPermissionFlags.ProcessExecution));
    }

    [Fact]
    public async Task Echo_returns_stdout_and_zero_exit()
    {
        var result = await _tool.ExecuteAsync(P(("command", "echo hello_world")), Context());

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        var data = Assert.IsType<Dictionary<string, object?>>(result.Data);
        Assert.Equal(0, data["exit_code"]);
        Assert.Contains("hello_world", (string)data["stdout"]!);
    }

    [Fact]
    public async Task Nonzero_exit_is_reported_as_failure_with_code()
    {
        var result = await _tool.ExecuteAsync(P(("command", "exit 3")), Context());

        Assert.False(result.IsSuccessful);
        var data = Assert.IsType<Dictionary<string, object?>>(result.Data);
        Assert.Equal(3, data["exit_code"]);
    }

    [Fact]
    public async Task Missing_process_execution_permission_is_denied()
    {
        var ctx = new ToolExecutionContext { Permissions = new ToolPermissions { ProcessExecution = false } };
        var result = await _tool.ExecuteAsync(P(("command", "echo hi")), ctx);

        Assert.False(result.IsSuccessful);
        Assert.Contains("permission", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Empty_command_fails_validation()
    {
        var result = await _tool.ExecuteAsync(P(("command", "")), Context());
        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task Nonexistent_working_directory_fails()
    {
        var missing = Path.Combine(Path.GetTempPath(), "andy-no-such-" + Guid.NewGuid().ToString("N"));
        var result = await _tool.ExecuteAsync(P(("command", "echo hi"), ("working_directory", missing)), Context());

        Assert.False(result.IsSuccessful);
        Assert.Contains("working directory", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runs_in_specified_working_directory()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // pwd is POSIX; covered on Linux/macOS CI legs
        }

        var dir = Path.Combine(Path.GetTempPath(), "andy-wd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var result = await _tool.ExecuteAsync(P(("command", "pwd"), ("working_directory", dir)), Context());
            Assert.True(result.IsSuccessful, result.ErrorMessage);
            var data = (Dictionary<string, object?>)result.Data!;
            // macOS resolves /var/folders/... via /private symlink; match on the leaf.
            Assert.Contains(Path.GetFileName(dir), (string)data["stdout"]!);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Timeout_terminates_long_command()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // sleep is POSIX; covered on Linux/macOS CI legs
        }

        var result = await _tool.ExecuteAsync(P(("command", "sleep 10"), ("timeout_seconds", 1)), Context());

        Assert.False(result.IsSuccessful);
        var data = (Dictionary<string, object?>)result.Data!;
        Assert.True((bool)data["timed_out"]!);
    }

    [Fact]
    public async Task Cancellation_is_reported()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        var ctx = new ToolExecutionContext
        {
            Permissions = new ToolPermissions { ProcessExecution = true },
            CancellationToken = cts.Token,
        };
        cts.CancelAfter(200);

        var result = await _tool.ExecuteAsync(P(("command", "sleep 10")), ctx);
        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task Environment_variables_are_passed_through()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // ${VAR} expansion is shell-specific
        }

        var ctx = new ToolExecutionContext
        {
            Permissions = new ToolPermissions { ProcessExecution = true },
            Environment = { ["ANDY_TEST_VAR"] = "andyval123" },
        };

        var result = await _tool.ExecuteAsync(P(("command", "echo $ANDY_TEST_VAR")), ctx);
        Assert.True(result.IsSuccessful, result.ErrorMessage);
        var data = (Dictionary<string, object?>)result.Data!;
        Assert.Contains("andyval123", (string)data["stdout"]!);
    }
}
