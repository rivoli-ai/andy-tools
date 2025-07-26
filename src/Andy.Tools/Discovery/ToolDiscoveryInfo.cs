namespace Andy.Tools.Discovery;

/// <summary>
/// Information about tool discovery capabilities and sources.
/// </summary>
public class ToolDiscoveryInfo
{
    /// <summary>
    /// Gets or sets the number of loaded assemblies available for scanning.
    /// </summary>
    public int LoadedAssembliesCount { get; set; }

    /// <summary>
    /// Gets or sets the list of loaded assembly names.
    /// </summary>
    public IList<string> LoadedAssemblyNames { get; set; } = [];

    /// <summary>
    /// Gets or sets available plugin directories.
    /// </summary>
    public IList<string> AvailablePluginDirectories { get; set; } = [];

    /// <summary>
    /// Gets or sets supported file extensions for plugins.
    /// </summary>
    public IList<string> SupportedFileExtensions { get; set; } = [".dll", ".exe"];

    /// <summary>
    /// Gets or sets the current discovery configuration.
    /// </summary>
    public ToolDiscoveryOptions CurrentOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets statistics about the last discovery run.
    /// </summary>
    public ToolDiscoveryStatistics? LastDiscoveryStats { get; set; }
}
