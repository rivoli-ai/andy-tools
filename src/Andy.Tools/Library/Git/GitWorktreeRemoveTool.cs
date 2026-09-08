using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Library.Git;

/// <summary>
/// Tool for removing a git worktree and its directory.
/// </summary>
public class GitWorktreeRemoveTool : ToolBase
{
    private readonly ILogger<GitWorktreeRemoveTool>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitWorktreeRemoveTool"/> class.
    /// </summary>
    public GitWorktreeRemoveTool() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitWorktreeRemoveTool"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public GitWorktreeRemoveTool(ILogger<GitWorktreeRemoveTool>? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the tool metadata.
    /// </summary>
    public override ToolMetadata Metadata => new()
    {
        Id = "git_worktree_remove",
        Name = "git_worktree_remove",
        Description = "Remove a git worktree and delete its directory; refuses dirty or locked worktrees unless force is set",
        Category = ToolCategory.Git,
        RequiredPermissions = ToolPermissionFlags.ProcessExecution | ToolPermissionFlags.FileSystemWrite,
        RequiredCapabilities = ToolCapability.Destructive,
        RequiresConfirmation = true,
        Parameters =
        [
            new ToolParameter
            {
                Name = "path",
                Type = "string",
                Description = "Path of the worktree to remove; relative paths resolve against the working directory",
                Required = true
            },
            new ToolParameter
            {
                Name = "force",
                Type = "boolean",
                Description = "Remove the worktree even if it has uncommitted changes or is locked (default: false)",
                Required = false,
                DefaultValue = false
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var workingDirectory = context.WorkingDirectory;
        var path = GetParameter<string?>(parameters, "path", null);
        var force = GetParameter<bool>(parameters, "force", false);

        if (string.IsNullOrWhiteSpace(path))
        {
            return ToolResults.InvalidParameter("path", path, "A worktree path is required");
        }

        try
        {
            if (!await GitProcessRunner.IsGitRepositoryAsync(workingDirectory, context.CancellationToken))
            {
                return ToolResults.Failure("Not in a git repository");
            }

            var fullPath = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(workingDirectory ?? Directory.GetCurrentDirectory(), path));

            var args = new StringBuilder("worktree remove");

            if (force)
            {
                // A locked worktree needs --force twice; a dirty one needs it once.
                args.Append(" --force --force");
            }

            args.Append(' ').Append(Quote(fullPath));

            var result = await GitProcessRunner.RunAsync(args.ToString(), workingDirectory, context.CancellationToken);

            if (!result.Succeeded)
            {
                return ToolResults.Failure($"Failed to remove worktree: {result.StandardError.Trim()}");
            }

            return ToolResults.Success(new Dictionary<string, object?>
            {
                ["removed_path"] = fullPath,
                ["forced"] = force
            }, $"Removed worktree at {fullPath}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing git worktree");
            return ToolResults.Failure($"Failed to remove worktree: {ex.Message}");
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
