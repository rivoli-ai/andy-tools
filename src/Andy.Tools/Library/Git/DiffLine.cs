namespace Andy.Tools.Library.Git;

/// <summary>
/// Represents a single line in a diff.
/// </summary>
public class DiffLine
{
    /// <summary>
    /// Gets or sets the type of diff line.
    /// </summary>
    public DiffLineType Type { get; set; }

    /// <summary>
    /// Gets or sets the line content.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Gets or sets the line number.
    /// </summary>
    public int LineNumber { get; set; }
}
