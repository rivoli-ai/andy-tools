namespace Andy.Tools.Observability;

/// <summary>
/// Usage pattern information.
/// </summary>
public class UsagePattern
{
    /// <summary>
    /// Gets or sets the pattern name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pattern description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the occurrence count.
    /// </summary>
    public int Occurrences { get; set; }

    /// <summary>
    /// Gets or sets the tools involved.
    /// </summary>
    public List<string> ToolIds { get; set; } = new();
}
