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
