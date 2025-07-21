namespace Andy.Tools.Core;

/// <summary>
/// Represents metadata about a tool parameter.
/// </summary>
public class ToolParameter
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameter description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameter type (string, number, boolean, array, object).
    /// </summary>
    public string Type { get; set; } = "string";

    /// <summary>
    /// Gets or sets whether this parameter is required.
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Gets or sets the default value for this parameter.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the allowed values for this parameter (for enum-like parameters).
    /// </summary>
    public IList<object>? AllowedValues { get; set; }

    /// <summary>
    /// Gets or sets the minimum value (for numeric parameters).
    /// </summary>
    public double? MinValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum value (for numeric parameters).
    /// </summary>
    public double? MaxValue { get; set; }

    /// <summary>
    /// Gets or sets the minimum length (for string/array parameters).
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// Gets or sets the maximum length (for string/array parameters).
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets the regex pattern for string validation.
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Gets or sets examples of valid values for this parameter.
    /// </summary>
    public IList<object>? Examples { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for this parameter.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the JSON schema for complex object parameters.
    /// </summary>
    public object? Schema { get; set; }

    /// <summary>
    /// Gets or sets the format hint for the parameter (e.g., "uri", "email", "date-time").
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets the item type for array parameters.
    /// </summary>
    public ToolParameter? ItemType { get; set; }
}

/// <summary>
/// Represents capabilities required by a tool.
/// </summary>
[Flags]
public enum ToolCapability
{
    /// <summary>No special capabilities required.</summary>
    None = 0,
    /// <summary>Requires file system access.</summary>
    FileSystem = 1 << 0,
    /// <summary>Requires network access.</summary>
    Network = 1 << 1,
    /// <summary>Requires process execution.</summary>
    ProcessExecution = 1 << 2,
    /// <summary>Requires environment variable access.</summary>
    Environment = 1 << 3,
    /// <summary>Requires elevated privileges.</summary>
    Elevated = 1 << 4,
    /// <summary>Tool is potentially destructive.</summary>
    Destructive = 1 << 5,
    /// <summary>Tool performs long-running operations.</summary>
    LongRunning = 1 << 6,
    /// <summary>Tool requires user interaction.</summary>
    Interactive = 1 << 7
}

/// <summary>
/// Represents permission flags required by a tool.
/// </summary>
[Flags]
public enum ToolPermissionFlags
{
    /// <summary>No special permissions required.</summary>
    None = 0,
    /// <summary>Requires file system read access.</summary>
    FileSystemRead = 1 << 0,
    /// <summary>Requires file system write access.</summary>
    FileSystemWrite = 1 << 1,
    /// <summary>Requires network access.</summary>
    Network = 1 << 2,
    /// <summary>Requires process execution permissions.</summary>
    ProcessExecution = 1 << 3,
    /// <summary>Requires system information access.</summary>
    SystemInformation = 1 << 4,
    /// <summary>Requires environment variable access.</summary>
    Environment = 1 << 5,
    /// <summary>Requires elevated privileges.</summary>
    Elevated = 1 << 6
}

/// <summary>
/// Represents the category of a tool.
/// </summary>
public enum ToolCategory
{
    /// <summary>General utility tools.</summary>
    General,
    /// <summary>File system operations.</summary>
    FileSystem,
    /// <summary>Web and network operations.</summary>
    Web,
    /// <summary>Shell and process operations.</summary>
    Shell,
    /// <summary>Text and data processing.</summary>
    TextProcessing,
    /// <summary>Development and debugging tools.</summary>
    Development,
    /// <summary>System administration tools.</summary>
    System,
    /// <summary>Database operations.</summary>
    Database,
    /// <summary>Cloud and DevOps operations.</summary>
    Cloud,
    /// <summary>AI and machine learning tools.</summary>
    AI,
    /// <summary>Security and encryption tools.</summary>
    Security,
    /// <summary>Utility tools.</summary>
    Utility,
    /// <summary>Productivity and task management tools.</summary>
    Productivity,
    /// <summary>Git version control tools.</summary>
    Git
}

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

/// <summary>
/// Represents an example of how to use a tool.
/// </summary>
public class ToolExample
{
    /// <summary>
    /// Gets or sets the name of this example.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of this example.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the input parameters for this example.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = [];

    /// <summary>
    /// Gets or sets the expected output for this example.
    /// </summary>
    public object? ExpectedOutput { get; set; }

    /// <summary>
    /// Gets or sets additional notes about this example.
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Represents a dependency required by a tool.
/// </summary>
public class ToolDependency
{
    /// <summary>
    /// Gets or sets the name of the dependency.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version requirement for this dependency.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets whether this dependency is optional.
    /// </summary>
    public bool Optional { get; set; } = false;

    /// <summary>
    /// Gets or sets the description of this dependency.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the URL where this dependency can be obtained.
    /// </summary>
    public string? Url { get; set; }
}
