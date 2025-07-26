using System.Collections.Generic;
using System.Linq;
using Andy.Tools.Core;
using Andy.Tools.Validation;
using Xunit;

namespace Andy.Tools.Tests.Validation;

public class ToolValidatorTests
{
    private readonly ToolValidator _validator = new();

    [Fact]
    public void ValidateMetadata_ValidMetadata_ShouldReturnSuccess()
    {
        // Arrange
        var metadata = new ToolMetadata
        {
            Id = "valid_tool",
            Name = "Valid Tool",
            Description = "A valid tool for testing",
            Version = "1.0.0",
            Category = ToolCategory.Utility,
            Parameters =
            [
                new ToolParameter
                {
                    Name = "input",
                    Type = "string",
                    Description = "Input parameter",
                    Required = true
                }
            ]
        };

        // Act
        var result = _validator.ValidateMetadata(metadata);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateMetadata_MissingRequiredFields_ShouldReturnErrors()
    {
        // Arrange
        var metadata = new ToolMetadata
        {
            Id = "",
            Name = "",
            Description = ""
        };

        // Act
        var result = _validator.ValidateMetadata(metadata);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "METADATA_ID_REQUIRED");
        Assert.Contains(result.Errors, e => e.Code == "METADATA_NAME_REQUIRED");
        Assert.Contains(result.Errors, e => e.Code == "METADATA_DESCRIPTION_REQUIRED");
    }

