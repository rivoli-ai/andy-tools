namespace Andy.Tools.Observability;

/// <summary>
/// Tool combination information.
/// </summary>
public class ToolCombination
{
    /// <summary>
    /// Gets or sets the tool IDs in the combination.
    /// </summary>
    public List<string> ToolIds { get; set; } = new();

    /// <summary>
    /// Gets or sets how often this combination occurs.
    /// </summary>
    public int Frequency { get; set; }

    /// <summary>
    /// Gets or sets the average time between executions.
    /// </summary>
    public TimeSpan AverageTimeBetween { get; set; }
}
