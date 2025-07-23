namespace Andy.Tools.Core.OutputLimiting;

/// <summary>
/// Context for output limiting.
/// </summary>
public class OutputLimitContext
{
    /// <summary>
    /// Maximum allowed size in characters.
    /// </summary>
    public int? MaxCharacters { get; set; }

    /// <summary>
    /// Maximum allowed size in bytes.
    /// </summary>
    public long? MaxBytes { get; set; }

    /// <summary>
    /// Maximum number of items (for lists).
    /// </summary>
    public int? MaxItems { get; set; }

    /// <summary>
    /// Maximum lines (for text content).
    /// </summary>
    public int? MaxLines { get; set; }

    /// <summary>
    /// Whether to include summary information.
    /// </summary>
    public bool IncludeSummary { get; set; } = true;

    /// <summary>
    /// Whether to provide suggestions.
    /// </summary>
    public bool ProvideSuggestions { get; set; } = true;

    /// <summary>
    /// Tool-specific context data.
    /// </summary>
    public Dictionary<string, object> ToolContext { get; set; } = new();
}
