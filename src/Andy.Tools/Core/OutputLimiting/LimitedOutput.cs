namespace Andy.Tools.Core.OutputLimiting;

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
