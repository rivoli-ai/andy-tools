namespace Andy.Tools.Validation;

/// <summary>
/// Represents a validation warning.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ValidationWarning"/> class.
/// </remarks>
/// <param name="code">The warning code.</param>
/// <param name="message">The warning message.</param>
/// <param name="propertyPath">The property path.</param>
public class ValidationWarning(string code, string message, string? propertyPath = null)
{
    /// <summary>
    /// Gets or sets the warning code.
    /// </summary>
    public string Code { get; set; } = code;

    /// <summary>
    /// Gets or sets the warning message.
    /// </summary>
    public string Message { get; set; } = message;

    /// <summary>
    /// Gets or sets the property path that caused the warning.
    /// </summary>
    public string? PropertyPath { get; set; } = propertyPath;
}
