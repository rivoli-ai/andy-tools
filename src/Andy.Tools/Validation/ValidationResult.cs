namespace Andy.Tools.Validation;

/// <summary>
/// Represents a validation result.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets or sets whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation errors.
    /// </summary>
    public IList<ValidationError> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets warnings that don't prevent execution.
    /// </summary>
    public IList<ValidationWarning> Warnings { get; set; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>A successful validation result.</returns>
    public static ValidationResult Success(IList<ValidationWarning>? warnings = null)
        => new() { IsValid = true, Warnings = warnings ?? [] };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>A failed validation result.</returns>
    public static ValidationResult Failure(IList<ValidationError> errors, IList<ValidationWarning>? warnings = null)
        => new() { IsValid = false, Errors = errors, Warnings = warnings ?? [] };

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    /// <param name="error">The validation error.</param>
    /// <returns>A failed validation result.</returns>
    public static ValidationResult Failure(ValidationError error)
        => new() { IsValid = false, Errors = [error] };
}
