namespace Andy.Tools.Core.OutputLimiting;

/// <summary>
/// Truncation strategies.
/// </summary>
public enum TruncationStrategy
{
    /// <summary>
    /// Simple truncation at limit.
    /// </summary>
    Simple,

    /// <summary>
    /// Intelligent truncation with summaries.
    /// </summary>
    Intelligent,

    /// <summary>
    /// Summarize without showing raw data.
    /// </summary>
    SummarizeOnly
}
