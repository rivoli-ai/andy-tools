namespace Andy.Tools.Library.Git;

/// <summary>
/// Represents a file diff.
/// </summary>
public class FileDiff
{
    /// <summary>
    /// Gets or sets the file path.
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// Gets or sets the diff hunks.
    /// </summary>
    public List<DiffHunk> Hunks { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of added lines.
    /// </summary>
    public int AddedLines { get; set; }

    /// <summary>
    /// Gets or sets the number of removed lines.
    /// </summary>
    public int RemovedLines { get; set; }

    /// <summary>
    /// Gets the total number of modifications.
    /// </summary>
    public int TotalModifications => AddedLines + RemovedLines;
}
