namespace Andy.Tools.Core;

/// <summary>
/// Represents resource usage statistics for a tool execution.
/// </summary>
public class ToolResourceUsage
{
    /// <summary>
    /// Gets or sets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the average memory usage in bytes.
    /// </summary>
    public long AverageMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the total CPU time in milliseconds.
    /// </summary>
    public double CpuTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the number of files accessed.
    /// </summary>
    public int FilesAccessed { get; set; }

    /// <summary>
    /// Gets or sets the total bytes read from files.
    /// </summary>
    public long BytesRead { get; set; }

    /// <summary>
    /// Gets or sets the total bytes written to files.
    /// </summary>
    public long BytesWritten { get; set; }

    /// <summary>
    /// Gets or sets the number of network requests made.
    /// </summary>
    public int NetworkRequests { get; set; }

    /// <summary>
    /// Gets or sets the total bytes sent over the network.
    /// </summary>
    public long NetworkBytesSent { get; set; }

    /// <summary>
    /// Gets or sets the total bytes received over the network.
    /// </summary>
    public long NetworkBytesReceived { get; set; }

    /// <summary>
    /// Gets or sets the number of processes started.
    /// </summary>
    public int ProcessesStarted { get; set; }
}
