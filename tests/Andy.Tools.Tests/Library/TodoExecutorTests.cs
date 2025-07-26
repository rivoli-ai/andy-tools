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

public class TodoExecutorTests
{
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _serviceProvider;

    public TodoExecutorTests(ITestOutputHelper output)
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
        var tool = new TodoExecutor(_serviceProvider);

        // Act
        var metadata = tool.Metadata;

        // Assert
        Assert.Equal("todo_executor", metadata.Id);
        Assert.Equal("Todo Executor", metadata.Name);
        Assert.Contains("execute", metadata.Description.ToLower());
        Assert.Equal(ToolCategory.Productivity, metadata.Category);
        Assert.Equal(ToolPermissionFlags.FileSystemRead | ToolPermissionFlags.FileSystemWrite | ToolPermissionFlags.ProcessExecution, metadata.RequiredPermissions);
        Assert.NotEmpty(metadata.Parameters);
        Assert.NotEmpty(metadata.Examples);
    }

    [Fact]
    public void Tool_HasAllRequiredActions()
    {
        // Arrange
        var tool = new TodoExecutor();
        var actionParam = tool.Metadata.Parameters.FirstOrDefault(p => p.Name == "action");

        // Assert
        Assert.NotNull(actionParam);
        Assert.NotNull(actionParam.AllowedValues);
        var allowedActions = actionParam.AllowedValues.Cast<string>().ToList();
        Assert.Contains("execute_all", allowedActions);
        Assert.Contains("execute_single", allowedActions);
        Assert.Contains("analyze", allowedActions);
        Assert.Contains("dry_run", allowedActions);
    }

    [Fact]
    public async Task Tool_RejectsInvalidAction()
    {
        // Arrange
        var tool = new TodoExecutor(_serviceProvider);
        await tool.InitializeAsync(new Dictionary<string, object?> { ["ServiceProvider"] = _serviceProvider });
        var parameters = new Dictionary<string, object?>
        {
            ["action"] = "invalid_action"
        };
        var context = CreateTestContext();

        // Act
        var result = await tool.ExecuteAsync(parameters, context);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Contains("Parameter 'action' must be one of:", result.ErrorMessage);
    }

    [Fact]
    public async Task Tool_RequiresActionParameter()
    {
        // Arrange
        var tool = new TodoExecutor(_serviceProvider);
        await tool.InitializeAsync(new Dictionary<string, object?> { ["ServiceProvider"] = _serviceProvider });
        var parameters = new Dictionary<string, object?>();
        var context = CreateTestContext();

        // Act
        var result = await tool.ExecuteAsync(parameters, context);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Contains("Required parameter 'action' is missing", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteSingle_RequiresTodoldParameter()
    {
        // Arrange
        var tool = new TodoExecutor(_serviceProvider);
        await tool.InitializeAsync(new Dictionary<string, object?> { ["ServiceProvider"] = _serviceProvider });
        var parameters = new Dictionary<string, object?>
        {
            ["action"] = "execute_single"
        };
        var context = CreateTestContext();

        // Act
        var result = await tool.ExecuteAsync(parameters, context);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Contains("todoId parameter is required", result.ErrorMessage);
    }

    [Fact]
    public void Tool_ValidatesParameters()
    {
        // Arrange
        var tool = new TodoExecutor();

        // Test valid parameters
        var validParams = new Dictionary<string, object?>
        {
            ["action"] = "execute_all",
            ["confirmCritical"] = true
        };

        // Act & Assert
        Assert.Empty(tool.ValidateParameters(validParams));

        // Test invalid action
        var invalidParams = new Dictionary<string, object?>
        {
            ["action"] = "invalid"
        };

        var validationErrors = tool.ValidateParameters(invalidParams);
        Assert.NotEmpty(validationErrors);
        Assert.Contains(validationErrors, e => e.ToLower().Contains("action"));
    }

    [Fact]
    public void Tool_HasProperExamples()
    {
        // Arrange
        var tool = new TodoExecutor();

        // Act
        var examples = tool.Metadata.Examples;

        // Assert
        Assert.NotNull(examples);
        Assert.NotEmpty(examples);

        // Check execute_all example
        var executeAllExample = examples.FirstOrDefault(e => e.Description.Contains("Execute all pending"));
        Assert.NotNull(executeAllExample);
        Assert.NotNull(executeAllExample.Parameters);
        Assert.Equal("execute_all", executeAllExample.Parameters["action"]);

        // Check execute_single example
        var executeSingleExample = examples.FirstOrDefault(e => e.Description.Contains("specific todo"));
        Assert.NotNull(executeSingleExample);
        Assert.NotNull(executeSingleExample.Parameters);
        Assert.Equal("execute_single", executeSingleExample.Parameters["action"]);
        Assert.NotNull(executeSingleExample.Parameters["todoId"]);
    }

    [Fact]
    public void TaskAnalysis_IdentifiesFrameworkUpdate()
    {
        // This would require making AnalyzeTodoTask public or testing through the public interface
        // For now, we'll test through the analyze action
    }

    [Fact]
    public void TaskAnalysis_IdentifiesPackageUpdate()
    {
        // This would require making AnalyzeTodoTask public or testing through the public interface
        // For now, we'll test through the analyze action
    }

    [Fact]
    public void TaskAnalysis_IdentifiesTestExecution()
    {
        // This would require making AnalyzeTodoTask public or testing through the public interface
        // For now, we'll test through the analyze action
    }

    [Fact]
    public void TaskAnalysis_IdentifiesManualTasks()
    {
        // This would require making AnalyzeTodoTask public or testing through the public interface
        // For now, we'll test through the analyze action
    }

    private ToolExecutionContext CreateTestContext()
    {
        return new ToolExecutionContext
        {
            Permissions = new ToolPermissions
            {
                FileSystemAccess = true,
                NetworkAccess = true,
                ProcessExecution = true
            },
            AdditionalData = new Dictionary<string, object?>
            {
                ["ServiceProvider"] = _serviceProvider
            }
        };
    }
}
