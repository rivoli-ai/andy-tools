namespace Andy.Tools.Validation;

/// <summary>
/// Represents the severity of a validation issue.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Information only.</summary>
    Info,
    /// <summary>Warning that doesn't prevent execution.</summary>
    Warning,
    /// <summary>Error that prevents execution.</summary>
    Error,
    /// <summary>Critical error that prevents execution.</summary>
    Critical
}
