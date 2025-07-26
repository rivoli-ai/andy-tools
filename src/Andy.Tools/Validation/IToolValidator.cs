using Andy.Tools.Core;

namespace Andy.Tools.Validation;

/// <summary>
/// Interface for validating tools and their parameters.
/// </summary>
public interface IToolValidator
{
    /// <summary>
    /// Validates tool metadata.
    /// </summary>
    /// <param name="metadata">The tool metadata to validate.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult ValidateMetadata(ToolMetadata metadata);

    /// <summary>
    /// Validates tool parameters against their definitions.
    /// </summary>
    /// <param name="parameters">The parameters to validate.</param>
    /// <param name="parameterDefinitions">The parameter definitions.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult ValidateParameters(Dictionary<string, object?> parameters, IList<ToolParameter> parameterDefinitions);

    /// <summary>
    /// Validates tool permissions for execution.
    /// </summary>
    /// <param name="requiredCapabilities">The capabilities required by the tool.</param>
    /// <param name="grantedPermissions">The permissions granted for execution.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult ValidatePermissions(ToolCapability requiredCapabilities, ToolPermissions grantedPermissions);

    /// <summary>
    /// Validates resource limits for tool execution.
    /// </summary>
    /// <param name="estimatedUsage">The estimated resource usage.</param>
    /// <param name="limits">The resource limits.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult ValidateResourceLimits(ToolResourceUsage? estimatedUsage, ToolResourceLimits limits);

    /// <summary>
    /// Validates a tool execution request.
    /// </summary>
    /// <param name="request">The execution request.</param>
    /// <param name="toolMetadata">The tool metadata.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult ValidateExecutionRequest(ToolExecutionRequest request, ToolMetadata toolMetadata);

    /// <summary>
    /// Validates a tool type for registration.
    /// </summary>
    /// <param name="toolType">The tool type.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult ValidateToolType(Type toolType);
}
