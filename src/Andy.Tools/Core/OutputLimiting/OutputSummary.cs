namespace Andy.Tools.Core.OutputLimiting;

/// <summary>
/// Summary information about truncated output.
/// </summary>
public class OutputSummary
{
    /// <summary>
    /// Total count of items.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Count of items shown.
    /// </summary>
    public int ShownCount { get; set; }

    /// <summary>
    /// Statistical information.
    /// </summary>
    public Dictionary<string, object> Statistics { get; set; } = new();

    /// <summary>
    /// Groups or categories in the output.
    /// </summary>
    public List<OutputGroup> Groups { get; set; } = new();
}
