using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Library.Git;

/// <summary>
/// Tool for listing git commit history.
/// </summary>
public class GitLogTool : ToolBase
{
    private readonly ILogger<GitLogTool>? _logger;

    // Use a unit-separator delimited, NUL-terminated pretty format for stable parsing.
    private const string FieldSeparator = "\x1f";
    private const string RecordSeparator = "\x1e";
    private const string PrettyFormat = "%H\x1f%an\x1f%aI\x1f%s\x1e";

    /// <summary>
    /// Initializes a new instance of the <see cref="GitLogTool"/> class.
    /// </summary>
    public GitLogTool() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitLogTool"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public GitLogTool(ILogger<GitLogTool>? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the tool metadata.
    /// </summary>
    public override ToolMetadata Metadata => new()
    {
        Id = "git_log",
        Name = "git_log",
        Description = "List git commit history with hash, author, date, and subject for each commit",
        Category = ToolCategory.Git,
        RequiredPermissions = ToolPermissionFlags.ProcessExecution,
        Parameters =
        [
            new ToolParameter
            {
                Name = "max_count",
                Type = "integer",
                Description = "Maximum number of commits to return (default: 20)",
                Required = false,
                DefaultValue = 20
            },
            new ToolParameter
            {
                Name = "path",
                Type = "string",
                Description = "Limit the log to commits that touched the given path (optional)",
                Required = false
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var workingDirectory = context.WorkingDirectory;
        var maxCount = GetParameter<int>(parameters, "max_count", 20);
        var path = GetParameter<string?>(parameters, "path", null);

        if (maxCount <= 0)
        {
            maxCount = 20;
        }

        try
        {
            if (!await GitProcessRunner.IsGitRepositoryAsync(workingDirectory, context.CancellationToken))
            {
                return ToolResults.Failure("Not in a git repository");
            }

            var args = new StringBuilder();
            args.Append("log --no-color --max-count=").Append(maxCount);
            args.Append(" --pretty=format:").Append(PrettyFormat);

            if (!string.IsNullOrWhiteSpace(path))
            {
                args.Append(" -- \"").Append(path.Replace("\"", "\\\"")).Append('"');
            }

            var result = await GitProcessRunner.RunAsync(args.ToString(), workingDirectory, context.CancellationToken);

            if (!result.Succeeded)
            {
                return ToolResults.Failure($"Failed to get git log: {result.StandardError.Trim()}");
            }

            var commits = ParseLog(result.StandardOutput);
            return ToolResults.ListSuccess(commits, $"Retrieved {commits.Count} commit(s)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing git log");
            return ToolResults.Failure($"Failed to get git log: {ex.Message}");
        }
    }

    private static List<Dictionary<string, object?>> ParseLog(string output)
    {
        var commits = new List<Dictionary<string, object?>>();
        var records = output.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var record in records)
        {
            var trimmed = record.Trim('\n', '\r');
            if (trimmed.Length == 0)
            {
                continue;
            }

            var fields = trimmed.Split(FieldSeparator);
            if (fields.Length < 4)
            {
                continue;
            }

            commits.Add(new Dictionary<string, object?>
            {
                ["hash"] = fields[0],
                ["author"] = fields[1],
                ["date"] = fields[2],
                ["subject"] = fields[3]
            });
        }

        return commits;
    }
}
