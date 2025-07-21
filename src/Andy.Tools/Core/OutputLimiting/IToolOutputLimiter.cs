namespace Andy.Tools.Core.OutputLimiting;

/// <summary>
/// Interface for limiting and truncating tool output to prevent context overflow.
/// </summary>
public interface IToolOutputLimiter
{
    /// <summary>
    /// Limits the output based on the specified type and configuration.
    /// </summary>
    /// <param name="output">The output to limit.</param>
    /// <param name="outputType">The type of output being limited.</param>
    /// <param name="context">Additional context for the truncation.</param>
    /// <returns>A limited output result.</returns>
    public LimitedOutput LimitOutput(object output, OutputType outputType, OutputLimitContext? context = null);

    /// <summary>
    /// Checks if the output needs limiting.
    /// </summary>
    /// <param name="output">The output to check.</param>
    /// <param name="outputType">The type of output.</param>
    /// <returns>True if limiting is needed.</returns>
    public bool NeedsLimiting(object output, OutputType outputType);
}

/// <summary>
/// Types of output that can be limited.
/// </summary>
public enum OutputType
{
    /// <summary>
    /// Generic text output.
    /// </summary>
    Text,

    /// <summary>
    /// File listing output.
    /// </summary>
    FileList,

    /// <summary>
    /// File content output.
    /// </summary>
    FileContent,

    /// <summary>
    /// Directory tree output.
    /// </summary>
    DirectoryTree,

    /// <summary>
    /// JSON or structured data output.
    /// </summary>
    StructuredData,

    /// <summary>
    /// Log or console output.
    /// </summary>
    Logs
}

/// <summary>
/// Result of limiting tool output.
/// </summary>
public class LimitedOutput
{
    /// <summary>
    /// The limited output content.
    /// </summary>
    public object Content { get; set; } = null!;

    /// <summary>
    /// Whether the output was truncated.
    /// </summary>
    public bool WasTruncated { get; set; }

    /// <summary>
    /// Original size of the output before truncation.
    /// </summary>
    public long OriginalSize { get; set; }

    /// <summary>
    /// Size after truncation.
    /// </summary>
    public long TruncatedSize { get; set; }

    /// <summary>
    /// Reason for truncation.
    /// </summary>
    public string? TruncationReason { get; set; }

    /// <summary>
    /// Summary information about the truncated content.
    /// </summary>
    public OutputSummary? Summary { get; set; }

    /// <summary>
    /// Suggestions for alternative queries.
    /// </summary>
    public List<string> Suggestions { get; set; } = new();

    /// <summary>
    /// Gets a formatted message about the truncation.
    /// </summary>
    public string GetTruncationMessage()
    {
        if (!WasTruncated)
        {
            return string.Empty;
        }

        var message = $"\n⚠️ Output truncated from {OriginalSize:N0} to {TruncatedSize:N0} characters";
        if (!string.IsNullOrEmpty(TruncationReason))
        {
            message += $"\nReason: {TruncationReason}";
        }

        if (Suggestions.Count > 0)
        {
            message += "\n\nSuggestions:";
            foreach (var suggestion in Suggestions)
            {
                message += $"\n  • {suggestion}";
            }
        }

        return message;
    }
}

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
