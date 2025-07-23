namespace Andy.Tools.Core.OutputLimiting;

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
