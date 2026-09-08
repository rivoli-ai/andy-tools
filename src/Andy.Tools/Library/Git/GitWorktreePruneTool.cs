using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Library.Git;

/// <summary>
/// Tool for pruning stale git worktree registrations whose directories no longer exist.
/// </summary>
public class GitWorktreePruneTool : ToolBase
{
    private readonly ILogger<GitWorktreePruneTool>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitWorktreePruneTool"/> class.
    /// </summary>
    public GitWorktreePruneTool() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitWorktreePruneTool"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public GitWorktreePruneTool(ILogger<GitWorktreePruneTool>? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the tool metadata.
    /// </summary>
    public override ToolMetadata Metadata => new()
    {
        Id = "git_worktree_prune",
        Name = "git_worktree_prune",
        Description = "Prune stale git worktree registrations whose directories were deleted from disk",
        Category = ToolCategory.Git,
        RequiredPermissions = ToolPermissionFlags.ProcessExecution | ToolPermissionFlags.FileSystemWrite,
        Parameters =
        [
            new ToolParameter
            {
                Name = "dry_run",
                Type = "boolean",
                Description = "Report what would be pruned without removing anything (default: false)",
                Required = false,
                DefaultValue = false
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var workingDirectory = context.WorkingDirectory;
        var dryRun = GetParameter<bool>(parameters, "dry_run", false);

        try
        {
            if (!await GitProcessRunner.IsGitRepositoryAsync(workingDirectory, context.CancellationToken))
            {
                return ToolResults.Failure("Not in a git repository");
            }

            var args = dryRun ? "worktree prune --verbose --dry-run" : "worktree prune --verbose";
            var result = await GitProcessRunner.RunAsync(args, workingDirectory, context.CancellationToken);

            if (!result.Succeeded)
            {
                return ToolResults.Failure($"Failed to prune worktrees: {result.StandardError.Trim()}");
            }

            // git prints one "Removing worktrees/<name>: <reason>" line per pruned entry, on stderr
            // in some git versions and stdout in others.
            var pruned = (result.StandardOutput + "\n" + result.StandardError)
                .Split('\n')
                .Select(static line => line.Trim())
                .Where(static line => line.Length > 0)
                .ToList();

            var action = dryRun ? "Would prune" : "Pruned";
            return ToolResults.Success(new Dictionary<string, object?>
            {
                ["pruned"] = pruned,
                ["dry_run"] = dryRun
            }, $"{action} {pruned.Count} stale worktree registration(s)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error pruning git worktrees");
            return ToolResults.Failure($"Failed to prune worktrees: {ex.Message}");
        }
    }
}
