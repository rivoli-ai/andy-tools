namespace Andy.Tools.Library.Git;

/// <summary>
/// Interface for formatting git diff output.
/// </summary>
public interface IGitDiffFormatter
{
    /// <summary>
    /// Formats the stats summary section of git diff output.
    /// </summary>
    /// <param name="statsContent">The stats content to format.</param>
    /// <returns>Formatted stats summary.</returns>
    public string FormatStatsSummary(string statsContent);

    /// <summary>
    /// Formats a single file diff.
    /// </summary>
    /// <param name="fileDiff">The file diff to format.</param>
    /// <param name="maxLines">Maximum number of lines to display.</param>
    /// <returns>Formatted file diff.</returns>
    public string FormatFileDiff(FileDiff fileDiff, int maxLines);
}
