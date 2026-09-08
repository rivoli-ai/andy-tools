using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Library.Git;

/// <summary>
/// Tool for listing git worktrees attached to the current repository.
/// </summary>
public class GitWorktreeListTool : ToolBase
{
    private readonly ILogger<GitWorktreeListTool>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitWorktreeListTool"/> class.
    /// </summary>
    public GitWorktreeListTool() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitWorktreeListTool"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public GitWorktreeListTool(ILogger<GitWorktreeListTool>? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the tool metadata.
    /// </summary>
    public override ToolMetadata Metadata => new()
    {
        Id = "git_worktree_list",
        Name = "git_worktree_list",
        Description = "List all git worktrees of the current repository with their path, HEAD, branch, and lock/prunable state",
        Category = ToolCategory.Git,
        RequiredPermissions = ToolPermissionFlags.ProcessExecution,
        Parameters = []
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var workingDirectory = context.WorkingDirectory;

        try
        {
            if (!await GitProcessRunner.IsGitRepositoryAsync(workingDirectory, context.CancellationToken))
            {
                return ToolResults.Failure("Not in a git repository");
            }

            var result = await GitProcessRunner.RunAsync("worktree list --porcelain", workingDirectory, context.CancellationToken);

            if (!result.Succeeded)
            {
                return ToolResults.Failure($"Failed to list worktrees: {result.StandardError.Trim()}");
            }

            var worktrees = GitWorktreePorcelainParser.Parse(result.StandardOutput);
            return ToolResults.ListSuccess(worktrees, $"Found {worktrees.Count} worktree(s)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error listing git worktrees");
            return ToolResults.Failure($"Failed to list worktrees: {ex.Message}");
        }
    }
}