    [Fact]
    public void ValidateMetadata_InvalidId_ShouldReturnError()
    {
        // Arrange
        var metadata = new ToolMetadata
        {
            Id = "invalid@tool#id",
            Name = "Test Tool",
            Description = "Test description"
        };

        // Act
        var result = _validator.ValidateMetadata(metadata);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "METADATA_ID_INVALID");
    }

    [Fact]
    public void ValidateMetadata_InvalidVersion_ShouldReturnError()
    {
        // Arrange
        var metadata = new ToolMetadata
        {
            Id = "test_tool",
            Name = "Test Tool",
            Description = "Test description",
            Version = "invalid.version"
        };

        // Act
        var result = _validator.ValidateMetadata(metadata);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "METADATA_VERSION_INVALID");
    }

    [Fact]
    public void ValidateMetadata_DuplicateParameterNames_ShouldReturnError()
    {
        // Arrange
        var metadata = new ToolMetadata
        {
            Id = "test_tool",
            Name = "Test Tool",
            Description = "Test description",
            Parameters =
            [
                new ToolParameter { Name = "param1", Type = "string" },
                new ToolParameter { Name = "param1", Type = "number" }
            ]
        };

        // Act
        var result = _validator.ValidateMetadata(metadata);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PARAMETER_NAME_DUPLICATE");
    }

    [Fact]
    public void ValidateMetadata_InvalidParameterType_ShouldReturnError()
    {
        // Arrange
        var metadata = new ToolMetadata
        {
            Id = "test_tool",
            Name = "Test Tool",
            Description = "Test description",
            Parameters =
            [
                new ToolParameter { Name = "param1", Type = "invalid_type" }
            ]
        };

        // Act
        var result = _validator.ValidateMetadata(metadata);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PARAMETER_TYPE_INVALID");
    }

    [Fact]
    public void ValidateParameters_RequiredParameterMissing_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>();
        var parameterDefinitions = new List<ToolParameter>
        {
            new() { Name = "required_param", Type = "string", Required = true }
        };

        // Act
        var result = _validator.ValidateParameters(parameters, parameterDefinitions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PARAMETER_REQUIRED");
    }

    [Fact]
    public void ValidateParameters_RequiredParameterNull_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { { "required_param", null } };
        var parameterDefinitions = new List<ToolParameter>
        {
            new() { Name = "required_param", Type = "string", Required = true }
        };

        // Act
        var result = _validator.ValidateParameters(parameters, parameterDefinitions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PARAMETER_NULL");
    }

    [Fact]
    public void ValidateParameters_UnknownParameter_ShouldReturnWarning()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { { "unknown_param", "value" } };
        var parameterDefinitions = new List<ToolParameter>();

        // Act
        var result = _validator.ValidateParameters(parameters, parameterDefinitions);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "PARAMETER_UNKNOWN");
    }

    [Fact]
    public void ValidateParameters_StringTooShort_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { { "text_param", "ab" } };
        var parameterDefinitions = new List<ToolParameter>
        {
            new() { Name = "text_param", Type = "string", MinLength = 5 }
        };

        // Act
        var result = _validator.ValidateParameters(parameters, parameterDefinitions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PARAMETER_STRING_TOO_SHORT");
    }

    [Fact]
    public void ValidateParameters_StringTooLong_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { { "text_param", "toolongstring" } };
        var parameterDefinitions = new List<ToolParameter>
        {
            new() { Name = "text_param", Type = "string", MaxLength = 5 }
        };

        // Act
        var result = _validator.ValidateParameters(parameters, parameterDefinitions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PARAMETER_STRING_TOO_LONG");
    }

    [Fact]
    public void ValidateParameters_NumberTooSmall_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { { "num_param", 5 } };
        var parameterDefinitions = new List<ToolParameter>
        {
            new() { Name = "num_param", Type = "number", MinValue = 10 }
        };

        // Act
        var result = _validator.ValidateParameters(parameters, parameterDefinitions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PARAMETER_NUMBER_TOO_SMALL");
    }

    [Fact]
    public void ValidateParameters_NumberTooLarge_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { { "num_param", 15 } };
        var parameterDefinitions = new List<ToolParameter>
        {
            new() { Name = "num_param", Type = "number", MaxValue = 10 }
        };

        // Act
        var result = _validator.ValidateParameters(parameters, parameterDefinitions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PARAMETER_NUMBER_TOO_LARGE");
    }

    [Fact]
    public void ValidateParameters_NotInteger_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { { "int_param", 5.5 } };
        var parameterDefinitions = new List<ToolParameter>
        {
            new() { Name = "int_param", Type = "integer" }
        };

        // Act
        var result = _validator.ValidateParameters(parameters, parameterDefinitions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PARAMETER_NOT_INTEGER");
    }

    [Fact]
    public void ValidateParameters_WrongType_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { { "bool_param", "not_boolean" } };
        var parameterDefinitions = new List<ToolParameter>
        {
            new() { Name = "bool_param", Type = "boolean" }
        };

        // Act
        var result = _validator.ValidateParameters(parameters, parameterDefinitions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PARAMETER_TYPE_MISMATCH");
    }

    [Fact]
    public void ValidateParameters_ValueNotAllowed_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { { "choice_param", "invalid_choice" } };
        var parameterDefinitions = new List<ToolParameter>
        {
            new() { Name = "choice_param", Type = "string", AllowedValues = ["option1", "option2"] }
        };

        // Act
        var result = _validator.ValidateParameters(parameters, parameterDefinitions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PARAMETER_VALUE_NOT_ALLOWED");
    }

    [Fact]
    public void ValidatePermissions_FileSystemRequired_AccessDenied_ShouldReturnError()
    {
        // Arrange
        var requiredCapabilities = ToolCapability.FileSystem;
        var grantedPermissions = new ToolPermissions { FileSystemAccess = false };

        // Act
        var result = _validator.ValidatePermissions(requiredCapabilities, grantedPermissions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PERMISSION_FILESYSTEM_DENIED");
    }

    [Fact]
    public void ValidatePermissions_NetworkRequired_AccessDenied_ShouldReturnError()
    {
        // Arrange
        var requiredCapabilities = ToolCapability.Network;
        var grantedPermissions = new ToolPermissions { NetworkAccess = false };

        // Act
        var result = _validator.ValidatePermissions(requiredCapabilities, grantedPermissions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PERMISSION_NETWORK_DENIED");
    }

    [Fact]
    public void ValidatePermissions_ProcessRequired_AccessDenied_ShouldReturnError()
    {
        // Arrange
        var requiredCapabilities = ToolCapability.ProcessExecution;
        var grantedPermissions = new ToolPermissions { ProcessExecution = false };

        // Act
        var result = _validator.ValidatePermissions(requiredCapabilities, grantedPermissions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PERMISSION_PROCESS_DENIED");
    }

    [Fact]
    public void ValidatePermissions_EnvironmentRequired_AccessDenied_ShouldReturnError()
    {
        // Arrange
        var requiredCapabilities = ToolCapability.Environment;
        var grantedPermissions = new ToolPermissions { EnvironmentAccess = false };

        // Act
        var result = _validator.ValidatePermissions(requiredCapabilities, grantedPermissions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PERMISSION_ENVIRONMENT_DENIED");
    }

    [Fact]
    public void ValidatePermissions_AllAccessGranted_ShouldReturnSuccess()
    {
        // Arrange
        var requiredCapabilities = ToolCapability.FileSystem | ToolCapability.Network;
        var grantedPermissions = new ToolPermissions
        {
            FileSystemAccess = true,
            NetworkAccess = true
        };

        // Act
        var result = _validator.ValidatePermissions(requiredCapabilities, grantedPermissions);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateResourceLimits_ExceedsMemoryLimit_ShouldReturnWarning()
    {
        // Arrange
        var estimatedUsage = new ToolResourceUsage { PeakMemoryBytes = 1000 };
        var limits = new ToolResourceLimits { MaxMemoryBytes = 500 };

        // Act
        var result = _validator.ValidateResourceLimits(estimatedUsage, limits);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "RESOURCE_MEMORY_EXCEEDED");
    }

    [Fact]
    public void ValidateResourceLimits_ExceedsFileCount_ShouldReturnWarning()
    {
        // Arrange
        var estimatedUsage = new ToolResourceUsage { FilesAccessed = 100 };
        var limits = new ToolResourceLimits { MaxFileCount = 50 };

        // Act
        var result = _validator.ValidateResourceLimits(estimatedUsage, limits);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "RESOURCE_FILE_COUNT_EXCEEDED");
    }

    [Fact]
    public void ValidateToolType_ValidType_ShouldReturnSuccess()
    {
        // Act
        var result = _validator.ValidateToolType(typeof(ValidTestTool));

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateToolType_NotITool_ShouldReturnError()
    {
        // Act
        var result = _validator.ValidateToolType(typeof(string));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TOOL_TYPE_INVALID");
    }

    [Fact]
    public void ValidateToolType_AbstractType_ShouldReturnError()
    {
        // Act
        var result = _validator.ValidateToolType(typeof(AbstractTestTool));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TOOL_TYPE_ABSTRACT");
    }

    [Fact]
    public void ValidateToolType_InterfaceType_ShouldReturnError()
    {
        // Act
        var result = _validator.ValidateToolType(typeof(ITool));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TOOL_TYPE_INTERFACE");
    }

    [Fact]
    public void ValidateExecutionRequest_ValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var context = new ToolExecutionContext
        {
            CorrelationId = "test-correlation",
            Permissions = new ToolPermissions { FileSystemAccess = true }
        };

        var request = new ToolExecutionRequest
        {
            ToolId = "test_tool",
            Parameters = new Dictionary<string, object?>(),
            Context = context,
            ValidateParameters = false,
            EnforcePermissions = false
        };

        var metadata = new ToolMetadata
        {
            Id = "test_tool",
            Name = "Test Tool",
            Description = "Test description"
        };

        // Act
        var result = _validator.ValidateExecutionRequest(request, metadata);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateExecutionRequest_MissingToolId_ShouldReturnError()
    {
        // Arrange
        var request = new ToolExecutionRequest
        {
            ToolId = "",
            Context = new ToolExecutionContext()
        };

        var metadata = new ToolMetadata { Id = "test_tool" };

        // Act
        var result = _validator.ValidateExecutionRequest(request, metadata);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "REQUEST_TOOL_ID_REQUIRED");
    }

    [Fact]
    public void ValidateExecutionRequest_ToolIdMismatch_ShouldReturnError()
    {
        // Arrange
        var request = new ToolExecutionRequest
        {
            ToolId = "different_tool",
            Context = new ToolExecutionContext()
        };

        var metadata = new ToolMetadata { Id = "test_tool" };

        // Act
        var result = _validator.ValidateExecutionRequest(request, metadata);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "REQUEST_TOOL_ID_MISMATCH");
    }

    [Fact]
    public void ValidateExecutionRequest_InvalidTimeout_ShouldReturnError()
    {
        // Arrange
        var request = new ToolExecutionRequest
        {
            ToolId = "test_tool",
            TimeoutMs = -100,
            Context = new ToolExecutionContext()
        };

        var metadata = new ToolMetadata { Id = "test_tool" };

        // Act
        var result = _validator.ValidateExecutionRequest(request, metadata);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "REQUEST_TIMEOUT_INVALID");
    }
}

// Test helper classes
public class ValidTestTool : ITool
{
    public ToolMetadata Metadata { get; } = new()
    {
        Id = "valid_test_tool",
        Name = "Valid Test Tool",
        Description = "A valid test tool"
    };

    public Task InitializeAsync(Dictionary<string, object?>? configuration = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        return Task.FromResult(ToolResult.Success("Success"));
    }

    public IList<string> ValidateParameters(Dictionary<string, object?> parameters)
    {
        return new List<string>();
    }

    public bool CanExecuteWithPermissions(ToolPermissions permissions)
    {
        return true;
    }

    public Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public abstract class AbstractTestTool : ITool
{
    public abstract ToolMetadata Metadata { get; }
    public abstract Task InitializeAsync(Dictionary<string, object?>? configuration = null, CancellationToken cancellationToken = default);
    public abstract Task<ToolResult> ExecuteAsync(Dictionary<string, object?> parameters, ToolExecutionContext context);
    public abstract IList<string> ValidateParameters(Dictionary<string, object?> parameters);
    public abstract bool CanExecuteWithPermissions(ToolPermissions permissions);
    public abstract Task DisposeAsync(CancellationToken cancellationToken = default);
}
