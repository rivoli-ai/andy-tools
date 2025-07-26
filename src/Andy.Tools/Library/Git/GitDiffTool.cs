using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Library.Git;

/// <summary>
/// Tool for displaying git diff output with color-coded changes.
/// </summary>
public class GitDiffTool : ToolBase
{
    private readonly ILogger<GitDiffTool>? _logger;
    private readonly IGitDiffFormatter _formatter;
    private static readonly Regex DiffHeaderRegex = new(@"^diff --git a/(.+) b/(.+)$", RegexOptions.Multiline);
    private static readonly Regex FileStatsRegex = new(@"^(.+?)\s+\|\s+(\d+)\s*([\+\-]+)$", RegexOptions.Multiline);
    private static readonly Regex HunkHeaderRegex = new(@"^@@\s+-(\d+)(?:,(\d+))?\s+\+(\d+)(?:,(\d+))?\s+@@", RegexOptions.Multiline);

    /// <summary>
    /// Initializes a new instance of the <see cref="GitDiffTool"/> class.
    /// </summary>
    public GitDiffTool() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitDiffTool"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public GitDiffTool(ILogger<GitDiffTool>? logger)
    {
        _logger = logger;
        _formatter = new GitDiffFormatter();
    }

    /// <summary>
    /// Checks if the tool can execute with the given permissions.
    /// </summary>
    public override bool CanExecuteWithPermissions(ToolPermissions permissions)
    {
        // Git diff requires process execution permission
        return permissions.ProcessExecution;
    }

    /// <summary>
    /// Gets the tool metadata.
    /// </summary>
    public override ToolMetadata Metadata => new()
    {
        Id = "git_diff",
        Name = "git_diff",
        Description = "Display git changes with color-coded diff output for modified files",
        Category = ToolCategory.Git,
        Parameters = new List<ToolParameter>
        {
            new ToolParameter
            {
                Name = "file_path",
                Type = "string",
                Description = "Path to the specific file to show diff for (optional)",
                Required = false
            },
            new ToolParameter
            {
                Name = "staged",
                Type = "boolean",
                Description = "Show staged changes instead of working directory changes",
                Required = false,
                DefaultValue = false
            },
            new ToolParameter
            {
                Name = "context_lines",
                Type = "integer",
                Description = "Number of context lines to show around changes (default: 3)",
                Required = false,
                DefaultValue = 3
            },
            new ToolParameter
            {
                Name = "max_lines",
                Type = "integer",
                Description = "Maximum number of changed lines to display per file (default: 10)",
                Required = false,
                DefaultValue = 10
            }
        }
    };

