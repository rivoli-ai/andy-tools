using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Library.Git;

/// <summary>
/// Tool for reporting the working tree status of a git repository.
/// </summary>
public class GitStatusTool : ToolBase
{
    private readonly ILogger<GitStatusTool>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitStatusTool"/> class.
    /// </summary>
    public GitStatusTool() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitStatusTool"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public GitStatusTool(ILogger<GitStatusTool>? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the tool metadata.
    /// </summary>
    public override ToolMetadata Metadata => new()
    {
        Id = "git_status",
        Name = "git_status",
        Description = "Show the working tree status of a git repository (branch, staged, modified, untracked files)",
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

            var result = await GitProcessRunner.RunAsync("status --porcelain=v1 -b", workingDirectory, context.CancellationToken);

            if (!result.Succeeded)
            {
                return ToolResults.Failure($"Failed to get git status: {result.StandardError.Trim()}");
            }

            return ParseStatus(result.StandardOutput);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing git status");
            return ToolResults.Failure($"Failed to get git status: {ex.Message}");
        }
    }

    private static ToolResult ParseStatus(string output)
    {
        string? branch = null;
        var staged = new List<string>();
        var modified = new List<string>();
        var untracked = new List<string>();

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("##", StringComparison.Ordinal))
            {
                // Format: "## branch...tracking [ahead N, behind M]" or "## HEAD (no branch)"
                var branchInfo = line.Length > 3 ? line[3..] : string.Empty;
                var dotsIndex = branchInfo.IndexOf("...", StringComparison.Ordinal);
                if (dotsIndex >= 0)
                {
                    branchInfo = branchInfo[..dotsIndex];
                }

                var spaceIndex = branchInfo.IndexOf(' ');
                if (spaceIndex >= 0)
                {
                    branchInfo = branchInfo[..spaceIndex];
                }

                branch = branchInfo.Trim();
                continue;
            }

            if (line.Length < 3)
            {
                continue;
            }

            var indexStatus = line[0];
            var workTreeStatus = line[1];
            var path = line[3..].Trim();

            if (indexStatus == '?' && workTreeStatus == '?')
            {
                untracked.Add(path);
                continue;
            }

            // X (index/staged) column: anything other than space or '?' means staged change.
            if (indexStatus != ' ' && indexStatus != '?')
            {
                staged.Add(path);
            }

            // Y (work tree) column: anything other than space or '?' means unstaged modification.
            if (workTreeStatus != ' ' && workTreeStatus != '?')
            {
                modified.Add(path);
            }
        }

        var data = new Dictionary<string, object?>
        {
            ["branch"] = branch,
            ["staged"] = staged,
            ["modified"] = modified,
            ["untracked"] = untracked,
            ["is_clean"] = staged.Count == 0 && modified.Count == 0 && untracked.Count == 0
        };

        return ToolResults.Success(data, "Git status retrieved successfully");
    }
}
