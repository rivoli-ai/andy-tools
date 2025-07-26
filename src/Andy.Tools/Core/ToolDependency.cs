namespace Andy.Tools.Core;

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
