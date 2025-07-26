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
