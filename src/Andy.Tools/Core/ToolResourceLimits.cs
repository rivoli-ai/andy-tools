namespace Andy.Tools.Core;

/// <summary>
/// Represents resource limits for tool execution.
/// </summary>
public class ToolResourceLimits
{
    /// <summary>
    /// Gets or sets the maximum execution time in milliseconds.
    /// </summary>
    public int MaxExecutionTimeMs { get; set; } = 30000; // 30 seconds default

    /// <summary>
    /// Gets or sets the maximum memory usage in bytes.
    /// </summary>
    public long MaxMemoryBytes { get; set; } = 100 * 1024 * 1024; // 100MB default

    /// <summary>
    /// Gets or sets the maximum file size for operations in bytes.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB default

    /// <summary>
    /// Gets or sets the maximum number of files that can be processed.
    /// </summary>
    public int MaxFileCount { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum output size in bytes.
    /// </summary>
    public long MaxOutputSizeBytes { get; set; } = 1024 * 1024; // 1MB default
}
