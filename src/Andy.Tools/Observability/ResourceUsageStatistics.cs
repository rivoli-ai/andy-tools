namespace Andy.Tools.Observability;

/// <summary>
/// Resource usage statistics.
/// </summary>
public class ResourceUsageStatistics
{
    /// <summary>
    /// Gets or sets average memory usage in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets average CPU usage percentage.
    /// </summary>
    public double AverageCpuUsage { get; set; }

    /// <summary>
    /// Gets or sets peak CPU usage percentage.
    /// </summary>
    public double PeakCpuUsage { get; set; }

    /// <summary>
    /// Gets or sets total file system operations.
    /// </summary>
    public long FileSystemOperations { get; set; }

    /// <summary>
    /// Gets or sets total network operations.
    /// </summary>
    public long NetworkOperations { get; set; }
}
