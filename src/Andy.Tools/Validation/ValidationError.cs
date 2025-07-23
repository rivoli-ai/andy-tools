namespace Andy.Tools.Validation;

/// <summary>
/// Represents a validation error.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ValidationError"/> class.
/// </remarks>
/// <param name="code">The error code.</param>
/// <param name="message">The error message.</param>
/// <param name="propertyPath">The property path.</param>
/// <param name="attemptedValue">The attempted value.</param>
public class ValidationError(string code, string message, string? propertyPath = null, object? attemptedValue = null)
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string Code { get; set; } = code;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = message;

    /// <summary>
    /// Gets or sets the property path that caused the error.
    /// </summary>
    public string? PropertyPath { get; set; } = propertyPath;

    /// <summary>
    /// Gets or sets the attempted value that caused the error.
    /// </summary>
    public object? AttemptedValue { get; set; } = attemptedValue;

    /// <summary>
    /// Gets or sets the severity of this error.
    /// </summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
}
