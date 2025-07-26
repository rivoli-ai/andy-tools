using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Library;

/// <summary>
/// Tool that executes tasks from a todo list by analyzing and automating actionable items.
/// </summary>
public class TodoExecutor : ToolBase
{
    private IServiceProvider? _serviceProvider;
    private ILogger<TodoExecutor>? _logger;
    private IToolExecutor? _toolExecutor;
    private IToolRegistry? _toolRegistry;

    /// <inheritdoc />
    public override ToolMetadata Metadata => new()
    {
        Id = "todo_executor",
        Name = "Todo Executor",
        Description = "Executes tasks from a todo list by analyzing and automating actionable items. Can run commands, edit files, and perform other automated tasks.",
        Version = "1.0.0",
        Author = "Andy CLI",
        Category = ToolCategory.Productivity,
        Examples = new[]
        {
            new ToolExample
            {
                Description = "Execute all pending tasks from the todo list",
                Parameters = new Dictionary<string, object?>
                {
                    ["action"] = "execute_all"
                }
            },
            new ToolExample
            {
                Description = "Execute a specific todo by ID",
                Parameters = new Dictionary<string, object?>
                {
                    ["action"] = "execute_single",
                    ["todoId"] = 1
                }
            },
            new ToolExample
            {
                Description = "Analyze todo list and report what can be automated",
                Parameters = new Dictionary<string, object?>
                {
                    ["action"] = "analyze"
                }
            }
        },
        RequiredPermissions = ToolPermissionFlags.FileSystemRead | ToolPermissionFlags.FileSystemWrite | ToolPermissionFlags.ProcessExecution,
        Parameters = new List<ToolParameter>
        {
            new ToolParameter
            {
                Name = "action",
                Type = "string",
                Description = "The action to perform: execute_all, execute_single, analyze, dry_run",
                Required = true,
                AllowedValues = new object[] { "execute_all", "execute_single", "analyze", "dry_run" }
            },
            new ToolParameter
            {
                Name = "todoId",
                Type = "number",
                Description = "The ID of a specific todo to execute (required for execute_single)",
                Required = false
            },
            new ToolParameter
            {
                Name = "filter",
                Type = "object",
                Description = "Filter criteria for selecting todos to execute",
                Required = false
            },
            new ToolParameter
            {
                Name = "confirmCritical",
                Type = "boolean",
                Description = "Whether to require confirmation for critical operations (default: true)",
                Required = false,
                DefaultValue = true
            }
        }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoExecutor"/> class.
    /// </summary>
    public TodoExecutor()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoExecutor"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="logger">Optional logger instance.</param>
    public TodoExecutor(IServiceProvider serviceProvider, ILogger<TodoExecutor>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task InitializeAsync(Dictionary<string, object?>? configuration = null, CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(configuration, cancellationToken);

        // Try to get service provider from configuration if not already set
        if (_serviceProvider == null && configuration != null && configuration.TryGetValue("ServiceProvider", out var spObj) && spObj is IServiceProvider sp)
        {
            _serviceProvider = sp;
            _logger = sp.GetService<ILogger<TodoExecutor>>();
            _toolExecutor = sp.GetService<IToolExecutor>();
            _toolRegistry = sp.GetService<IToolRegistry>();
        }
    }

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        if (!parameters.TryGetValue("action", out var actionObj) || actionObj is not string action)
        {
            return ToolResult.Failure("Action parameter is required and must be a string");
        }

        action = action.ToLowerInvariant();

        try
        {
            // Get service provider from context if not already available
            if (_serviceProvider == null)
            {
                if (context.AdditionalData.TryGetValue("ServiceProvider", out var spObj) && spObj is IServiceProvider sp)
                {
                    _serviceProvider = sp;
                    _logger = sp.GetService<ILogger<TodoExecutor>>();
                    _toolExecutor = sp.GetService<IToolExecutor>();
                    _toolRegistry = sp.GetService<IToolRegistry>();
                }
                else
                {
                    return ToolResult.Failure("Service provider is not available. The tool requires dependency injection context.");
                }
            }

            return action switch
            {
                "execute_all" => await ExecuteAllTodosAsync(parameters, context),
                "execute_single" => await ExecuteSingleTodoAsync(parameters, context),
                "analyze" => await AnalyzeTodosAsync(parameters, context),
                "dry_run" => await DryRunTodosAsync(parameters, context),
                _ => ToolResult.Failure($"Unknown action: {action}")
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing todo executor action: {Action}", action);
            return ToolResult.Failure($"Error: {ex.Message}");
        }
    }

    private async Task<ToolResult> ExecuteAllTodosAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var todos = await GetTodosAsync();
        if (todos == null || !todos.Any())
        {
            return ToolResult.Success(new { message = "No todos found to execute", executed = 0 });
        }

        var executionResults = new List<object>();
        var filter = GetFilterFromParameters(parameters);
        var confirmCritical = parameters.TryGetValue("confirmCritical", out var confirmObj) && confirmObj is bool confirm ? confirm : true;

        foreach (var todo in todos)
        {
            if (!ShouldExecuteTodo(todo, filter))
            {
                continue;
            }

            var result = await ExecuteTodoTaskAsync(todo, confirmCritical, context);
            executionResults.Add(new
            {
                todoId = todo.Id,
                text = todo.Text,
                success = result.Success,
                message = result.Message,
                automated = result.WasAutomated
            });

            if (result.Success && result.WasAutomated)
            {
                await UpdateTodoProgressAsync(todo.Id, 100, "Completed automatically");
            }
        }

        var successCount = executionResults.Count(r => ((dynamic)r).success);
        var automatedCount = executionResults.Count(r => ((dynamic)r).automated);

        return ToolResult.Success(new
        {
            message = $"Executed {executionResults.Count} todos: {successCount} successful ({automatedCount} automated)",
            totalExecuted = executionResults.Count,
            successful = successCount,
            automated = automatedCount,
            results = executionResults
        });
    }

    private async Task<ToolResult> ExecuteSingleTodoAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        if (!parameters.TryGetValue("todoId", out var idObj) || !TryGetInt(idObj, out var todoId))
        {
            return ToolResult.Failure("todoId parameter is required and must be a number for execute_single action");
        }

        var todo = await GetTodoByIdAsync(todoId);
        if (todo == null)
        {
            return ToolResult.Failure($"Todo #{todoId} not found");
        }

        var confirmCritical = parameters.TryGetValue("confirmCritical", out var confirmObj) && confirmObj is bool confirm ? confirm : true;
        var result = await ExecuteTodoTaskAsync(todo, confirmCritical, context);

        if (result.Success && result.WasAutomated)
        {
            await UpdateTodoProgressAsync(todo.Id, 100, "Completed automatically");
        }

        return ToolResult.Success(new
        {
            todoId = todo.Id,
            text = todo.Text,
            success = result.Success,
            message = result.Message,
            automated = result.WasAutomated,
            executionDetails = result.Details
        });
    }

