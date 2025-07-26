using System.Collections;
using Andy.Tools.Core;

namespace Andy.Tools.Library;

/// <summary>
/// Base class for tool implementations providing common functionality.
/// </summary>
public abstract class ToolBase : ITool, IAsyncDisposable
{
    private bool _isDisposed;
    private Dictionary<string, object?>? _configuration;

    /// <inheritdoc />
    public abstract ToolMetadata Metadata { get; }

    /// <summary>
    /// Gets the configuration dictionary.
    /// </summary>
    protected Dictionary<string, object?> Configuration => _configuration ?? [];

    /// <summary>
    /// Gets a value indicating whether the tool is initialized.
    /// </summary>
    protected bool IsInitialized { get; private set; }

    /// <inheritdoc />
    public virtual Task InitializeAsync(Dictionary<string, object?>? configuration = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _configuration = configuration ?? [];
        IsInitialized = true;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            // Validate parameters before execution
            var validationErrors = ValidateParameters(parameters);
            if (validationErrors.Count > 0)
            {
                var errorDict = new Dictionary<string, object?> { ["validation_errors"] = validationErrors };
                return ToolResult.Failure(string.Join(", ", validationErrors), errorDict);
            }

            // Check permissions
            if (!CanExecuteWithPermissions(context.Permissions))
            {
                return ToolResult.Failure("Insufficient permissions to execute tool");
            }

            // Execute the tool
            return await ExecuteInternalAsync(parameters, context);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            return ToolResult.Failure("Tool execution was cancelled");
        }
        catch (Exception ex)
        {
            return ToolResult.Failure($"Tool execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the tool implementation.
    /// </summary>
    /// <param name="parameters">The validated parameters.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>The tool execution result.</returns>
    protected abstract Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context);

    /// <inheritdoc />
    public virtual IList<string> ValidateParameters(Dictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var errors = new List<string>();

        // Validate required parameters
        foreach (var parameter in Metadata.Parameters.Where(p => p.Required))
        {
            if (!parameters.TryGetValue(parameter.Name, out object? value) || value == null)
            {
                errors.Add($"Required parameter '{parameter.Name}' is missing");
            }
        }

        // Validate parameter types and constraints
        foreach (var kvp in parameters)
        {
            var parameter = Metadata.Parameters.FirstOrDefault(p => p.Name == kvp.Key);
            if (parameter != null)
            {
                var paramErrors = ValidateParameter(parameter, kvp.Value);
                errors.AddRange(paramErrors);
            }
        }

        return errors;
    }

    /// <inheritdoc />
    public virtual bool CanExecuteWithPermissions(ToolPermissions permissions)
    {
        // Check if tool requires permissions that are not granted
        var requiredPermissions = Metadata.RequiredPermissions;

        if (requiredPermissions.HasFlag(ToolPermissionFlags.FileSystemRead) && !permissions.FileSystemAccess)
        {
            return false;
        }

        if (requiredPermissions.HasFlag(ToolPermissionFlags.FileSystemWrite) && !permissions.FileSystemAccess)
        {
            return false;
        }

        if (requiredPermissions.HasFlag(ToolPermissionFlags.Network) && !permissions.NetworkAccess)
        {
            return false;
        }

        if (requiredPermissions.HasFlag(ToolPermissionFlags.ProcessExecution) && !permissions.ProcessExecution)
        {
            return false;
        }

        return !requiredPermissions.HasFlag(ToolPermissionFlags.SystemInformation) || permissions.EnvironmentAccess;
    }

    /// <inheritdoc />
    public virtual Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            IsInitialized = false;
            _configuration?.Clear();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates a single parameter value against its definition.
    /// </summary>
    /// <param name="parameter">The parameter definition.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>A list of validation errors.</returns>
    protected virtual IList<string> ValidateParameter(ToolParameter parameter, object? value)
    {
        var errors = new List<string>();

        if (value == null)
        {
            if (parameter.Required)
            {
                errors.Add($"Parameter '{parameter.Name}' cannot be null");
            }

            return errors;
        }

        // Type validation
        if (!IsValidType(value, parameter.Type))
        {
            errors.Add($"Parameter '{parameter.Name}' must be of type {parameter.Type}");
        }

        // Range validation for numeric types
        if (IsNumericType(parameter.Type))
        {
            if (parameter.MinValue.HasValue && Convert.ToDouble(value) < parameter.MinValue.Value)
            {
                errors.Add($"Parameter '{parameter.Name}' must be >= {parameter.MinValue.Value}");
            }

            if (parameter.MaxValue.HasValue && Convert.ToDouble(value) > parameter.MaxValue.Value)
            {
                errors.Add($"Parameter '{parameter.Name}' must be <= {parameter.MaxValue.Value}");
            }
        }

        // String length validation
        if (parameter.Type == "string" && value is string strValue)
        {
            if (parameter.MinLength.HasValue && strValue.Length < parameter.MinLength.Value)
            {
                errors.Add($"Parameter '{parameter.Name}' must be at least {parameter.MinLength.Value} characters");
            }

            if (parameter.MaxLength.HasValue && strValue.Length > parameter.MaxLength.Value)
            {
                errors.Add($"Parameter '{parameter.Name}' must be at most {parameter.MaxLength.Value} characters");
            }
        }

        // Allowed values validation
        if (parameter.AllowedValues?.Count > 0)
        {
            if (!parameter.AllowedValues.Contains(value))
            {
                errors.Add($"Parameter '{parameter.Name}' must be one of: {string.Join(", ", parameter.AllowedValues)}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Gets a typed parameter value with optional default.
    /// </summary>
    /// <typeparam name="T">The parameter type.</typeparam>
    /// <param name="parameters">The parameters dictionary.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="defaultValue">The default value if parameter is missing.</param>
    /// <returns>The typed parameter value.</returns>
    protected static T GetParameter<T>(Dictionary<string, object?> parameters, string name, T defaultValue = default!)
    {
        if (!parameters.TryGetValue(name, out var value) || value == null)
        {
            return defaultValue;
        }

        if (value is T directValue)
        {
            return directValue;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Reports progress through the execution context.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="message">The progress message.</param>
    /// <param name="percentage">Optional progress percentage.</param>
    protected static void ReportProgress(ToolExecutionContext context, string message, double? percentage = null)
    {
        if (percentage.HasValue)
        {
            context.OnProgressWithPercentage?.Invoke(percentage.Value, message);
        }
        else
        {
            context.OnProgress?.Invoke(message);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private void ThrowIfNotInitialized()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("Tool must be initialized before execution");
        }
    }

    private static bool IsValidType(object value, string typeName)
    {
        return typeName.ToLowerInvariant() switch
        {
            "string" => value is string,
            "int" or "integer" or "int32" => value is int or long or double && IsIntegerValue(value),
            "long" or "int64" => value is long or int or double && IsIntegerValue(value),
            "float" or "double" or "number" => value is double or float or int or long,
            "bool" or "boolean" => value is bool,
            "array" or "list" => value is IEnumerable and not string,
            "object" or "dictionary" => value is IDictionary,
            _ => true // Unknown types pass validation
        };
    }

    private static bool IsNumericType(string typeName)
    {
        return typeName.ToLowerInvariant() switch
        {
            "int" or "integer" or "int32" or "long" or "int64" or "float" or "double" or "number" => true,
            _ => false
        };
    }

    private static bool IsIntegerValue(object value)
    {
        if (value is int or long)
        {
            return true;
        }

        if (value is double d)
        {
            return Math.Abs(d % 1) < double.Epsilon;
        }

        return value is float f ? Math.Abs(f % 1) < float.Epsilon : false;
    }

    /// <summary>
    /// Releases unmanaged resources.
    /// </summary>
    /// <returns>A task representing the disposal operation.</returns>
    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return new ValueTask(DisposeAsync());
    }
}
