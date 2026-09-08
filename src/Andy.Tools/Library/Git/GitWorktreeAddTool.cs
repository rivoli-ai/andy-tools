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
/// Tool for creating a new git worktree.
/// </summary>
public class GitWorktreeAddTool : ToolBase
{
    private readonly ILogger<GitWorktreeAddTool>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitWorktreeAddTool"/> class.
    /// </summary>
    public GitWorktreeAddTool() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitWorktreeAddTool"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public GitWorktreeAddTool(ILogger<GitWorktreeAddTool>? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the tool metadata.
    /// </summary>
    public override ToolMetadata Metadata => new()
    {
        Id = "git_worktree_add",
        Name = "git_worktree_add",
        Description = "Create a new git worktree at the given path, optionally on a new branch or detached at a specific commit",
        Category = ToolCategory.Git,
        RequiredPermissions = ToolPermissionFlags.ProcessExecution | ToolPermissionFlags.FileSystemWrite,
        Parameters =
        [
            new ToolParameter
            {
                Name = "path",
                Type = "string",
                Description = "Directory to create the worktree in; relative paths resolve against the working directory",
                Required = true
            },
            new ToolParameter
            {
                Name = "branch",
                Type = "string",
                Description = "Name of a new branch to create for the worktree (optional; incompatible with detach)",
                Required = false
            },
            new ToolParameter
            {
                Name = "commit_ish",
                Type = "string",
                Description = "Commit, branch, or tag the worktree starts from (optional; defaults to HEAD)",
                Required = false
            },
            new ToolParameter
            {
                Name = "detach",
                Type = "boolean",
                Description = "Check out the worktree with a detached HEAD instead of a branch (default: false)",
                Required = false,
                DefaultValue = false
            },
            new ToolParameter
            {
                Name = "force",
                Type = "boolean",
                Description = "Allow checking out a branch that is already checked out in another worktree (default: false)",
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
        var branch = GetParameter<string?>(parameters, "branch", null);
        var commitIsh = GetParameter<string?>(parameters, "commit_ish", null);
        var detach = GetParameter<bool>(parameters, "detach", false);
        var force = GetParameter<bool>(parameters, "force", false);

        if (string.IsNullOrWhiteSpace(path))
        {
            return ToolResults.InvalidParameter("path", path, "A worktree path is required");
        }

        if (detach && !string.IsNullOrWhiteSpace(branch))
        {
            return ToolResults.InvalidParameter("detach", detach, "detach cannot be combined with a new branch name");
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

            var args = new StringBuilder("worktree add");

            if (force)
            {
                args.Append(" --force");
            }

            if (detach)
            {
                args.Append(" --detach");
            }

            if (!string.IsNullOrWhiteSpace(branch))
            {
                args.Append(" -b ").Append(Quote(branch));
            }

            args.Append(' ').Append(Quote(fullPath));

            if (!string.IsNullOrWhiteSpace(commitIsh))
            {
                args.Append(' ').Append(Quote(commitIsh));
            }

            var result = await GitProcessRunner.RunAsync(args.ToString(), workingDirectory, context.CancellationToken);

            if (!result.Succeeded)
            {
                return ToolResults.Failure($"Failed to add worktree: {result.StandardError.Trim()}");
            }

            var head = await GitProcessRunner.RunAsync("rev-parse HEAD", fullPath, context.CancellationToken);
            var currentBranch = await GitProcessRunner.RunAsync("rev-parse --abbrev-ref HEAD", fullPath, context.CancellationToken);
            var branchName = currentBranch.Succeeded ? currentBranch.StandardOutput.Trim() : null;

            return ToolResults.DirectorySuccess(fullPath, $"Created worktree at {fullPath}", new Dictionary<string, object?>
            {
                ["head"] = head.Succeeded ? head.StandardOutput.Trim() : null,
                ["branch"] = branchName == "HEAD" ? null : branchName,
                ["is_detached"] = branchName == "HEAD"
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding git worktree");
            return ToolResults.Failure($"Failed to add worktree: {ex.Message}");
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
