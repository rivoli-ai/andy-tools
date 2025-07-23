namespace Andy.Tools.Observability;

/// <summary>
/// Options for exporting observability data.
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// Gets or sets the start time for the export.
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time for the export.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets specific tool IDs to include.
    /// </summary>
    public List<string>? ToolIds { get; set; }

    /// <summary>
    /// Gets or sets whether to include raw data.
    /// </summary>
    public bool IncludeRawData { get; set; }

    /// <summary>
    /// Gets or sets whether to include aggregated statistics.
    /// </summary>
    public bool IncludeStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include security events.
    /// </summary>
    public bool IncludeSecurityEvents { get; set; } = true;
}
