using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Library.Git;

/// <summary>
/// Tool for showing a single git object (commit metadata and diff).
/// </summary>
public class GitShowTool : ToolBase
{
    private readonly ILogger<GitShowTool>? _logger;

    private const string FieldSeparator = "\x1f";
    private const string MetadataFormat = "%H\x1f%an\x1f%ae\x1f%aI\x1f%s\x1f%b";

    /// <summary>
    /// Initializes a new instance of the <see cref="GitShowTool"/> class.
    /// </summary>
    public GitShowTool() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitShowTool"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public GitShowTool(ILogger<GitShowTool>? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the tool metadata.
    /// </summary>
    public override ToolMetadata Metadata => new()
    {
        Id = "git_show",
        Name = "git_show",
        Description = "Show metadata and diff for a single git commit or object",
        Category = ToolCategory.Git,
        RequiredPermissions = ToolPermissionFlags.ProcessExecution,
        Parameters =
        [
            new ToolParameter
            {
                Name = "ref",
                Type = "string",
                Description = "The git reference to show (commit hash, tag, branch, etc.). Defaults to HEAD",
                Required = false,
                DefaultValue = "HEAD"
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var workingDirectory = context.WorkingDirectory;
        var reference = GetParameter<string>(parameters, "ref", "HEAD");
        if (string.IsNullOrWhiteSpace(reference))
        {
            reference = "HEAD";
        }

        try
        {
            if (!await GitProcessRunner.IsGitRepositoryAsync(workingDirectory, context.CancellationToken))
            {
                return ToolResults.Failure("Not in a git repository");
            }

            var safeRef = reference.Replace("\"", "\\\"");

            // Fetch commit metadata first so we can return it as structured data.
            var metaArgs = new StringBuilder();
            metaArgs.Append("show --no-color --no-patch --pretty=format:").Append(MetadataFormat);
            metaArgs.Append(" \"").Append(safeRef).Append('"');

            var metaResult = await GitProcessRunner.RunAsync(metaArgs.ToString(), workingDirectory, context.CancellationToken);
            if (!metaResult.Succeeded)
            {
                return ToolResults.Failure($"Failed to show '{reference}': {metaResult.StandardError.Trim()}");
            }

            // Fetch the diff text separately.
            var diffArgs = new StringBuilder();
            diffArgs.Append("show --no-color --format= \"").Append(safeRef).Append('"');
            var diffResult = await GitProcessRunner.RunAsync(diffArgs.ToString(), workingDirectory, context.CancellationToken);
            if (!diffResult.Succeeded)
            {
                return ToolResults.Failure($"Failed to show '{reference}': {diffResult.StandardError.Trim()}");
            }

            var data = ParseMetadata(metaResult.StandardOutput);
            data["ref"] = reference;
            data["diff"] = diffResult.StandardOutput.Trim('\n', '\r');

            return ToolResults.Success(data, $"Retrieved details for '{reference}'");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing git show");
            return ToolResults.Failure($"Failed to show '{reference}': {ex.Message}");
        }
    }

    private static Dictionary<string, object?> ParseMetadata(string output)
    {
        var fields = output.Split(FieldSeparator);

        return new Dictionary<string, object?>
        {
            ["hash"] = fields.Length > 0 ? fields[0] : null,
            ["author"] = fields.Length > 1 ? fields[1] : null,
            ["author_email"] = fields.Length > 2 ? fields[2] : null,
            ["date"] = fields.Length > 3 ? fields[3] : null,
            ["subject"] = fields.Length > 4 ? fields[4] : null,
            ["body"] = fields.Length > 5 ? fields[5].Trim('\n', '\r') : string.Empty
        };
    }
}
