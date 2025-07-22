using System.Text.Json;

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

/// <summary>
/// Represents the execution context for a tool.
/// </summary>
public class ToolExecutionContext
{
    /// <summary>
    /// Gets or sets the correlation ID for tracking this execution.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID executing the tool.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the session ID for this execution.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the working directory for tool execution.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets environment variables for tool execution.
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = [];

    /// <summary>
    /// Gets or sets the permissions granted for this execution.
    /// </summary>
    public ToolPermissions Permissions { get; set; } = new();

    /// <summary>
    /// Gets or sets the resource limits for this execution.
    /// </summary>
    public ToolResourceLimits ResourceLimits { get; set; } = new();

    /// <summary>
    /// Gets or sets the cancellation token for this execution.
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = default;

    /// <summary>
    /// Gets or sets additional context data.
    /// </summary>
    public Dictionary<string, object?> AdditionalData { get; set; } = [];

    /// <summary>
    /// Gets or sets the progress callback for reporting progress messages.
    /// </summary>
    public Action<string>? OnProgress { get; set; }

    /// <summary>
    /// Gets or sets the progress callback for reporting progress with percentage.
    /// </summary>
    public Action<double, string>? OnProgressWithPercentage { get; set; }
}

/// <summary>
/// Represents permissions granted to a tool for execution.
/// </summary>
public class ToolPermissions
{
    /// <summary>
    /// Gets or sets whether the tool can access the file system.
    /// </summary>
    public bool FileSystemAccess { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the tool can make network requests.
    /// </summary>
    public bool NetworkAccess { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the tool can execute processes.
    /// </summary>
    public bool ProcessExecution { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the tool can access environment variables.
    /// </summary>
    public bool EnvironmentAccess { get; set; } = true;

    /// <summary>
    /// Gets or sets the allowed file system paths (null means all paths allowed if FileSystemAccess is true).
    /// </summary>
    public HashSet<string>? AllowedPaths { get; set; }

    /// <summary>
    /// Gets or sets the blocked file system paths (these paths are always blocked regardless of AllowedPaths).
    /// </summary>
    public HashSet<string>? BlockedPaths { get; set; }

    /// <summary>
    /// Gets or sets the allowed network hosts (null means all hosts allowed if NetworkAccess is true).
    /// </summary>
    public HashSet<string>? AllowedHosts { get; set; }

    /// <summary>
    /// Gets or sets the blocked network hosts (these hosts are always blocked regardless of AllowedHosts).
    /// </summary>
    public HashSet<string>? BlockedHosts { get; set; }

    /// <summary>
    /// Gets or sets custom permissions specific to tools.
    /// </summary>
    public Dictionary<string, object?> CustomPermissions { get; set; } = [];

    /// <summary>
    /// Gets or sets tool-specific permissions that override global permissions.
    /// </summary>
    public Dictionary<string, bool> ToolSpecificPermissions { get; set; } = [];

    /// <summary>
    /// Gets or sets the name of this permission profile.
    /// </summary>
    public string ProfileName { get; set; } = "default";

    /// <summary>
    /// Gets or sets the description of this permission profile.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets when this permission profile was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets when this permission profile was last modified.
    /// </summary>
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a copy of the current permissions with a new profile name.
    /// </summary>
    /// <param name="newProfileName">The new profile name.</param>
    /// <returns>A copy of the current permissions with the new profile name.</returns>
    public ToolPermissions Clone(string newProfileName)
    {
        return new ToolPermissions
        {
            FileSystemAccess = FileSystemAccess,
            NetworkAccess = NetworkAccess,
            ProcessExecution = ProcessExecution,
            EnvironmentAccess = EnvironmentAccess,
            AllowedPaths = AllowedPaths != null ? new HashSet<string>(AllowedPaths) : null,
            BlockedPaths = BlockedPaths != null ? new HashSet<string>(BlockedPaths) : null,
            AllowedHosts = AllowedHosts != null ? new HashSet<string>(AllowedHosts) : null,
            BlockedHosts = BlockedHosts != null ? new HashSet<string>(BlockedHosts) : null,
            CustomPermissions = new Dictionary<string, object?>(CustomPermissions),
            ToolSpecificPermissions = new Dictionary<string, bool>(ToolSpecificPermissions),
            ProfileName = newProfileName,
            Description = Description,
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a copy of the current permissions.
    /// </summary>
    /// <returns>A copy of the current permissions.</returns>
    public ToolPermissions Clone()
    {
        return Clone(ProfileName);
    }
}

/// <summary>
/// Represents resource limits for tool execution.
/// </summary>
public class ToolResourceLimits
{
    /// <summary>
    /// Gets or sets the maximum execution time in milliseconds.
    /// </summary>
    public int MaxExecutionTimeMs { get; set; } = 30000; // 30 seconds default

    /// <summary>
    /// Gets or sets the maximum memory usage in bytes.
    /// </summary>
    public long MaxMemoryBytes { get; set; } = 100 * 1024 * 1024; // 100MB default

    /// <summary>
    /// Gets or sets the maximum file size for operations in bytes.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB default

    /// <summary>
    /// Gets or sets the maximum number of files that can be processed.
    /// </summary>
    public int MaxFileCount { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum output size in bytes.
    /// </summary>
    public long MaxOutputSizeBytes { get; set; } = 1024 * 1024; // 1MB default
}

/// <summary>
/// Base interface for all tools in the Andy system.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Gets the metadata describing this tool.
    /// </summary>
    public ToolMetadata Metadata { get; }

    /// <summary>
    /// Initializes the tool with the given configuration.
    /// </summary>
    /// <param name="configuration">The tool configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the initialization operation.</returns>
    public Task InitializeAsync(Dictionary<string, object?>? configuration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the tool with the given parameters and context.
    /// </summary>
    /// <param name="parameters">The parameters for tool execution.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A task representing the tool execution result.</returns>
    public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> parameters, ToolExecutionContext context);

    /// <summary>
    /// Validates the given parameters for this tool.
    /// </summary>
    /// <param name="parameters">The parameters to validate.</param>
    /// <returns>A list of validation errors, or empty if valid.</returns>
    public IList<string> ValidateParameters(Dictionary<string, object?> parameters);

    /// <summary>
    /// Determines if this tool can execute with the given permissions.
    /// </summary>
    /// <param name="permissions">The permissions to check.</param>
    /// <returns>True if the tool can execute with the given permissions.</returns>
    public bool CanExecuteWithPermissions(ToolPermissions permissions);

    /// <summary>
    /// Disposes resources used by the tool.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the disposal operation.</returns>
    public Task DisposeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Base abstract class for tools providing common functionality.
/// </summary>
public abstract class ToolBase : ITool
{
    /// <inheritdoc />
    public abstract ToolMetadata Metadata { get; }

    /// <summary>
    /// Gets whether the tool has been initialized.
    /// </summary>
    protected bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets whether the tool has been disposed.
    /// </summary>
    protected bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public virtual Task InitializeAsync(Dictionary<string, object?>? configuration = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        IsInitialized = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (!IsInitialized)
        {
            throw new InvalidOperationException("Tool must be initialized before execution");
        }

        // Validate parameters
        var validationErrors = ValidateParameters(parameters);
        if (validationErrors.Count > 0)
        {
            return ToolResult.Failure($"Parameter validation failed: {string.Join(", ", validationErrors)}");
        }

        // Check permissions
        if (!CanExecuteWithPermissions(context.Permissions))
        {
            return ToolResult.Failure("Insufficient permissions to execute this tool");
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await ExecuteInternalAsync(parameters, context);
            stopwatch.Stop();
            result.DurationMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return ToolResult.Failure("Tool execution was cancelled", new Dictionary<string, object?>
            {
                ["duration_ms"] = stopwatch.Elapsed.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Failure(ex, new Dictionary<string, object?>
            {
                ["duration_ms"] = stopwatch.Elapsed.TotalMilliseconds
            });
        }
    }

    /// <summary>
    /// Executes the tool logic. Derived classes must implement this method.
    /// </summary>
    /// <param name="parameters">The parameters for tool execution.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A task representing the tool execution result.</returns>
    protected abstract Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context);

    /// <inheritdoc />
    public virtual IList<string> ValidateParameters(Dictionary<string, object?> parameters)
    {
        var errors = new List<string>();

        foreach (var param in Metadata.Parameters)
        {
            if (param.Required && !parameters.ContainsKey(param.Name))
            {
                errors.Add($"Required parameter '{param.Name}' is missing");
                continue;
            }

            if (parameters.TryGetValue(param.Name, out var value) && value != null)
            {
                // Validate parameter type and constraints
                var paramErrors = ValidateParameter(param, value);
                errors.AddRange(paramErrors);
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates a single parameter value against its definition.
    /// </summary>
    /// <param name="parameter">The parameter definition.</param>
    /// <param name="value">The value to validate.</param>
    /// <returns>A list of validation errors.</returns>
    protected virtual IList<string> ValidateParameter(ToolParameter parameter, object value)
    {
        var errors = new List<string>();

        // Basic type validation would go here
        // This is a simplified implementation
        if (parameter.Type == "string" && value is not string)
        {
            errors.Add($"Parameter '{parameter.Name}' must be a string");
        }
        else if (parameter.Type == "number" && value is not (int or long or float or double or decimal))
        {
            errors.Add($"Parameter '{parameter.Name}' must be a number");
        }
        else if (parameter.Type == "boolean" && value is not bool)
        {
            errors.Add($"Parameter '{parameter.Name}' must be a boolean");
        }

        return errors;
    }

    /// <inheritdoc />
    public virtual bool CanExecuteWithPermissions(ToolPermissions permissions)
    {
        // Default implementation - derived classes can override for specific permission requirements
        return true;
    }

    /// <inheritdoc />
    public virtual Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
        }

        return Task.CompletedTask;
    }
}
