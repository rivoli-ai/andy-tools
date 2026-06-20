using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Andy.Tools.Library;
using Andy.Tools.Library.Todos;
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
    public async Task Tool_WorksWithNoHostService_UsingFallbackStore()
    {
        // The tool is now self-contained: with no ITodoStore registered it falls back to a
        // process-wide in-memory store, so basic actions succeed instead of failing on a missing
        // host service.
        var tool = new TodoManagementTool(_serviceProvider);
        await tool.InitializeAsync();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?> { ["action"] = "list" }, NewContext());

        Assert.True(result.IsSuccessful);
    }

    // Builds a tool backed by a fresh, isolated in-memory store (the DI path), so tests don't share
    // the process-wide fallback store.
    private static async Task<TodoManagementTool> CreateToolWithIsolatedStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITodoStore, InMemoryTodoStore>();
        var sp = services.BuildServiceProvider();
        var tool = new TodoManagementTool(sp);
        await tool.InitializeAsync();
        return tool;
    }

    private static ToolExecutionContext NewContext() => new()
    {
        CorrelationId = Guid.NewGuid().ToString(),
        Permissions = new ToolPermissions()
    };

    private static T GetData<T>(ToolResult result, string property)
    {
        Assert.NotNull(result.Data);
        var prop = result.Data!.GetType().GetProperty(property);
        Assert.NotNull(prop);
        return (T)prop!.GetValue(result.Data)!;
    }

    [Fact]
    public async Task AddBatch_AcceptsDictionaryStringNullableObjectItems()
    {
        // This is the real-world failing shape: tool-call args deserialize todo items into
        // Dictionary<string, object?>, which the old code's `is Dictionary<string, object>` check
        // silently missed - so nothing was added.
        var tool = await CreateToolWithIsolatedStore();
        var todos = new object?[]
        {
            new Dictionary<string, object?> { ["text"] = "Buy groceries", ["priority"] = "medium" },
            new Dictionary<string, object?> { ["text"] = "Call dentist", ["priority"] = "high", ["tags"] = new[] { "health" } }
        };

        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["action"] = "add_batch", ["todos"] = todos }, NewContext());

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, GetData<int>(result, "count"));
        Assert.Equal(0, GetData<int>(result, "skipped"));
    }

    [Fact]
    public async Task AddBatch_AcceptsJsonElementArray()
    {
        var tool = await CreateToolWithIsolatedStore();
        // Realistic shape: the outer collection is an object[] (recognized as an array) whose items
        // are JsonElement values, as produced when tool-call args are deserialized with System.Text.Json.
        using var doc = JsonDocument.Parse("""
            [ { "text": "First", "priority": "low" }, "Second as bare string", { "text": "Third", "tags": ["a","b"] } ]
            """);
        var todos = doc.RootElement.EnumerateArray().Select(e => (object?)e.Clone()).ToArray();

        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["action"] = "add_batch", ["todos"] = todos }, NewContext());

        Assert.True(result.IsSuccessful);
        Assert.Equal(3, GetData<int>(result, "count"));
    }

    [Fact]
    public async Task AddBatch_AcceptsAnonymousObjectItems_AsInExamples()
    {
        var tool = await CreateToolWithIsolatedStore();
        var todos = new[]
        {
            new { text = "Buy groceries", priority = "medium" },
            new { text = "Complete report", priority = "high" }
        };

        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["action"] = "add_batch", ["todos"] = todos }, NewContext());

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, GetData<int>(result, "count"));
    }

    [Fact]
    public async Task AddBatch_SkipsItemsWithoutText_AndReportsCount()
    {
        var tool = await CreateToolWithIsolatedStore();
        var todos = new object?[]
        {
            new Dictionary<string, object?> { ["text"] = "Valid" },
            new Dictionary<string, object?> { ["priority"] = "high" }, // no text
            "  ", // whitespace only
        };

        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["action"] = "add_batch", ["todos"] = todos }, NewContext());

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, GetData<int>(result, "count"));
        Assert.Equal(2, GetData<int>(result, "skipped"));
    }

    [Fact]
    public async Task AddBatch_AllItemsInvalid_ReturnsFailure()
    {
        var tool = await CreateToolWithIsolatedStore();
        var todos = new object?[] { new Dictionary<string, object?> { ["priority"] = "high" }, "" };

        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["action"] = "add_batch", ["todos"] = todos }, NewContext());

        Assert.False(result.IsSuccessful);
        Assert.Contains("No valid todos", result.ErrorMessage);
    }

    [Fact]
    public async Task AddThenListAndComplete_RoundTrips()
    {
        var tool = await CreateToolWithIsolatedStore();
        var ctx = NewContext();

        var add = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["action"] = "add", ["text"] = "Write tests", ["priority"] = "high" }, ctx);
        Assert.True(add.IsSuccessful);
        var id = GetData<int>(add, "id");

        var list = await tool.ExecuteAsync(new Dictionary<string, object?> { ["action"] = "list" }, ctx);
        Assert.True(list.IsSuccessful);
        Assert.Equal(1, GetData<int>(list, "count"));

        var complete = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["action"] = "complete", ["id"] = id }, ctx);
        Assert.True(complete.IsSuccessful);

        var cleared = await tool.ExecuteAsync(new Dictionary<string, object?> { ["action"] = "clear_completed" }, ctx);
        Assert.True(cleared.IsSuccessful);
        Assert.Equal(1, GetData<int>(cleared, "count"));
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
