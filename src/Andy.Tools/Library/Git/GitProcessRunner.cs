using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Andy.Tools.Library.Git;

/// <summary>
/// Result of running a git command.
/// </summary>
internal sealed class GitCommandResult
{
    /// <summary>
    /// Gets or sets the process exit code.
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Gets or sets the standard output.
    /// </summary>
    public string StandardOutput { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the standard error.
    /// </summary>
    public string StandardError { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the command succeeded.
    /// </summary>
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Helper for running read-only git commands as a child process.
/// </summary>
internal static class GitProcessRunner
{
    /// <summary>
    /// Runs a git command in the specified working directory.
    /// </summary>
    /// <param name="arguments">The git arguments (without the leading "git").</param>
    /// <param name="workingDirectory">The directory to run git in. Defaults to current directory.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The command result.</returns>
    public static async Task<GitCommandResult> RunAsync(string arguments, string? workingDirectory, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new GitCommandResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = await stdoutTask.ConfigureAwait(false),
            StandardError = await stderrTask.ConfigureAwait(false)
        };
    }

    /// <summary>
    /// Checks whether the working directory is inside a git repository.
    /// </summary>
    /// <param name="workingDirectory">The directory to check.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><c>true</c> if inside a git repository.</returns>
    public static async Task<bool> IsGitRepositoryAsync(string? workingDirectory, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RunAsync("rev-parse --is-inside-work-tree", workingDirectory, cancellationToken).ConfigureAwait(false);
            return result.Succeeded && result.StandardOutput.Trim() == "true";
        }
        catch
        {
            return false;
        }
    }
}
