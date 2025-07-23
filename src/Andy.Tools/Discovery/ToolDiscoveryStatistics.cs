namespace Andy.Tools.Discovery;

/// <summary>
/// Statistics about a tool discovery operation.
/// </summary>
public class ToolDiscoveryStatistics
{
    /// <summary>
    /// Gets or sets the total number of assemblies scanned.
    /// </summary>
    public int AssembliesScanned { get; set; }

    /// <summary>
    /// Gets or sets the total number of types examined.
    /// </summary>
    public int TypesExamined { get; set; }

    /// <summary>
    /// Gets or sets the number of valid tools discovered.
    /// </summary>
    public int ValidToolsDiscovered { get; set; }

    /// <summary>
    /// Gets or sets the number of invalid tools found.
    /// </summary>
    public int InvalidToolsFound { get; set; }

    /// <summary>
    /// Gets or sets the time taken for discovery.
    /// </summary>
    public TimeSpan DiscoveryDuration { get; set; }

    /// <summary>
    /// Gets or sets errors encountered during discovery.
    /// </summary>
    public IList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets warnings encountered during discovery.
    /// </summary>
    public IList<string> Warnings { get; set; } = [];

    /// <summary>
    /// Gets or sets when this discovery was performed.
    /// </summary>
    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.UtcNow;
}
