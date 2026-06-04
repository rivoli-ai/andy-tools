using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.System;

/// <summary>
/// Executes a shell command and returns its exit code, stdout, and stderr. This is a high-privilege tool:
/// it declares the <see cref="ToolCapability.ProcessExecution"/> capability and requires confirmation, so
/// when used behind the Andy.Permissions consent layer it defaults to Ask unless an explicit allow rule
/// matches. The command string is passed as a single argument to the shell (<c>bash -c</c> /
/// <c>cmd /c</c>); no extra wrapping is performed.
/// </summary>
public class ExecuteCommandTool : ToolBase
{
    private const int DefaultTimeoutSeconds = 120;
    private const int MaxStreamChars = 1_000_000;

    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "execute_command",
        Name = "Execute Command",
        Description = "Executes a shell command and returns its exit code, standard output, and standard error.",
        Version = "1.0.0",
        Category = ToolCategory.System,
        RequiredPermissions = ToolPermissionFlags.ProcessExecution,
        RequiredCapabilities = ToolCapability.ProcessExecution,
        RequiresConfirmation = true,
        Parameters =
        [
            new()
            {
                Name = "command",
                Description = "The shell command to execute.",
                Type = "string",
                Required = true,
            },
            new()
            {
                Name = "working_directory",
                Description = "Directory to run the command in (defaults to the execution context working directory).",
                Type = "string",
                Required = false,
            },
            new()
            {
                Name = "timeout_seconds",
                Description = "Maximum seconds to allow the command to run before it is killed (default: 120).",
                Type = "integer",
                Required = false,
                DefaultValue = DefaultTimeoutSeconds,
            },
        ],
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var command = GetParameter<string>(parameters, "command");
        if (string.IsNullOrWhiteSpace(command))
        {
            return ToolResult.Failure("Parameter 'command' is required and cannot be empty.");
        }

        var timeoutSeconds = GetParameter(parameters, "timeout_seconds", DefaultTimeoutSeconds);
        if (timeoutSeconds <= 0)
        {
            timeoutSeconds = DefaultTimeoutSeconds;
        }

        var workingDirectory = GetParameter<string?>(parameters, "working_directory", null)
            ?? context.WorkingDirectory;
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            workingDirectory = ToolHelpers.GetSafePath(workingDirectory, context.WorkingDirectory);
            if (!Directory.Exists(workingDirectory))
            {
                return ToolResult.Failure($"Working directory does not exist: {workingDirectory}");
            }
        }

        var psi = BuildShellStartInfo(command);
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        foreach (var kvp in context.Environment)
        {
            psi.Environment[kvp.Key] = kvp.Value;
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => AppendCapped(stdout, e.Data);
        process.ErrorDataReceived += (_, e) => AppendCapped(stderr, e.Data);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return ToolResult.Failure($"Failed to start command: {ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeoutCts.Token);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            if (context.CancellationToken.IsCancellationRequested)
            {
                throw; // genuine cancellation — let ToolBase report it
            }

            timedOut = true;
        }

        stopwatch.Stop();

        // Drain any final buffered output.
        try { process.WaitForExit(500); } catch { /* best effort */ }

        var exitCode = timedOut ? -1 : SafeExitCode(process);
        var data = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["exit_code"] = exitCode,
            ["stdout"] = stdout.ToString(),
            ["stderr"] = stderr.ToString(),
            ["duration_ms"] = stopwatch.Elapsed.TotalMilliseconds,
            ["timed_out"] = timedOut,
            ["working_directory"] = workingDirectory,
        };

        if (timedOut)
        {
            return new ToolResult
            {
                IsSuccessful = false,
                Data = data,
                ErrorMessage = $"Command timed out after {timeoutSeconds}s and was terminated.",
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
            };
        }

        return new ToolResult
        {
            IsSuccessful = exitCode == 0,
            Data = data,
            ErrorMessage = exitCode == 0 ? null : $"Command exited with code {exitCode}.",
            DurationMs = stopwatch.Elapsed.TotalMilliseconds,
        };
    }

    private static ProcessStartInfo BuildShellStartInfo(string command)
    {
        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(command);
        }
        else
        {
            psi.FileName = File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(command);
        }

        return psi;
    }

    private static void AppendCapped(StringBuilder sb, string? data)
    {
        if (data is null || sb.Length >= MaxStreamChars)
        {
            return;
        }

        var remaining = MaxStreamChars - sb.Length;
        if (data.Length + 1 > remaining)
        {
            sb.Append(data.AsSpan(0, Math.Max(0, remaining - 1)));
            sb.Append("\n…[output truncated]");
        }
        else
        {
            sb.AppendLine(data);
        }
    }

    private static int SafeExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best effort
        }
    }
}