    private async Task<ToolResult> AnalyzeTodosAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var todos = await GetTodosAsync();
        if (todos == null || !todos.Any())
        {
            return ToolResult.Success(new { message = "No todos found to analyze", total = 0 });
        }

        var analysis = new List<object>();
        var filter = GetFilterFromParameters(parameters);

        foreach (var todo in todos)
        {
            if (!ShouldExecuteTodo(todo, filter))
            {
                continue;
            }

            var taskAnalysis = AnalyzeTodoTask(todo);
            analysis.Add(new
            {
                todoId = todo.Id,
                text = todo.Text,
                canAutomate = taskAnalysis.CanAutomate,
                taskType = taskAnalysis.TaskType,
                requiredTools = taskAnalysis.RequiredTools,
                estimatedComplexity = taskAnalysis.Complexity,
                warnings = taskAnalysis.Warnings
            });
        }

        var automatable = analysis.Count(a => ((dynamic)a).canAutomate);
        var manual = analysis.Count - automatable;

        return ToolResult.Success(new
        {
            message = $"Analyzed {analysis.Count} todos: {automatable} can be automated, {manual} require manual intervention",
            total = analysis.Count,
            automatable = automatable,
            manual = manual,
            analysis = analysis
        });
    }

    private async Task<ToolResult> DryRunTodosAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var todos = await GetTodosAsync();
        if (todos == null || !todos.Any())
        {
            return ToolResult.Success(new { message = "No todos found for dry run", total = 0 });
        }

        var dryRunResults = new List<object>();
        var filter = GetFilterFromParameters(parameters);

        foreach (var todo in todos)
        {
            if (!ShouldExecuteTodo(todo, filter))
            {
                continue;
            }

            var taskAnalysis = AnalyzeTodoTask(todo);
            var executionPlan = GenerateExecutionPlan(todo, taskAnalysis);

            dryRunResults.Add(new
            {
                todoId = todo.Id,
                text = todo.Text,
                canAutomate = taskAnalysis.CanAutomate,
                executionSteps = executionPlan.Steps,
                estimatedDuration = executionPlan.EstimatedDuration,
                requiredPermissions = executionPlan.RequiredPermissions
            });
        }

        return ToolResult.Success(new
        {
            message = $"Dry run completed for {dryRunResults.Count} todos",
            total = dryRunResults.Count,
            results = dryRunResults
        });
    }

    private async Task<(bool Success, string Message, bool WasAutomated, object? Details)> ExecuteTodoTaskAsync(dynamic todo, bool confirmCritical, ToolExecutionContext context)
    {
        var taskAnalysis = AnalyzeTodoTask(todo);

        if (!taskAnalysis.CanAutomate)
        {
            return (false, $"Task requires manual intervention: {taskAnalysis.Reason}", false, null);
        }

        var executionPlan = GenerateExecutionPlan(todo, taskAnalysis);

        if (taskAnalysis.IsCritical && confirmCritical)
        {
            _logger?.LogWarning("Critical operation requires confirmation: {TodoText}", (string)todo.Text);
            return (false, "Critical operation requires manual confirmation", false, executionPlan);
        }

        try
        {
            var executionDetails = new List<object>();

            foreach (var step in executionPlan.Steps)
            {
                var stepResult = await ExecuteStepAsync(step, context);
                executionDetails.Add(new
                {
                    step = step.Description,
                    tool = step.ToolId,
                    success = stepResult.Success,
                    output = stepResult.Data
                });

                if (!stepResult.Success)
                {
                    return (false, $"Failed at step: {step.Description}", true, executionDetails);
                }
            }

            return (true, "Task completed successfully", true, executionDetails);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing todo task: {TodoId}", (int)todo.Id);
            return (false, $"Execution error: {ex.Message}", true, null);
        }
    }

    private TaskAnalysis AnalyzeTodoTask(dynamic todo)
    {
        string text = ((string)todo.Text).ToLowerInvariant();
        var analysis = new TaskAnalysis
        {
            CanAutomate = false,
            TaskType = "unknown",
            RequiredTools = new List<string>(),
            Complexity = "low",
            Warnings = new List<string>()
        };

        // Pattern matching for different task types
        if (Regex.IsMatch(text, @"update.*target.*framework|update.*\.net|migrate.*\.net", RegexOptions.IgnoreCase))
        {
            analysis.CanAutomate = true;
            analysis.TaskType = "framework_update";
            analysis.RequiredTools.Add("file_editor");
            analysis.Complexity = "medium";
        }
        else if (Regex.IsMatch(text, @"update.*nuget|update.*package|restore.*package", RegexOptions.IgnoreCase))
        {
            analysis.CanAutomate = true;
            analysis.TaskType = "package_update";
            analysis.RequiredTools.Add("bash_command");
            analysis.Complexity = "low";
        }
        else if (Regex.IsMatch(text, @"run.*test|execute.*test|test.*suite", RegexOptions.IgnoreCase))
        {
            analysis.CanAutomate = true;
            analysis.TaskType = "test_execution";
            analysis.RequiredTools.Add("bash_command");
            analysis.Complexity = "low";
        }
        else if (Regex.IsMatch(text, @"update.*deployment|update.*script|modify.*config", RegexOptions.IgnoreCase))
        {
            analysis.CanAutomate = true;
            analysis.TaskType = "configuration_update";
            analysis.RequiredTools.Add("file_editor");
            analysis.RequiredTools.Add("file_search");
            analysis.Complexity = "medium";
            analysis.IsCritical = true;
            analysis.Warnings.Add("This operation modifies deployment or configuration files");
        }
        else if (Regex.IsMatch(text, @"refactor|optimize|review.*code|analyze", RegexOptions.IgnoreCase))
        {
            analysis.CanAutomate = false;
            analysis.TaskType = "code_review";
            analysis.Reason = "Code refactoring and review require human judgment";
        }
        else if (Regex.IsMatch(text, @"document|update.*doc|write.*guide", RegexOptions.IgnoreCase))
        {
            analysis.CanAutomate = false;
            analysis.TaskType = "documentation";
            analysis.Reason = "Documentation requires human writing and context";
        }
        else if (Regex.IsMatch(text, @"consult|engage|discuss|review.*guide", RegexOptions.IgnoreCase))
        {
            analysis.CanAutomate = false;
            analysis.TaskType = "consultation";
            analysis.Reason = "This task requires human interaction or external resources";
        }
        else if (Regex.IsMatch(text, @"build|compile|dotnet build", RegexOptions.IgnoreCase))
        {
            analysis.CanAutomate = true;
            analysis.TaskType = "build";
            analysis.RequiredTools.Add("bash_command");
            analysis.Complexity = "low";
        }

        return analysis;
    }

    private ExecutionPlan GenerateExecutionPlan(dynamic todo, TaskAnalysis analysis)
    {
        var plan = new ExecutionPlan
        {
            Steps = new List<ExecutionStep>(),
            EstimatedDuration = "1-5 minutes",
            RequiredPermissions = new List<string>()
        };

        switch (analysis.TaskType)
        {
            case "framework_update":
                plan.Steps.Add(new ExecutionStep
                {
                    Order = 1,
                    ToolId = "file_search",
                    Description = "Find all project files (.csproj)",
                    Parameters = new Dictionary<string, object?> { ["pattern"] = "*.csproj" }
                });
                plan.Steps.Add(new ExecutionStep
                {
                    Order = 2,
                    ToolId = "file_editor",
                    Description = "Update TargetFramework in project files",
                    Parameters = new Dictionary<string, object?> { ["action"] = "replace", ["search"] = "<TargetFramework>net\\d\\.\\d</TargetFramework>", ["replace"] = "<TargetFramework>net8.0</TargetFramework>" }
                });
                plan.RequiredPermissions.Add("FileSystem");
                break;

            case "package_update":
                plan.Steps.Add(new ExecutionStep
                {
                    Order = 1,
                    ToolId = "bash_command",
                    Description = "List outdated packages",
                    Parameters = new Dictionary<string, object?> { ["command"] = "dotnet list package --outdated" }
                });
                plan.Steps.Add(new ExecutionStep
                {
                    Order = 2,
                    ToolId = "bash_command",
                    Description = "Update packages",
                    Parameters = new Dictionary<string, object?> { ["command"] = "dotnet restore" }
                });
                plan.RequiredPermissions.Add("ProcessExecution");
                break;

            case "test_execution":
                plan.Steps.Add(new ExecutionStep
                {
                    Order = 1,
                    ToolId = "bash_command",
                    Description = "Run unit tests",
                    Parameters = new Dictionary<string, object?> { ["command"] = "dotnet test" }
                });
                plan.RequiredPermissions.Add("ProcessExecution");
                break;

            case "build":
                plan.Steps.Add(new ExecutionStep
                {
                    Order = 1,
                    ToolId = "bash_command",
                    Description = "Build solution",
                    Parameters = new Dictionary<string, object?> { ["command"] = "dotnet build" }
                });
                plan.RequiredPermissions.Add("ProcessExecution");
                break;
        }

        return plan;
    }

    private async Task<ToolResult> ExecuteStepAsync(ExecutionStep step, ToolExecutionContext parentContext)
    {
        if (_toolExecutor == null || _toolRegistry == null)
        {
            return ToolResult.Failure("Tool executor or registry not available");
        }

        try
        {
            var tool = _toolRegistry?.CreateTool(step.ToolId, _serviceProvider!);
            if (tool == null)
            {
                return ToolResult.Failure($"Tool '{step.ToolId}' not found");
            }

            var childContext = new ToolExecutionContext
            {
                Permissions = parentContext.Permissions,
                AdditionalData = parentContext.AdditionalData,
                CancellationToken = parentContext.CancellationToken
            };

            return await tool.ExecuteAsync(step.Parameters, childContext);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing step: {StepDescription}", step.Description);
            return ToolResult.Failure($"Step execution error: {ex.Message}");
        }
    }

    private async Task<List<TodoItem>?> GetTodosAsync()
    {
        using var scope = _serviceProvider!.CreateScope();
        var todoServiceType = Type.GetType("Andy.CLI.Services.ITodoService, Andy.CLI");
        if (todoServiceType == null)
        {
            return null;
        }

        var todoService = scope.ServiceProvider.GetService(todoServiceType);
        if (todoService == null)
        {
            return null;
        }

        dynamic service = todoService;
        dynamic todoList = await service.GetTodoListAsync();
        IEnumerable<dynamic> items = todoList.Items;

        var todos = new List<TodoItem>();
        foreach (var item in items)
        {
            if ((int)item.Status == 0 || (int)item.Status == 1) // Pending or InProgress
            {
                todos.Add(new TodoItem
                {
                    Id = (int)item.Id,
                    Text = (string)item.Text,
                    Status = item.Status.ToString(),
                    Priority = item.Priority.ToString(),
                    Progress = (int)item.Progress
                });
            }
        }

        return todos;
    }

    private async Task<TodoItem?> GetTodoByIdAsync(int todoId)
    {
        var todos = await GetTodosAsync();
        return todos?.FirstOrDefault(t => t.Id == todoId);
    }

    private async Task<bool> UpdateTodoProgressAsync(int todoId, int progress, string? currentAction)
    {
        using var scope = _serviceProvider!.CreateScope();
        var todoServiceType = Type.GetType("Andy.CLI.Services.ITodoService, Andy.CLI");
        if (todoServiceType == null)
        {
            return false;
        }

        var todoService = scope.ServiceProvider.GetService(todoServiceType);
        if (todoService == null)
        {
            return false;
        }

        dynamic service = todoService;
        return await service.UpdateProgressAsync(todoId, progress, currentAction);
    }

    private TodoFilter GetFilterFromParameters(Dictionary<string, object?> parameters)
    {
        var filter = new TodoFilter();

        if (parameters.TryGetValue("filter", out var filterObj) && filterObj is Dictionary<string, object?> filterDict)
        {
            if (filterDict.TryGetValue("status", out var statusObj) && statusObj is string status)
            {
                filter.Status = status;
            }

            if (filterDict.TryGetValue("priority", out var priorityObj) && priorityObj is string priority)
            {
                filter.Priority = priority;
            }

            if (filterDict.TryGetValue("tag", out var tagObj) && tagObj is string tag)
            {
                filter.Tag = tag;
            }
        }

        return filter;
    }

    private bool ShouldExecuteTodo(TodoItem todo, TodoFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.Status) && !todo.Status.Equals(filter.Status, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filter.Priority) && !todo.Priority.Equals(filter.Priority, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        // Tag filtering would require fetching full todo details
        return true;
    }

    private bool TryGetInt(object? value, out int result)
    {
        result = 0;

        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        if (value is long longValue && longValue >= int.MinValue && longValue <= int.MaxValue)
        {
            result = (int)longValue;
            return true;
        }

        if (value is double doubleValue && doubleValue >= int.MinValue && doubleValue <= int.MaxValue)
        {
            result = (int)doubleValue;
            return true;
        }

        if (value is JsonElement json && json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out result))
        {
            return true;
        }

        if (value is string str && int.TryParse(str, out result))
        {
            return true;
        }

        return false;
    }

    // Helper classes
    private class TodoItem
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
        public int Progress { get; set; }
    }

    private class TodoFilter
    {
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? Tag { get; set; }
    }

    private class TaskAnalysis
    {
        public bool CanAutomate { get; set; }
        public string TaskType { get; set; } = "";
        public List<string> RequiredTools { get; set; } = new();
        public string Complexity { get; set; } = "";
        public bool IsCritical { get; set; }
        public List<string> Warnings { get; set; } = new();
        public string? Reason { get; set; }
    }

    private class ExecutionPlan
    {
        public List<ExecutionStep> Steps { get; set; } = new();
        public string EstimatedDuration { get; set; } = "";
        public List<string> RequiredPermissions { get; set; } = new();
    }

    private class ExecutionStep
    {
        public int Order { get; set; }
        public string ToolId { get; set; } = "";
        public string Description { get; set; } = "";
        public Dictionary<string, object?> Parameters { get; set; } = new();
    }
}
