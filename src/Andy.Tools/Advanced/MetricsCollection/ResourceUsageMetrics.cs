namespace Andy.Tools.Advanced.MetricsCollection;

/// <summary>
/// Resource usage metrics.
/// </summary>
public class ResourceUsageMetrics
{
    /// <summary>
    /// Gets or sets CPU usage percentage.
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Gets or sets memory usage in bytes.
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets disk I/O read bytes.
    /// </summary>
    public long DiskReadBytes { get; set; }

    /// <summary>
    /// Gets or sets disk I/O write bytes.
    /// </summary>
    public long DiskWriteBytes { get; set; }

    /// <summary>
    /// Gets or sets network I/O sent bytes.
    /// </summary>
    public long NetworkSentBytes { get; set; }

    /// <summary>
    /// Gets or sets network I/O received bytes.
    /// </summary>
    public long NetworkReceivedBytes { get; set; }
}
