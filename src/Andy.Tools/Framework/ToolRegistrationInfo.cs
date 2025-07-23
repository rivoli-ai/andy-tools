namespace Andy.Tools.Framework;

/// <summary>
/// Information about a tool registration for dependency injection.
/// </summary>
public class ToolRegistrationInfo
{
    /// <summary>
    /// Gets or sets the tool type.
    /// </summary>
    public Type ToolType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the configuration for the tool.
    /// </summary>
    public Dictionary<string, object?> Configuration { get; set; } = [];
}
