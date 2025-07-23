namespace Andy.Tools.Core;

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
