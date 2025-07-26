namespace Andy.Tools.Core;

/// <summary>
/// Represents the result of a tool execution.
/// </summary>
public class ToolResult
{
    /// <summary>
    /// Gets or sets whether the tool execution was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the result data from the tool execution.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets the error message if the execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the execution.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the execution duration in milliseconds.
    /// </summary>
    public double? DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the result message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets the result output (alias for Data for compatibility).
    /// </summary>
    public object? Output => Data;

    /// <summary>
    /// Gets the error message (alias for ErrorMessage for compatibility).
    /// </summary>
    public string? Error => ErrorMessage;

    /// <summary>
    /// Gets the execution time in milliseconds (alias for DurationMs for compatibility).
    /// </summary>
    public double ExecutionTimeMs => DurationMs ?? 0.0;

    /// <summary>
    /// Gets the list of errors (converted from ErrorMessage for compatibility).
    /// </summary>
    public List<string> Errors => string.IsNullOrEmpty(ErrorMessage) ? [] : [ErrorMessage];

    /// <summary>
    /// Creates a successful tool result.
    /// </summary>
    /// <param name="data">The result data.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <returns>A successful tool result.</returns>
    public static ToolResult Success(object? data = null, Dictionary<string, object?>? metadata = null)
        => new() { IsSuccessful = true, Data = data, Metadata = metadata ?? [] };

    /// <summary>
    /// Creates a failed tool result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <returns>A failed tool result.</returns>
    public static ToolResult Failure(string errorMessage, Dictionary<string, object?>? metadata = null)
        => new() { IsSuccessful = false, ErrorMessage = errorMessage, Metadata = metadata ?? [] };

    /// <summary>
    /// Creates a failed tool result from an exception.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <returns>A failed tool result.</returns>
    public static ToolResult Failure(Exception exception, Dictionary<string, object?>? metadata = null)
        => new() { IsSuccessful = false, ErrorMessage = exception.Message, Metadata = metadata ?? [] };
}
