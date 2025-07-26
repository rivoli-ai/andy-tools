using System;
using System.Linq;
using System.Text;

namespace Andy.Tools.Library.Git;

/// <summary>
/// Formatter for git diff output with color coding.
/// </summary>
public class GitDiffFormatter : IGitDiffFormatter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GitDiffFormatter"/> class.
    /// </summary>
    public GitDiffFormatter()
    {
    }

    /// <summary>
    /// Formats the stats summary section of git diff output.
    /// </summary>
    public string FormatStatsSummary(string statsContent)
    {
        var output = new StringBuilder();
        var lines = statsContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        output.AppendLine("📊 **Change Summary**");
        output.AppendLine();

        foreach (var line in lines)
        {
            if (line.Contains(" | "))
            {
                var parts = line.Split(" | ");
                if (parts.Length >= 2)
                {
                    var fileName = parts[0].Trim();
                    var changes = parts[1].Trim();

                    output.Append($"  `{fileName}` ");

                    // Parse the changes (e.g., "5 +++++-")
                    var changesParts = changes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (changesParts.Length >= 1)
                    {
                        var count = changesParts[0];
                        output.Append($"({count} changes");

                        if (changesParts.Length >= 2)
                        {
                            var indicators = changesParts[1];
                            var additions = indicators.Count(c => c == '+');
                            var deletions = indicators.Count(c => c == '-');

                            if (additions > 0 || deletions > 0)
                            {
                                output.Append(": ");
                                if (additions > 0)
                                {
                                    output.Append($"**+{additions}**");
                                }

                                if (additions > 0 && deletions > 0)
                                {
                                    output.Append(", ");
                                }

                                if (deletions > 0)
                                {
                                    output.Append($"**-{deletions}**");
                                }
                            }
                        }

                        output.AppendLine(")");
                    }
                    else
                    {
                        output.AppendLine();
                    }
                }
            }
            else if (line.Contains(" changed,") || line.Contains(" changed"))
            {
                // Summary line like "3 files changed, 47 insertions(+), 5 deletions(-)"
                output.AppendLine();
                output.AppendLine($"  **Total**: {line.Trim()}");
            }
        }

        return output.ToString().TrimEnd();
    }

    /// <summary>
    /// Formats a single file diff.
    /// </summary>
    public string FormatFileDiff(FileDiff fileDiff, int maxLines)
    {
        var output = new StringBuilder();

        // File header
        output.AppendLine($"📄 **{fileDiff.FilePath}** ({fileDiff.TotalModifications} modifications)");

        if (fileDiff.AddedLines > 0 || fileDiff.RemovedLines > 0)
        {
            output.Append("   ");
            if (fileDiff.AddedLines > 0)
            {
                output.Append($"**+{fileDiff.AddedLines}** additions");
            }

            if (fileDiff.AddedLines > 0 && fileDiff.RemovedLines > 0)
            {
                output.Append(", ");
            }

            if (fileDiff.RemovedLines > 0)
            {
                output.Append($"**-{fileDiff.RemovedLines}** deletions");
            }

            output.AppendLine();
        }

        output.AppendLine();

        var totalChangedLines = 0;
        var truncated = false;

        foreach (var hunk in fileDiff.Hunks)
        {
            var hunkChangedLines = hunk.Lines.Count(l => l.Type != DiffLineType.Context);

            if (totalChangedLines + hunkChangedLines > maxLines && totalChangedLines > 0)
            {
                truncated = true;
                break;
            }

            // Hunk header
            output.AppendLine($"  Lines {hunk.NewStart}-{hunk.NewStart + hunk.NewCount - 1}:");
            output.AppendLine();

            // Format diff lines
            output.AppendLine("```diff");

            foreach (var line in hunk.Lines)
            {
                if (line.Type != DiffLineType.Context && totalChangedLines >= maxLines)
                {
                    truncated = true;
                    break;
                }

                switch (line.Type)
                {
                    case DiffLineType.Added:
                        output.AppendLine($"+ {line.LineNumber,4}: {line.Content}");
                        totalChangedLines++;
                        break;
                    case DiffLineType.Removed:
                        output.AppendLine($"- {line.LineNumber,4}: {line.Content}");
                        totalChangedLines++;
                        break;
                    case DiffLineType.Context:
                        output.AppendLine($"  {line.LineNumber,4}: {line.Content}");
                        break;
                }
            }

            output.AppendLine("```");
            output.AppendLine();

            if (truncated)
            {
                break;
            }
        }

        if (truncated)
        {
            var remainingChanges = fileDiff.TotalModifications - totalChangedLines;
            output.AppendLine($"  *... and {remainingChanges} more changes (total: {fileDiff.TotalModifications} lines modified)*");
        }

        return output.ToString().TrimEnd();
    }
}
