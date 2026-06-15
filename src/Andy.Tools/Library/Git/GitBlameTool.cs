using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Library.Git;

/// <summary>
/// Tool for showing per-line commit attribution (git blame) for a file.
/// </summary>
public class GitBlameTool : ToolBase
{
    private readonly ILogger<GitBlameTool>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitBlameTool"/> class.
    /// </summary>
    public GitBlameTool() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitBlameTool"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public GitBlameTool(ILogger<GitBlameTool>? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the tool metadata.
    /// </summary>
    public override ToolMetadata Metadata => new()
    {
        Id = "git_blame",
        Name = "git_blame",
        Description = "Show per-line commit attribution (blame) for a file, optionally limited to a line range",
        Category = ToolCategory.Git,
        RequiredPermissions = ToolPermissionFlags.ProcessExecution,
        Parameters =
        [
            new ToolParameter
            {
                Name = "file_path",
                Type = "string",
                Description = "Path to the file to blame",
                Required = true
            },
            new ToolParameter
            {
                Name = "start_line",
                Type = "integer",
                Description = "First line of the range to blame (1-based, optional)",
                Required = false
            },
            new ToolParameter
            {
                Name = "end_line",
                Type = "integer",
                Description = "Last line of the range to blame (1-based, optional)",
                Required = false
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var workingDirectory = context.WorkingDirectory;
        var filePath = GetParameter<string?>(parameters, "file_path", null);
        var startLine = GetParameter<int>(parameters, "start_line", 0);
        var endLine = GetParameter<int>(parameters, "end_line", 0);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ToolResults.InvalidParameter("file_path", filePath, "A file path is required");
        }

        try
        {
            if (!await GitProcessRunner.IsGitRepositoryAsync(workingDirectory, context.CancellationToken))
            {
                return ToolResults.Failure("Not in a git repository");
            }

            var args = new StringBuilder();
            args.Append("blame --line-porcelain");

            if (startLine > 0)
            {
                var rangeEnd = endLine >= startLine ? endLine.ToString() : string.Empty;
                args.Append(" -L ").Append(startLine).Append(',').Append(rangeEnd.Length > 0 ? rangeEnd : startLine.ToString());
            }

            args.Append(" -- \"").Append(filePath.Replace("\"", "\\\"")).Append('"');

            var result = await GitProcessRunner.RunAsync(args.ToString(), workingDirectory, context.CancellationToken);

            if (!result.Succeeded)
            {
                return ToolResults.Failure($"Failed to blame '{filePath}': {result.StandardError.Trim()}");
            }

            var lines = ParseBlame(result.StandardOutput);
            return ToolResults.ListSuccess(lines, $"Blamed {lines.Count} line(s) of '{filePath}'");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing git blame");
            return ToolResults.Failure($"Failed to blame '{filePath}': {ex.Message}");
        }
    }

    private static List<Dictionary<string, object?>> ParseBlame(string output)
    {
        var entries = new List<Dictionary<string, object?>>();
        var lines = output.Split('\n');

        string? commit = null;
        string? author = null;
        int lineNumber = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("author ", StringComparison.Ordinal))
            {
                author = line[7..];
                continue;
            }

            if (line.StartsWith('\t'))
            {
                // The actual source line; emit the accumulated entry.
                entries.Add(new Dictionary<string, object?>
                {
                    ["line"] = lineNumber,
                    ["commit"] = commit,
                    ["author"] = author,
                    ["content"] = line.Length > 1 ? line[1..] : string.Empty
                });
                continue;
            }

            // A header line begins each porcelain group: "<sha> <orig> <final> [count]".
            var firstSpace = line.IndexOf(' ');
            if (firstSpace == 40 && IsHex(line.AsSpan(0, 40)))
            {
                commit = line[..40];
                var rest = line[(firstSpace + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (rest.Length >= 2 && int.TryParse(rest[1], out var finalLine))
                {
                    lineNumber = finalLine;
                }
            }
        }

        return entries;
    }

    private static bool IsHex(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            var isHex = c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }
}
