using System.Reflection;

namespace Andy.Tools.Discovery;

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
