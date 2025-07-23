namespace Andy.Tools.Observability;

/// <summary>
/// Tool usage analytics.
/// </summary>
public class ToolUsageAnalytics
{
    /// <summary>
    /// Gets or sets tool usage by ID.
    /// </summary>
    public Dictionary<string, ToolUsageInfo> ToolUsage { get; set; } = new();

    /// <summary>
    /// Gets or sets usage patterns.
    /// </summary>
    public List<UsagePattern> UsagePatterns { get; set; } = new();

    /// <summary>
    /// Gets or sets peak usage times.
    /// </summary>
    public List<PeakUsageTime> PeakUsageTimes { get; set; } = new();

    /// <summary>
    /// Gets or sets tool combinations frequently used together.
    /// </summary>
    public List<ToolCombination> FrequentCombinations { get; set; } = new();

    /// <summary>
    /// Gets or sets the time period for analytics.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time for analytics.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }
}
