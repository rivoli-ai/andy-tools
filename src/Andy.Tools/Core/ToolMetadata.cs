namespace Andy.Tools.Core;

/// <summary>
/// Represents comprehensive metadata about a tool.
/// </summary>
public class ToolMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier for this tool.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the tool.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of what this tool does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of this tool.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the author of this tool.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the category this tool belongs to.
    /// </summary>
    public ToolCategory Category { get; set; } = ToolCategory.General;

    /// <summary>
    /// Gets or sets the tags associated with this tool.
    /// </summary>
    public IList<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the capabilities required by this tool.
    /// </summary>
    public ToolCapability RequiredCapabilities { get; set; } = ToolCapability.None;

    /// <summary>
    /// Gets or sets the permissions required by this tool.
    /// </summary>
    public ToolPermissionFlags RequiredPermissions { get; set; } = ToolPermissionFlags.None;

    /// <summary>
    /// Gets or sets the parameters accepted by this tool.
    /// </summary>
    public IList<ToolParameter> Parameters { get; set; } = [];

    /// <summary>
    /// Gets or sets example usage of this tool.
    /// </summary>
    public IList<ToolExample> Examples { get; set; } = [];

    /// <summary>
    /// Gets or sets the help URL for this tool.
    /// </summary>
    public string? HelpUrl { get; set; }

    /// <summary>
    /// Gets or sets whether this tool is deprecated.
    /// </summary>
    public bool IsDeprecated { get; set; } = false;

    /// <summary>
    /// Gets or sets the deprecation message if the tool is deprecated.
    /// </summary>
    public string? DeprecationMessage { get; set; }

    /// <summary>
    /// Gets or sets whether this tool is experimental.
    /// </summary>
    public bool IsExperimental { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this tool requires confirmation before execution.
    /// </summary>
    public bool RequiresConfirmation { get; set; } = false;

    /// <summary>
    /// Gets or sets the estimated execution time in milliseconds.
    /// </summary>
    public int? EstimatedExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the maximum supported input size in bytes.
    /// </summary>
    public long? MaxInputSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for this tool.
    /// </summary>
    public Dictionary<string, object?> AdditionalMetadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the schema for the tool's output format.
    /// </summary>
    public object? OutputSchema { get; set; }

    /// <summary>
    /// Gets or sets the supported platforms for this tool.
    /// </summary>
    public IList<string> SupportedPlatforms { get; set; } = ["windows", "linux", "macos"];

    /// <summary>
    /// Gets or sets the minimum .NET version required for this tool.
    /// </summary>
    public string? MinimumDotNetVersion { get; set; }

    /// <summary>
    /// Gets or sets the dependencies required by this tool.
    /// </summary>
    public IList<ToolDependency> Dependencies { get; set; } = [];
}
