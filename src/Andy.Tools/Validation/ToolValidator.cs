using Andy.Tools.Core;

namespace Andy.Tools.Validation;

/// <summary>
/// Standard tool validator implementation.
/// </summary>
public class ToolValidator : IToolValidator
{
    /// <inheritdoc />
    public ValidationResult ValidateMetadata(ToolMetadata metadata)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(metadata.Id))
        {
            errors.Add(new ValidationError("METADATA_ID_REQUIRED", "Tool ID is required", nameof(metadata.Id)));
        }

        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            errors.Add(new ValidationError("METADATA_NAME_REQUIRED", "Tool name is required", nameof(metadata.Name)));
        }

        if (string.IsNullOrWhiteSpace(metadata.Description))
        {
            errors.Add(new ValidationError("METADATA_DESCRIPTION_REQUIRED", "Tool description is required", nameof(metadata.Description)));
        }

        // Validate ID format
        if (!string.IsNullOrWhiteSpace(metadata.Id) && !IsValidToolId(metadata.Id))
        {
            errors.Add(new ValidationError("METADATA_ID_INVALID", "Tool ID must contain only letters, numbers, underscores, and hyphens", nameof(metadata.Id), metadata.Id));
        }

        // Validate version format
        if (!string.IsNullOrWhiteSpace(metadata.Version) && !IsValidVersion(metadata.Version))
        {
            errors.Add(new ValidationError("METADATA_VERSION_INVALID", "Tool version must be in semantic versioning format", nameof(metadata.Version), metadata.Version));
        }

        // Validate parameters
        for (int i = 0; i < metadata.Parameters.Count; i++)
        {
            var param = metadata.Parameters[i];
            var paramPath = $"{nameof(metadata.Parameters)}[{i}]";

            if (string.IsNullOrWhiteSpace(param.Name))
            {
                errors.Add(new ValidationError("PARAMETER_NAME_REQUIRED", "Parameter name is required", $"{paramPath}.{nameof(param.Name)}"));
            }

            if (string.IsNullOrWhiteSpace(param.Description))
            {
                warnings.Add(new ValidationWarning("PARAMETER_DESCRIPTION_MISSING", "Parameter description is recommended", $"{paramPath}.{nameof(param.Description)}"));
            }

            if (!IsValidParameterType(param.Type))
            {
                errors.Add(new ValidationError("PARAMETER_TYPE_INVALID", $"Invalid parameter type: {param.Type}", $"{paramPath}.{nameof(param.Type)}", param.Type));
            }
        }

        // Check for duplicate parameter names
        var duplicateParams = metadata.Parameters
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicateParam in duplicateParams)
        {
            errors.Add(new ValidationError("PARAMETER_NAME_DUPLICATE", $"Duplicate parameter name: {duplicateParam}", nameof(metadata.Parameters), duplicateParam));
        }

        // Validate examples
        for (int i = 0; i < metadata.Examples.Count; i++)
        {
            var example = metadata.Examples[i];
            var examplePath = $"{nameof(metadata.Examples)}[{i}]";

            if (string.IsNullOrWhiteSpace(example.Name))
            {
                warnings.Add(new ValidationWarning("EXAMPLE_NAME_MISSING", "Example name is recommended", $"{examplePath}.{nameof(example.Name)}"));
            }

            // Validate example parameters against tool parameters
            var paramValidation = ValidateParameters(example.Parameters, metadata.Parameters);
            if (!paramValidation.IsValid)
            {
                foreach (var error in paramValidation.Errors)
                {
                    errors.Add(new ValidationError("EXAMPLE_PARAMETER_INVALID", $"Example '{example.Name}' has invalid parameters: {error.Message}", $"{examplePath}.{nameof(example.Parameters)}"));
                }
            }
        }

        return errors.Count > 0 ? ValidationResult.Failure(errors, warnings) : ValidationResult.Success(warnings);
    }

    /// <inheritdoc />
    public ValidationResult ValidateParameters(Dictionary<string, object?> parameters, IList<ToolParameter> parameterDefinitions)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Check for required parameters
        foreach (var param in parameterDefinitions.Where(p => p.Required))
        {
            if (!parameters.ContainsKey(param.Name))
            {
                errors.Add(new ValidationError("PARAMETER_REQUIRED", $"Required parameter '{param.Name}' is missing", param.Name));
                continue;
            }

            var value = parameters[param.Name];
            if (value == null)
            {
                errors.Add(new ValidationError("PARAMETER_NULL", $"Required parameter '{param.Name}' cannot be null", param.Name, value));
            }
        }

        // Validate each provided parameter
        foreach (var kvp in parameters)
        {
            var paramDef = parameterDefinitions.FirstOrDefault(p => string.Equals(p.Name, kvp.Key, StringComparison.OrdinalIgnoreCase));
            if (paramDef == null)
            {
                warnings.Add(new ValidationWarning("PARAMETER_UNKNOWN", $"Unknown parameter '{kvp.Key}' will be ignored", kvp.Key));
                continue;
            }

            if (kvp.Value != null)
            {
                var paramErrors = ValidateParameterValue(paramDef, kvp.Value);
                errors.AddRange(paramErrors);
            }
        }

        return errors.Count > 0 ? ValidationResult.Failure(errors, warnings) : ValidationResult.Success(warnings);
    }

    /// <inheritdoc />
    public ValidationResult ValidatePermissions(ToolCapability requiredCapabilities, ToolPermissions grantedPermissions)
    {
        var errors = new List<ValidationError>();

        if (requiredCapabilities.HasFlag(ToolCapability.FileSystem) && !grantedPermissions.FileSystemAccess)
        {
            errors.Add(new ValidationError("PERMISSION_FILESYSTEM_DENIED", "Tool requires file system access but it is not granted"));
        }

        if (requiredCapabilities.HasFlag(ToolCapability.Network) && !grantedPermissions.NetworkAccess)
        {
            errors.Add(new ValidationError("PERMISSION_NETWORK_DENIED", "Tool requires network access but it is not granted"));
        }

        if (requiredCapabilities.HasFlag(ToolCapability.ProcessExecution) && !grantedPermissions.ProcessExecution)
        {
            errors.Add(new ValidationError("PERMISSION_PROCESS_DENIED", "Tool requires process execution but it is not granted"));
        }

        if (requiredCapabilities.HasFlag(ToolCapability.Environment) && !grantedPermissions.EnvironmentAccess)
        {
            errors.Add(new ValidationError("PERMISSION_ENVIRONMENT_DENIED", "Tool requires environment access but it is not granted"));
        }

        return errors.Count > 0 ? ValidationResult.Failure(errors) : ValidationResult.Success();
    }

    /// <inheritdoc />
    public ValidationResult ValidateResourceLimits(ToolResourceUsage? estimatedUsage, ToolResourceLimits limits)
    {
        var warnings = new List<ValidationWarning>();

        if (estimatedUsage != null)
        {
            if (estimatedUsage.PeakMemoryBytes > limits.MaxMemoryBytes)
            {
                warnings.Add(new ValidationWarning("RESOURCE_MEMORY_EXCEEDED", $"Estimated memory usage ({estimatedUsage.PeakMemoryBytes:N0} bytes) exceeds limit ({limits.MaxMemoryBytes:N0} bytes)"));
            }

            if (estimatedUsage.FilesAccessed > limits.MaxFileCount)
            {
                warnings.Add(new ValidationWarning("RESOURCE_FILE_COUNT_EXCEEDED", $"Estimated file count ({estimatedUsage.FilesAccessed}) exceeds limit ({limits.MaxFileCount})"));
            }

            var totalFileSize = estimatedUsage.BytesRead + estimatedUsage.BytesWritten;
            if (totalFileSize > limits.MaxFileSizeBytes)
            {
                warnings.Add(new ValidationWarning("RESOURCE_FILE_SIZE_EXCEEDED", $"Estimated file size ({totalFileSize:N0} bytes) exceeds limit ({limits.MaxFileSizeBytes:N0} bytes)"));
            }
        }

        return ValidationResult.Success(warnings);
    }

    /// <inheritdoc />
    public ValidationResult ValidateExecutionRequest(ToolExecutionRequest request, ToolMetadata toolMetadata)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Validate tool ID
        if (string.IsNullOrWhiteSpace(request.ToolId))
        {
            errors.Add(new ValidationError("REQUEST_TOOL_ID_REQUIRED", "Tool ID is required"));
        }

        if (request.ToolId != toolMetadata.Id)
        {
            errors.Add(new ValidationError("REQUEST_TOOL_ID_MISMATCH", $"Request tool ID '{request.ToolId}' does not match metadata ID '{toolMetadata.Id}'"));
        }

        // Validate context
        if (string.IsNullOrWhiteSpace(request.Context.CorrelationId))
        {
            warnings.Add(new ValidationWarning("REQUEST_CORRELATION_ID_MISSING", "Correlation ID is recommended for tracking"));
        }

        // Validate timeout
        if (request.TimeoutMs.HasValue && request.TimeoutMs.Value <= 0)
        {
            errors.Add(new ValidationError("REQUEST_TIMEOUT_INVALID", "Timeout must be positive", nameof(request.TimeoutMs), request.TimeoutMs));
        }

        // Validate parameters if requested
        if (request.ValidateParameters)
        {
            var paramValidation = ValidateParameters(request.Parameters, toolMetadata.Parameters);
            errors.AddRange(paramValidation.Errors);
            warnings.AddRange(paramValidation.Warnings);
        }

        // Validate permissions if requested
        if (request.EnforcePermissions)
        {
            var permissionValidation = ValidatePermissions(toolMetadata.RequiredCapabilities, request.Context.Permissions);
            errors.AddRange(permissionValidation.Errors);
            warnings.AddRange(permissionValidation.Warnings);
        }

        return errors.Count > 0 ? ValidationResult.Failure(errors, warnings) : ValidationResult.Success(warnings);
    }

    /// <inheritdoc />
    public ValidationResult ValidateToolType(Type toolType)
    {
        var errors = new List<ValidationError>();

        if (!typeof(ITool).IsAssignableFrom(toolType))
        {
            errors.Add(new ValidationError("TOOL_TYPE_INVALID", $"Type '{toolType.FullName}' does not implement ITool interface"));
        }

        if (toolType.IsAbstract)
        {
            errors.Add(new ValidationError("TOOL_TYPE_ABSTRACT", $"Type '{toolType.FullName}' is abstract and cannot be instantiated"));
        }

        if (toolType.IsInterface)
        {
            errors.Add(new ValidationError("TOOL_TYPE_INTERFACE", $"Type '{toolType.FullName}' is an interface and cannot be instantiated"));
        }

        if (!toolType.GetConstructors().Any(c => c.GetParameters().Length == 0 || c.GetParameters().All(p => p.HasDefaultValue)))
        {
            errors.Add(new ValidationError("TOOL_TYPE_NO_CONSTRUCTOR", $"Type '{toolType.FullName}' does not have a parameterless constructor or constructor with default parameters"));
        }

        return errors.Count > 0 ? ValidationResult.Failure(errors) : ValidationResult.Success();
    }

    private static bool IsValidToolId(string toolId)
    {
        return !string.IsNullOrWhiteSpace(toolId) &&
               toolId.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-') &&
               toolId.Length <= 100;
    }

    private static bool IsValidVersion(string version)
    {
        return Version.TryParse(version, out _);
    }

    private static bool IsValidParameterType(string type)
    {
        var validTypes = new[] { "string", "number", "integer", "boolean", "array", "object" };
        return validTypes.Contains(type, StringComparer.OrdinalIgnoreCase);
    }

    private static IList<ValidationError> ValidateParameterValue(ToolParameter parameter, object value)
    {
        var errors = new List<ValidationError>();

        // Type validation
        switch (parameter.Type.ToLowerInvariant())
        {
            case "string":
                if (value is not string stringValue)
                {
                    errors.Add(new ValidationError("PARAMETER_TYPE_MISMATCH", $"Parameter '{parameter.Name}' must be a string", parameter.Name, value));
                    break;
                }

                if (parameter.MinLength.HasValue && stringValue.Length < parameter.MinLength.Value)
                {
                    errors.Add(new ValidationError("PARAMETER_STRING_TOO_SHORT", $"Parameter '{parameter.Name}' must be at least {parameter.MinLength.Value} characters", parameter.Name, value));
                }

                if (parameter.MaxLength.HasValue && stringValue.Length > parameter.MaxLength.Value)
                {
                    errors.Add(new ValidationError("PARAMETER_STRING_TOO_LONG", $"Parameter '{parameter.Name}' must be at most {parameter.MaxLength.Value} characters", parameter.Name, value));
                }

                if (!string.IsNullOrEmpty(parameter.Pattern) && !System.Text.RegularExpressions.Regex.IsMatch(stringValue, parameter.Pattern))
                {
                    errors.Add(new ValidationError("PARAMETER_STRING_PATTERN_MISMATCH", $"Parameter '{parameter.Name}' does not match the required pattern", parameter.Name, value));
                }

                break;

            case "number":
            case "integer":
                if (!IsNumericValue(value, out var numericValue))
                {
                    errors.Add(new ValidationError("PARAMETER_TYPE_MISMATCH", $"Parameter '{parameter.Name}' must be a number", parameter.Name, value));
                    break;
                }

                if (parameter.Type.Equals("integer", StringComparison.OrdinalIgnoreCase) && numericValue != Math.Floor(numericValue))
                {
                    errors.Add(new ValidationError("PARAMETER_NOT_INTEGER", $"Parameter '{parameter.Name}' must be an integer", parameter.Name, value));
                }

                if (parameter.MinValue.HasValue && numericValue < parameter.MinValue.Value)
                {
                    errors.Add(new ValidationError("PARAMETER_NUMBER_TOO_SMALL", $"Parameter '{parameter.Name}' must be at least {parameter.MinValue.Value}", parameter.Name, value));
                }

                if (parameter.MaxValue.HasValue && numericValue > parameter.MaxValue.Value)
                {
                    errors.Add(new ValidationError("PARAMETER_NUMBER_TOO_LARGE", $"Parameter '{parameter.Name}' must be at most {parameter.MaxValue.Value}", parameter.Name, value));
                }

                break;

            case "boolean":
                if (value is not bool)
                {
                    errors.Add(new ValidationError("PARAMETER_TYPE_MISMATCH", $"Parameter '{parameter.Name}' must be a boolean", parameter.Name, value));
                }

                break;

            case "array":
                if (value is not System.Collections.IEnumerable enumerable || value is string)
                {
                    errors.Add(new ValidationError("PARAMETER_TYPE_MISMATCH", $"Parameter '{parameter.Name}' must be an array", parameter.Name, value));
                    break;
                }

                var arrayValue = enumerable.Cast<object>().ToList();
                if (parameter.MinLength.HasValue && arrayValue.Count < parameter.MinLength.Value)
                {
                    errors.Add(new ValidationError("PARAMETER_ARRAY_TOO_SHORT", $"Parameter '{parameter.Name}' must have at least {parameter.MinLength.Value} items", parameter.Name, value));
                }

                if (parameter.MaxLength.HasValue && arrayValue.Count > parameter.MaxLength.Value)
                {
                    errors.Add(new ValidationError("PARAMETER_ARRAY_TOO_LONG", $"Parameter '{parameter.Name}' must have at most {parameter.MaxLength.Value} items", parameter.Name, value));
                }

                break;

            case "object":
                // Objects are generally accepted as-is, but could add JSON schema validation here
                break;
        }

        // Validate allowed values
        if (parameter.AllowedValues != null && parameter.AllowedValues.Count > 0)
        {
            if (!parameter.AllowedValues.Contains(value))
            {
                errors.Add(new ValidationError("PARAMETER_VALUE_NOT_ALLOWED", $"Parameter '{parameter.Name}' value is not in the list of allowed values", parameter.Name, value));
            }
        }

        return errors;
    }

    private static bool IsNumericValue(object value, out double numericValue)
    {
        numericValue = 0;
        return value switch
        {
            int intValue => (numericValue = intValue) >= 0 || true,
            long longValue => (numericValue = longValue) >= 0 || true,
            float floatValue => (numericValue = floatValue) >= 0 || true,
            double doubleValue => (numericValue = doubleValue) >= 0 || true,
            decimal decimalValue => (numericValue = (double)decimalValue) >= 0 || true,
            _ => false
        };
    }
}
