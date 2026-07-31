using Andy.Tools.Core;
using Andy.Tools.Library.System;
using Microsoft.Extensions.Options;
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

    private static ExecuteCommandTool ToolWithCeiling(int maximumTimeoutSeconds)
    {
        var tool = new ExecuteCommandTool(Options.Create(new ExecuteCommandToolOptions
        {
            MaximumTimeoutSeconds = maximumTimeoutSeconds,
        }));
        tool.InitializeAsync().GetAwaiter().GetResult();
        return tool;
    }

    [Fact]
    public void Metadata_declares_process_execution_and_confirmation()
    {
        Assert.Equal("execute_command", _tool.Metadata.Id);
        Assert.True(_tool.Metadata.RequiresConfirmation);
        Assert.True(_tool.Metadata.RequiredCapabilities.HasFlag(ToolCapability.ProcessExecution));
        Assert.True(_tool.Metadata.RequiredPermissions.HasFlag(ToolPermissionFlags.ProcessExecution));
    }

    [Fact]
    public void Parameterless_reflection_activation_preserves_tool_discovery_compatibility()
    {
        var tool = Activator.CreateInstance<ExecuteCommandTool>();

        Assert.NotNull(tool);
        Assert.Equal("execute_command", tool.Metadata.Id);
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
        Assert.False((bool)data["cancelled"]!);
        Assert.Equal("timeout", data["termination_reason"]);
        Assert.Equal(1, data["effective_timeout_seconds"]);
        Assert.Equal("timeout", result.Metadata["termination_reason"]);
    }

    [Fact]
    public async Task Host_ceiling_clamps_model_timeout_and_reports_precedence()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var tool = ToolWithCeiling(1);
        var result = await tool.ExecuteAsync(
            P(("command", "sleep 10"), ("timeout_seconds", 30)),
            Context());

        Assert.False(result.IsSuccessful);
        Assert.Equal(30, result.Metadata["requested_timeout_seconds"]);
        Assert.Equal(1, result.Metadata["effective_timeout_seconds"]);
        Assert.Equal(1, result.Metadata["timeout_ceiling_seconds"]);
        Assert.Equal(true, result.Metadata["timeout_clamped"]);
        Assert.Equal("host_ceiling", result.Metadata["timeout_source"]);
        Assert.Equal("timeout", result.Metadata["termination_reason"]);
    }

    [Fact]
    public async Task No_host_ceiling_preserves_requested_timeout()
    {
        var result = await _tool.ExecuteAsync(
            P(("command", "echo timeout_metadata"), ("timeout_seconds", 600)),
            Context());

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        Assert.Equal(600, result.Metadata["requested_timeout_seconds"]);
        Assert.Equal(600, result.Metadata["effective_timeout_seconds"]);
        Assert.Equal(false, result.Metadata["timeout_clamped"]);
        Assert.Equal("model", result.Metadata["timeout_source"]);
        Assert.False(result.Metadata.ContainsKey("timeout_ceiling_seconds"));
    }

    [Fact]
    public async Task Host_ceiling_clamps_default_timeout_when_model_omits_it()
    {
        var tool = ToolWithCeiling(5);
        var result = await tool.ExecuteAsync(P(("command", "echo default_timeout")), Context());

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        Assert.Equal(120, result.Metadata["requested_timeout_seconds"]);
        Assert.Equal(5, result.Metadata["effective_timeout_seconds"]);
        Assert.Equal("host_ceiling", result.Metadata["timeout_source"]);
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
        Assert.Contains("cancelled by the host", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        var data = Assert.IsType<Dictionary<string, object?>>(result.Data);
        Assert.False((bool)data["timed_out"]!);
        Assert.True((bool)data["cancelled"]!);
        Assert.Equal("external_cancellation", data["termination_reason"]);
        Assert.Equal("external_cancellation", result.Metadata["termination_reason"]);
    }

    [Fact]
    public async Task Timeout_terminates_child_process_tree()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var marker = Path.Combine(
            Path.GetTempPath(),
            "andy-command-child-" + Guid.NewGuid().ToString("N"));
        var command = $"(sleep 2; touch '{marker}') & wait";
        var tool = ToolWithCeiling(1);

        try
        {
            var result = await tool.ExecuteAsync(
                P(("command", command), ("timeout_seconds", 30)),
                Context());
            Assert.False(result.IsSuccessful);
            Assert.Equal("timeout", result.Metadata["termination_reason"]);

            await Task.Delay(1500);
            Assert.False(
                File.Exists(marker),
                "the timed-out command's child process must not survive to create the marker");
        }
        finally
        {
            if (File.Exists(marker))
            {
                File.Delete(marker);
            }
        }
    }

    [Fact]
    public void Nonpositive_host_ceiling_is_rejected()
    {
        var options = Options.Create(new ExecuteCommandToolOptions
        {
            MaximumTimeoutSeconds = 0,
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => new ExecuteCommandTool(options));
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
