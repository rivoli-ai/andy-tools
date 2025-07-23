namespace Andy.Tools.Library.Git;

/// <summary>
/// Represents a diff hunk.
/// </summary>
public class DiffHunk
{
    /// <summary>
    /// Gets or sets the starting line number in the old file.
    /// </summary>
    public int OldStart { get; set; }

    /// <summary>
    /// Gets or sets the number of lines in the old file.
    /// </summary>
    public int OldCount { get; set; }

    /// <summary>
    /// Gets or sets the starting line number in the new file.
    /// </summary>
    public int NewStart { get; set; }

    /// <summary>
    /// Gets or sets the number of lines in the new file.
    /// </summary>
    public int NewCount { get; set; }

    /// <summary>
    /// Gets or sets the diff lines in this hunk.
    /// </summary>
    public List<DiffLine> Lines { get; set; } = new();
}
