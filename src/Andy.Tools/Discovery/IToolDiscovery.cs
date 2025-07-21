using System.Reflection;
using Andy.Tools.Core;

namespace Andy.Tools.Discovery;

/// <summary>
/// Represents information about a discovered tool.
/// </summary>
public class DiscoveredTool
{
    /// <summary>
    /// Gets or sets the tool type.
    /// </summary>
    public Type ToolType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the source assembly.
    /// </summary>
    public Assembly SourceAssembly { get; set; } = Assembly.GetExecutingAssembly();

    /// <summary>
    /// Gets or sets the source path.
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    /// Gets or sets the discovery source (assembly, directory, plugin, etc.).
    /// </summary>
    public string DiscoverySource { get; set; } = "unknown";

    /// <summary>
    /// Gets or sets when this tool was discovered.
    /// </summary>
    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets additional metadata about the discovery.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this tool passed validation during discovery.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Gets or sets validation errors if the tool is invalid.
    /// </summary>
    public IList<string> ValidationErrors { get; set; } = [];
}

/// <summary>
/// Options for tool discovery.
/// </summary>
public class ToolDiscoveryOptions
{
    /// <summary>
    /// Gets or sets whether to scan the current assembly.
    /// </summary>
    public bool ScanCurrentAssembly { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to scan loaded assemblies.
    /// </summary>
    public bool ScanLoadedAssemblies { get; set; } = true;

    /// <summary>
    /// Gets or sets additional assemblies to scan.
    /// </summary>
    public IList<Assembly> AdditionalAssemblies { get; set; } = [];

    /// <summary>
    /// Gets or sets directories to scan for plugin assemblies.
    /// </summary>
    public IList<string> PluginDirectories { get; set; } = [];

    /// <summary>
    /// Gets or sets file patterns to match for plugin assemblies.
    /// </summary>
    public IList<string> PluginFilePatterns { get; set; } = ["*.dll"];

    /// <summary>
    /// Gets or sets whether to validate discovered tools.
    /// </summary>
    public bool ValidateTools { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include abstract tool classes.
    /// </summary>
    public bool IncludeAbstractTypes { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to include internal tool classes.
    /// </summary>
    public bool IncludeInternalTypes { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum recursion depth for directory scanning.
    /// </summary>
    public int MaxDirectoryDepth { get; set; } = 3;

    /// <summary>
    /// Gets or sets assembly name patterns to exclude.
    /// </summary>
    public IList<string> ExcludeAssemblyPatterns { get; set; } =
    [
        "System.*",
        "Microsoft.*",
        "netstandard",
        "mscorlib"
    ];

    /// <summary>
    /// Gets or sets type name patterns to exclude.
    /// </summary>
    public IList<string> ExcludeTypePatterns { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to load assemblies with reflection-only context.
    /// </summary>
    public bool UseReflectionOnlyLoad { get; set; } = false;
}

/// <summary>
/// Interface for discovering tools from various sources.
/// </summary>
public interface IToolDiscovery
{
    /// <summary>
    /// Discovers tools based on the provided options.
    /// </summary>
    /// <param name="options">Discovery options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of discovered tools.</returns>
    public Task<IReadOnlyList<DiscoveredTool>> DiscoverToolsAsync(ToolDiscoveryOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers tools from a specific assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="validate">Whether to validate discovered tools.</param>
    /// <returns>A list of discovered tools.</returns>
    public IReadOnlyList<DiscoveredTool> DiscoverToolsFromAssembly(Assembly assembly, bool validate = true);

    /// <summary>
    /// Discovers tools from a specific directory.
    /// </summary>
    /// <param name="directoryPath">The directory path to scan.</param>
    /// <param name="filePatterns">File patterns to match.</param>
    /// <param name="recursive">Whether to scan subdirectories.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of discovered tools.</returns>
    public Task<IReadOnlyList<DiscoveredTool>> DiscoverToolsFromDirectoryAsync(string directoryPath, IList<string>? filePatterns = null, bool recursive = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers tools from a specific file.
    /// </summary>
    /// <param name="filePath">The file path to scan.</param>
    /// <param name="validate">Whether to validate discovered tools.</param>
    /// <returns>A list of discovered tools.</returns>
    public Task<IReadOnlyList<DiscoveredTool>> DiscoverToolsFromFileAsync(string filePath, bool validate = true);

    /// <summary>
    /// Gets information about available discovery sources.
    /// </summary>
    /// <returns>Discovery source information.</returns>
    public ToolDiscoveryInfo GetDiscoveryInfo();
}

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
