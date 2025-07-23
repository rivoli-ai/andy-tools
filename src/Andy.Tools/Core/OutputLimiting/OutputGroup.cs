namespace Andy.Tools.Core.OutputLimiting;

/// <summary>
/// Represents a group in the output summary.
/// </summary>
public class OutputGroup
{
    /// <summary>
    /// Name of the group.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Count of items in the group.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Sample items from the group.
    /// </summary>
    public List<string> SampleItems { get; set; } = new();
}