    /// <summary>
    /// Executes the git diff tool.
    /// </summary>
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        try
        {
            var filePath = parameters.GetValueOrDefault("file_path")?.ToString();
            var staged = parameters.GetValueOrDefault("staged") as bool? ?? false;
            var contextLines = parameters.GetValueOrDefault("context_lines") as int? ?? 3;
            var maxLines = parameters.GetValueOrDefault("max_lines") as int? ?? 10;

            _logger?.LogInformation("Executing git diff for file: {FilePath}, staged: {Staged}", filePath, staged);

            // Check if we're in a git repository
            if (!await IsGitRepositoryAsync())
            {
                return ToolResult.Failure("Not in a git repository");
            }

            // Get diff data
            var diffData = await GetGitDiffAsync(filePath, staged, contextLines);

            if (string.IsNullOrWhiteSpace(diffData))
            {
                return ToolResult.Success("No changes to display");
            }

            // Parse and format the diff
            var formattedOutput = await FormatDiffOutputAsync(diffData, maxLines);

            return ToolResult.Success(formattedOutput);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing git diff");
            return ToolResult.Failure($"Failed to get git diff: {ex.Message}");
        }
    }

    private async Task<bool> IsGitRepositoryAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --git-dir",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GetGitDiffAsync(string? filePath, bool staged, int contextLines)
    {
        var arguments = new StringBuilder("diff");

        if (staged)
        {
            arguments.Append(" --cached");
        }

        arguments.Append($" -U{contextLines}");
        arguments.Append(" --no-color");
        arguments.Append(" --stat");
        arguments.Append(" && git diff");

        if (staged)
        {
            arguments.Append(" --cached");
        }

        arguments.Append($" -U{contextLines}");
        arguments.Append(" --no-color");

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            arguments.Append($" -- \"{filePath}\"");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sh",
                Arguments = $"-c \"git {arguments}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException($"Git diff failed: {error}");
        }

        return output;
    }

    private Task<string> FormatDiffOutputAsync(string diffData, int maxLines)
    {
        var sections = diffData.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        if (sections.Length == 0)
        {
            return Task.FromResult("No changes to display");
        }

        var output = new StringBuilder();

        // Process stats section if present
        if (sections[0].Contains(" | ") && !sections[0].StartsWith("diff"))
        {
            output.AppendLine(_formatter.FormatStatsSummary(sections[0]));
            output.AppendLine();

            // Process diff sections
            if (sections.Length > 1)
            {
                var diffContent = string.Join("\n\n", sections.Skip(1));
                var fileDiffs = ParseFileDiffs(diffContent);

                foreach (var fileDiff in fileDiffs)
                {
                    output.AppendLine(_formatter.FormatFileDiff(fileDiff, maxLines));
                    output.AppendLine();
                }
            }
        }
        else
        {
            // No stats section, process all as diff content
            var fileDiffs = ParseFileDiffs(diffData);

            foreach (var fileDiff in fileDiffs)
            {
                output.AppendLine(_formatter.FormatFileDiff(fileDiff, maxLines));
                output.AppendLine();
            }
        }

        return Task.FromResult(output.ToString().TrimEnd());
    }

    private List<FileDiff> ParseFileDiffs(string diffContent)
    {
        var fileDiffs = new List<FileDiff>();
        var diffMatches = DiffHeaderRegex.Matches(diffContent);

        for (int i = 0; i < diffMatches.Count; i++)
        {
            var match = diffMatches[i];
            var startIndex = match.Index;
            var endIndex = i < diffMatches.Count - 1 ? diffMatches[i + 1].Index : diffContent.Length;
            var fileDiffContent = diffContent.Substring(startIndex, endIndex - startIndex);

            var fileDiff = ParseSingleFileDiff(match.Groups[1].Value, fileDiffContent);
            if (fileDiff != null)
            {
                fileDiffs.Add(fileDiff);
            }
        }

        return fileDiffs;
    }

    private FileDiff? ParseSingleFileDiff(string filePath, string diffContent)
    {
        var lines = diffContent.Split('\n');
        var hunks = new List<DiffHunk>();
        DiffHunk? currentHunk = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var hunkMatch = HunkHeaderRegex.Match(line);
            if (hunkMatch.Success)
            {
                if (currentHunk != null)
                {
                    hunks.Add(currentHunk);
                }

                currentHunk = new DiffHunk
                {
                    OldStart = int.Parse(hunkMatch.Groups[1].Value),
                    OldCount = hunkMatch.Groups[2].Success ? int.Parse(hunkMatch.Groups[2].Value) : 1,
                    NewStart = int.Parse(hunkMatch.Groups[3].Value),
                    NewCount = hunkMatch.Groups[4].Success ? int.Parse(hunkMatch.Groups[4].Value) : 1,
                    Lines = new List<DiffLine>()
                };
            }
            else if (currentHunk != null && (line.StartsWith("+") || line.StartsWith("-") || line.StartsWith(" ")))
            {
                currentHunk.Lines.Add(new DiffLine
                {
                    Type = line[0] switch
                    {
                        '+' => DiffLineType.Added,
                        '-' => DiffLineType.Removed,
                        _ => DiffLineType.Context
                    },
                    Content = line.Length > 1 ? line.Substring(1) : "",
                    LineNumber = line[0] switch
                    {
                        '+' => currentHunk.NewStart + currentHunk.Lines.Count(l => l.Type != DiffLineType.Removed),
                        '-' => currentHunk.OldStart + currentHunk.Lines.Count(l => l.Type != DiffLineType.Added),
                        _ => currentHunk.OldStart + currentHunk.Lines.Count(l => l.Type != DiffLineType.Added)
                    }
                });
            }
        }

        if (currentHunk != null)
        {
            hunks.Add(currentHunk);
        }

        if (hunks.Count == 0)
        {
            return null;
        }

        return new FileDiff
        {
            FilePath = filePath,
            Hunks = hunks,
            AddedLines = hunks.Sum(h => h.Lines.Count(l => l.Type == DiffLineType.Added)),
            RemovedLines = hunks.Sum(h => h.Lines.Count(l => l.Type == DiffLineType.Removed))
        };
    }
}
