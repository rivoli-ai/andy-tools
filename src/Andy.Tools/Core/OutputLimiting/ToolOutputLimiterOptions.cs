namespace Andy.Tools.Core.OutputLimiting;

/// <summary>
/// Configuration options for tool output limiting.
/// </summary>
public class ToolOutputLimiterOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ToolOutput";

    /// <summary>
    /// Maximum characters for general output.
    /// </summary>
    public int MaxOutputCharacters { get; set; } = 50_000;

    /// <summary>
    /// Maximum characters for file list output.
    /// </summary>
    public int MaxFileListCharacters { get; set; } = 50_000;

    /// <summary>
    /// Maximum number of entries in file lists.
    /// </summary>
    public int MaxFileListEntries { get; set; } = 1_000;

    /// <summary>
    /// Maximum characters for file content.
    /// </summary>
    public int MaxFileContentCharacters { get; set; } = 100_000;

    /// <summary>
    /// Maximum lines per file.
    /// </summary>
    public int MaxLinesPerFile { get; set; } = 1_000;

    /// <summary>
    /// Enable smart summaries for truncated output.
    /// </summary>
    public bool EnableSmartSummaries { get; set; } = true;

    /// <summary>
    /// Show truncation warnings.
    /// </summary>
    public bool ShowTruncationWarning { get; set; } = true;

    /// <summary>
    /// Include statistics in summaries.
    /// </summary>
    public bool IncludeStatistics { get; set; } = true;

    /// <summary>
    /// Default truncation strategy.
    /// </summary>
    public TruncationStrategy DefaultStrategy { get; set; } = TruncationStrategy.Intelligent;
}
