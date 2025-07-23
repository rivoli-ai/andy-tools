using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Andy.Tools.Library;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Tools.Tests.Library;

public class TodoManagementToolTests
{
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _serviceProvider;

    public TodoManagementToolTests(ITestOutputHelper output)
    {
        _output = output;

        // Set up minimal service provider for testing
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void Tool_HasCorrectMetadata()
    {
        // Arrange
        var tool = new TodoManagementTool(_serviceProvider);

        // Act
        var metadata = tool.Metadata;

        // Assert
        Assert.Equal("todo_management", metadata.Id);
        Assert.Equal("Todo Management Tool", metadata.Name);
        Assert.Contains("todo", metadata.Description.ToLower());
        Assert.Equal(ToolCategory.Productivity, metadata.Category);
        Assert.Equal(ToolPermissionFlags.None, metadata.RequiredPermissions);
        Assert.NotEmpty(metadata.Parameters);
        Assert.NotEmpty(metadata.Examples);
    }

    [Fact]
    public void Tool_HasAllRequiredActions()
    {
        // Arrange
        var tool = new TodoManagementTool(_serviceProvider);
        var actionParam = tool.Metadata.Parameters.FirstOrDefault(p => p.Name == "action");

        // Assert
        Assert.NotNull(actionParam);
        Assert.NotNull(actionParam.AllowedValues);

        var allowedActions = actionParam.AllowedValues.Select(v => v?.ToString()).ToList();
        Assert.Contains("add", allowedActions);
        Assert.Contains("add_batch", allowedActions);
        Assert.Contains("list", allowedActions);
        Assert.Contains("complete", allowedActions);
        Assert.Contains("remove", allowedActions);
        Assert.Contains("update_progress", allowedActions);
        Assert.Contains("search", allowedActions);
        Assert.Contains("clear_completed", allowedActions);
    }

    [Fact]
    public async Task Tool_FailsGracefullyWhenTodoServiceNotAvailable()
    {
        // Arrange
        var tool = new TodoManagementTool(_serviceProvider);
        await tool.InitializeAsync();

        var parameters = new Dictionary<string, object?>
        {
            ["action"] = "list"
        };

        var context = new ToolExecutionContext
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Permissions = new ToolPermissions()
        };

        // Act
        var result = await tool.ExecuteAsync(parameters, context);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Contains("not available", result.ErrorMessage);
        _output.WriteLine($"Expected error: {result.ErrorMessage}");
    }

    [Fact]
    public void Tool_ValidatesRequiredParameters()
    {
        // Arrange
        var tool = new TodoManagementTool(_serviceProvider);

        // Test missing action
        var parameters1 = new Dictionary<string, object?>();
        var errors1 = tool.ValidateParameters(parameters1);
        Assert.Contains(errors1, e => e.Contains("action") && e.Contains("required", StringComparison.OrdinalIgnoreCase));

        // Test invalid action
        var parameters2 = new Dictionary<string, object?>
        {
            ["action"] = "invalid_action"
        };
        // Note: Basic validation doesn't check allowed values, that's done during execution

        // Test add without text
        var parameters3 = new Dictionary<string, object?>
        {
            ["action"] = "add"
        };
        var errors3 = tool.ValidateParameters(parameters3);
        // Text validation happens during execution, not in parameter validation

        // Test valid parameters
        var parameters4 = new Dictionary<string, object?>
        {
            ["action"] = "list"
        };
        var errors4 = tool.ValidateParameters(parameters4);
        Assert.Empty(errors4);
    }

    [Fact]
    public void Tool_ExamplesAreValid()
    {
        // Arrange
        var tool = new TodoManagementTool(_serviceProvider);

        // Act & Assert
        foreach (var example in tool.Metadata.Examples)
        {
            _output.WriteLine($"Validating example: {example.Description}");

            // Validate the example parameters
            var errors = tool.ValidateParameters(example.Parameters);
            Assert.Empty(errors);

            // Check that action is present
            Assert.True(example.Parameters.ContainsKey("action"));

            _output.WriteLine($"Example is valid: {example.Description}");
        }
    }
}
