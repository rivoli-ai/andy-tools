namespace Andy.Tools.Library.Git;

/// <summary>
/// Type of diff line.
/// </summary>
public enum DiffLineType
{
    /// <summary>
    /// Context line (unchanged).
    /// </summary>
    Context,

    /// <summary>
    /// Added line.
    /// </summary>
    Added,

    /// <summary>
    /// Removed line.
    /// </summary>
    Removed
}
