using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Library;

/// <summary>
/// Tool that provides AI integration with the /todo command system.
/// </summary>
public class TodoManagementTool : ToolBase
{
    private IServiceProvider? _serviceProvider;
    private ILogger<TodoManagementTool>? _logger;

    /// <inheritdoc />
    public override ToolMetadata Metadata => new()
    {
        Id = "todo_management",
        Name = "Todo Management Tool",
        Description = "Manages todos in the built-in /todo command system. Use this when users ask to create, manage, or interact with todo lists.",
        Version = "1.0.0",
        Author = "Andy CLI",
        Category = ToolCategory.Productivity,
        Examples = new[]
        {
            new ToolExample
            {
                Description = "Add multiple todos at once",
                Parameters = new Dictionary<string, object?>
                {
                    ["action"] = "add_batch",
                    ["todos"] = new[]
                    {
                        new { text = "Buy groceries", priority = "medium" },
                        new { text = "Complete project report", priority = "high" },
                        new { text = "Call dentist", priority = "low" }
                    }
                }
            },
            new ToolExample
            {
                Description = "List all todos",
                Parameters = new Dictionary<string, object?>
                {
                    ["action"] = "list"
                }
            }
        },
        RequiredPermissions = ToolPermissionFlags.None,
        Parameters = new List<ToolParameter>
        {
            new ToolParameter
            {
                Name = "action",
                Type = "string",
                Description = "The action to perform: add, add_batch, list, complete, remove, update_progress, search, clear_completed",
                Required = true,
                AllowedValues = new object[] { "add", "add_batch", "list", "complete", "done", "remove", "delete", "update_progress", "search", "clear_completed" }
            },
            new ToolParameter
            {
                Name = "text",
                Type = "string",
                Description = "The todo text (required for 'add' action)",
                Required = false
            },
            new ToolParameter
            {
                Name = "todos",
                Type = "array",
                Description = "Array of todo items for batch add. Each item can be a string or an object with 'text', 'priority', and 'tags' properties",
                Required = false
            },
            new ToolParameter
            {
                Name = "id",
                Type = "number",
                Description = "The todo ID (required for complete, remove, and update_progress actions)",
                Required = false
            },
            new ToolParameter
            {
                Name = "priority",
                Type = "string",
                Description = "Todo priority: high, medium, or low (default: medium)",
                Required = false,
                DefaultValue = "medium",
                AllowedValues = new object[] { "high", "medium", "low" }
            },
            new ToolParameter
            {
                Name = "tags",
                Type = "array",
                Description = "Array of tags to assign to the todo",
                Required = false
            },
            new ToolParameter
            {
                Name = "progress",
                Type = "number",
                Description = "Progress percentage (0-100) for update_progress action",
                Required = false,
                MinValue = 0,
                MaxValue = 100
            },
            new ToolParameter
            {
                Name = "currentAction",
                Type = "string",
                Description = "Current action description for update_progress",
                Required = false
            },
            new ToolParameter
            {
                Name = "query",
                Type = "string",
                Description = "Search query for finding todos",
                Required = false
            },
            new ToolParameter
            {
                Name = "status",
                Type = "string",
                Description = "Filter by status: pending, inprogress, completed, blocked, cancelled",
                Required = false,
                AllowedValues = new object[] { "pending", "inprogress", "completed", "blocked", "cancelled" }
            },
            new ToolParameter
            {
                Name = "tag",
                Type = "string",
                Description = "Filter by tag",
                Required = false
            }
        }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoManagementTool"/> class.
    /// </summary>
    public TodoManagementTool()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoManagementTool"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="logger">Optional logger instance.</param>
    public TodoManagementTool(IServiceProvider serviceProvider, ILogger<TodoManagementTool>? logger = null)
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
            _logger = sp.GetService<ILogger<TodoManagementTool>>();
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
                // Try to get from AdditionalData
                if (context.AdditionalData.TryGetValue("ServiceProvider", out var spObj) && spObj is IServiceProvider sp)
                {
                    _serviceProvider = sp;
                    _logger = sp.GetService<ILogger<TodoManagementTool>>();
                }
                else
                {
                    return ToolResult.Failure("Service provider is not available. The tool requires dependency injection context.");
                }
            }

            // Get the ITodoService from DI container
            using var scope = _serviceProvider.CreateScope();
            var todoServiceType = Type.GetType("Andy.CLI.Services.ITodoService, Andy.CLI");

            if (todoServiceType == null)
            {
                _logger?.LogError("Could not find ITodoService type");
                return ToolResult.Failure("Todo service type is not available.");
            }

            var todoService = scope.ServiceProvider.GetService(todoServiceType);

            if (todoService == null)
            {
                _logger?.LogError("Todo service is not registered in the DI container");
                return ToolResult.Failure("Todo service is not available in the current context.");
            }

            // Use dynamic to work with the service without compile-time dependency
            dynamic service = todoService;

            return action switch
            {
                "add" => await AddTodoAsync(service, parameters),
                "add_batch" => await AddBatchTodosAsync(service, parameters),
                "list" => await ListTodosAsync(service, parameters),
                "complete" or "done" => await CompleteTodoAsync(service, parameters),
                "remove" or "delete" => await RemoveTodoAsync(service, parameters),
                "update_progress" => await UpdateProgressAsync(service, parameters),
                "search" => await SearchTodosAsync(service, parameters),
                "clear_completed" => await ClearCompletedAsync(service),
                _ => ToolResult.Failure($"Unknown action: {action}")
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing todo management action: {Action}", action);
            return ToolResult.Failure($"Error: {ex.Message}");
        }
    }

    private async Task<ToolResult> AddTodoAsync(dynamic todoService, Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("text", out var textObj) || textObj is not string text)
        {
            return ToolResult.Failure("Text parameter is required for add action");
        }

        var priorityStr = parameters.TryGetValue("priority", out var priorityObj) && priorityObj is string p ? p : "medium";
        priorityStr = priorityStr.ToLowerInvariant();

        // Get TodoPriority enum type and create enum value
        var todoPriorityType = Type.GetType("Andy.CLI.Models.TodoPriority, Andy.CLI");
        if (todoPriorityType == null)
        {
            return ToolResult.Failure("TodoPriority type not found");
        }

        // Map priority string to enum value
        object priorityValue = priorityStr switch
        {
            "high" => Enum.ToObject(todoPriorityType, 2),    // TodoPriority.High = 2
            "medium" => Enum.ToObject(todoPriorityType, 1),  // TodoPriority.Medium = 1
            "low" => Enum.ToObject(todoPriorityType, 0),     // TodoPriority.Low = 0
            _ => Enum.ToObject(todoPriorityType, 1)          // Default to Medium
        };

        IEnumerable<string>? tags = null;
        if (parameters.TryGetValue("tags", out var tagsObj))
        {
            var tagList = new List<string>();
            if (tagsObj is JsonElement tagsJson && tagsJson.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsJson.EnumerateArray())
                {
                    if (tag.ValueKind == JsonValueKind.String)
                    {
                        tagList.Add(tag.GetString() ?? "");
                    }
                }
            }
            else if (tagsObj is string[] tagsArray)
            {
                tagList.AddRange(tagsArray);
            }

            tags = tagList;
        }

        try
        {
            // Use reflection to call the method properly
            var addTodoMethod = todoService.GetType().GetMethod("AddTodoAsync");
            if (addTodoMethod == null)
            {
                return ToolResult.Failure("AddTodoAsync method not found");
            }

            // Invoke the method
            var task = (Task)addTodoMethod.Invoke(todoService, new object?[] { text, priorityValue, tags, null });
            await task;

            // Get the result
            var resultProperty = task.GetType().GetProperty("Result");
            if (resultProperty == null)
            {
                return ToolResult.Failure("Failed to get task result");
            }

            var result = resultProperty.GetValue(task);
            if (result == null)
            {
                return ToolResult.Failure("AddTodoAsync returned null");
            }

            dynamic todoItem = result;
            int todoId = todoItem.Id;

            return ToolResult.Success(new
            {
                message = $"Added todo #{todoId}: {text}",
                id = todoId,
                text = text,
                priority = priorityStr,
                tags = tags?.ToArray() ?? Array.Empty<string>()
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add todo");
            return ToolResult.Failure($"Failed to add todo: {ex.Message}");
        }
    }

    private async Task<ToolResult> AddBatchTodosAsync(dynamic todoService, Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("todos", out var todosObj))
        {
            return ToolResult.Failure("Todos parameter is required for add_batch action");
        }

        var addedTodos = new List<object>();
        IEnumerable<object>? todoItems = null;

        // Handle different input formats
        if (todosObj is JsonElement todosJson && todosJson.ValueKind == JsonValueKind.Array)
        {
            var items = new List<object>();
            foreach (var item in todosJson.EnumerateArray())
            {
                items.Add(item);
            }

            todoItems = items;
        }
        else if (todosObj is IEnumerable<object> enumerable)
        {
            todoItems = enumerable;
        }
        else
        {
            return ToolResult.Failure("The 'todos' parameter must be an array of todo items.");
        }

        foreach (var todoItem in todoItems)
        {
            try
            {
                string text;
                string priorityStr = "medium";
                IEnumerable<string>? tags = null;
                var tagList = new List<string>();

                if (todoItem is JsonElement todoElement)
                {
                    if (todoElement.ValueKind == JsonValueKind.String)
                    {
                        text = todoElement.GetString() ?? "";
                    }
                    else if (todoElement.ValueKind == JsonValueKind.Object)
                    {
                        text = todoElement.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";
                        if (todoElement.TryGetProperty("priority", out var priorityProp))
                        {
                            priorityStr = priorityProp.GetString() ?? "medium";
                        }

                        if (todoElement.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var tag in tagsProp.EnumerateArray())
                            {
                                if (tag.ValueKind == JsonValueKind.String)
                                {
                                    tagList.Add(tag.GetString() ?? "");
                                }
                            }
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
                else if (todoItem is string str)
                {
                    text = str;
                }
                else if (todoItem is Dictionary<string, object> dict)
                {
                    text = dict.TryGetValue("text", out var textVal) && textVal is string t ? t : "";
                    priorityStr = dict.TryGetValue("priority", out var priorityVal) && priorityVal is string p ? p : "medium";
                    if (dict.TryGetValue("tags", out var tagsVal) && tagsVal is IEnumerable<string> tagEnumerable)
                    {
                        tagList.AddRange(tagEnumerable);
                    }
                }
                else
                {
                    // Try to get text representation
                    text = todoItem?.ToString() ?? "";
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                // Set tags from tagList
                if (tagList.Count > 0)
                {
                    tags = tagList;
                }

                // Get TodoPriority enum type
                var todoPriorityType = Type.GetType("Andy.CLI.Models.TodoPriority, Andy.CLI");
                if (todoPriorityType == null)
                {
                    continue; // Skip this item if we can't get the enum type
                }

                // Map priority string to enum value
                object priorityValue = priorityStr.ToLowerInvariant() switch
                {
                    "high" => Enum.ToObject(todoPriorityType, 2),    // TodoPriority.High = 2
                    "medium" => Enum.ToObject(todoPriorityType, 1),  // TodoPriority.Medium = 1
                    "low" => Enum.ToObject(todoPriorityType, 0),     // TodoPriority.Low = 0
                    _ => Enum.ToObject(todoPriorityType, 1)          // Default to Medium
                };

                // Use reflection to call the method properly
                var addTodoMethod = todoService.GetType().GetMethod("AddTodoAsync");
                if (addTodoMethod == null)
                {
                    continue;
                }

                // Invoke the method
                var task = (Task)addTodoMethod.Invoke(todoService, new object?[] { text, priorityValue, tags, null });
                await task;

                // Get the result
                var resultProperty = task.GetType().GetProperty("Result");
                if (resultProperty == null)
                {
                    continue;
                }

                var result = resultProperty.GetValue(task);
                if (result == null)
                {
                    continue;
                }

                dynamic newTodoItem = result;
                int todoId = newTodoItem.Id;

                addedTodos.Add(new
                {
                    id = todoId,
                    text = text,
                    priority = priorityStr,
                    tags = tags?.ToArray() ?? Array.Empty<string>()
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to add todo item");
            }
        }

        if (addedTodos.Count == 0)
        {
            return ToolResult.Failure("No valid todos were added.");
        }

        return ToolResult.Success(new
        {
            message = $"Added {addedTodos.Count} todo(s) to your list",
            count = addedTodos.Count,
            todos = addedTodos
        });
    }

    private async Task<ToolResult> ListTodosAsync(dynamic todoService, Dictionary<string, object?> parameters)
    {
        try
        {
            dynamic todoList = await todoService.GetTodoListAsync();
            IEnumerable<dynamic> todos = todoList.Items;

            // Apply filters
            if (parameters.TryGetValue("status", out var statusObj) && statusObj is string statusFilter)
            {
                var statusValue = statusFilter.ToLowerInvariant() switch
                {
                    "pending" => 0,
                    "inprogress" => 1,
                    "completed" => 2,
                    "blocked" => 3,
                    "cancelled" => 4,
                    _ => 0
                };

                var filteredTodos = new List<dynamic>();
                foreach (var todo in todos)
                {
                    if ((int)todo.Status == statusValue)
                    {
                        filteredTodos.Add(todo);
                    }
                }

                todos = filteredTodos;
            }

            if (parameters.TryGetValue("tag", out var tagObj) && tagObj is string tagFilter)
            {
                var filteredTodos = new List<dynamic>();
                foreach (var todo in todos)
                {
                    List<string> tags = todo.Tags;
                    if (tags.Any(tag => tag.Equals(tagFilter, StringComparison.OrdinalIgnoreCase)))
                    {
                        filteredTodos.Add(todo);
                    }
                }

                todos = filteredTodos;
            }

            var formattedTodos = new List<object>();
            foreach (var todo in todos)
            {
                formattedTodos.Add(new
                {
                    id = (int)todo.Id,
                    text = (string)todo.Text,
                    status = todo.Status.ToString(),
                    priority = todo.Priority.ToString(),
                    progress = (int)todo.Progress,
                    currentAction = (string?)todo.CurrentAction ?? "",
                    tags = ((List<string>)todo.Tags).ToArray(),
                    createdAt = ((DateTime)todo.Created).ToString("yyyy-MM-dd HH:mm"),
                    completedAt = todo.Completed != null ? ((DateTime)todo.Completed).ToString("yyyy-MM-dd HH:mm") : ""
                });
            }

            var summary = $"Found {formattedTodos.Count} todo(s)";
            if (parameters.ContainsKey("status"))
            {
                summary += $" with status '{statusObj}'";
            }

            if (parameters.ContainsKey("tag"))
            {
                summary += $" tagged with '{tagObj}'";
            }

            return ToolResult.Success(new
            {
                message = summary,
                count = formattedTodos.Count,
                todos = formattedTodos
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list todos");
            return ToolResult.Failure($"Failed to list todos: {ex.Message}");
        }
    }

    private async Task<ToolResult> CompleteTodoAsync(dynamic todoService, Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("id", out var idObj) || !TryGetInt(idObj, out var todoId))
        {
            return ToolResult.Failure("ID parameter is required and must be a number for complete action");
        }

        try
        {
            bool success = await todoService.CompleteTodoAsync(todoId);

            if (success)
            {
                return ToolResult.Success(new
                {
                    message = $"Marked todo #{todoId} as completed",
                    id = todoId
                });
            }

            return ToolResult.Failure($"Failed to complete todo #{todoId}. It may not exist or is already completed.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to complete todo");
            return ToolResult.Failure($"Failed to complete todo: {ex.Message}");
        }
    }

    private async Task<ToolResult> RemoveTodoAsync(dynamic todoService, Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("id", out var idObj) || !TryGetInt(idObj, out var todoId))
        {
            return ToolResult.Failure("ID parameter is required and must be a number for remove action");
        }

        try
        {
            bool success = await todoService.RemoveTodoAsync(todoId);

            if (success)
            {
                return ToolResult.Success(new
                {
                    message = $"Removed todo #{todoId}",
                    id = todoId
                });
            }

            return ToolResult.Failure($"Failed to remove todo #{todoId}. It may not exist.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to remove todo");
            return ToolResult.Failure($"Failed to remove todo: {ex.Message}");
        }
    }

    private async Task<ToolResult> UpdateProgressAsync(dynamic todoService, Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("id", out var idObj) || !TryGetInt(idObj, out var todoId))
        {
            return ToolResult.Failure("ID parameter is required and must be a number for update_progress action");
        }

        if (!parameters.TryGetValue("progress", out var progressObj) || !TryGetInt(progressObj, out var progress))
        {
            return ToolResult.Failure("Progress parameter is required and must be a number for update_progress action");
        }

        if (progress < 0 || progress > 100)
        {
            return ToolResult.Failure("Progress must be between 0 and 100.");
        }

        var currentAction = parameters.TryGetValue("currentAction", out var actionObj) && actionObj is string action ? action : null;

        try
        {
            bool success = await todoService.UpdateProgressAsync(todoId, progress, currentAction);

            if (success)
            {
                return ToolResult.Success(new
                {
                    message = $"Updated todo #{todoId} progress to {progress}%",
                    id = todoId,
                    progress = progress,
                    currentAction = currentAction
                });
            }

            return ToolResult.Failure($"Failed to update progress for todo #{todoId}. It may not exist.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update todo progress");
            return ToolResult.Failure($"Failed to update progress: {ex.Message}");
        }
    }

    private async Task<ToolResult> SearchTodosAsync(dynamic todoService, Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("query", out var queryObj) || queryObj is not string query)
        {
            return ToolResult.Failure("Query parameter is required for search action");
        }

        try
        {
            IList<dynamic> todos = await todoService.SearchTodosAsync(query);

            var formattedTodos = new List<object>();
            foreach (var todo in todos)
            {
                formattedTodos.Add(new
                {
                    id = (int)todo.Id,
                    text = (string)todo.Text,
                    status = todo.Status.ToString(),
                    priority = todo.Priority.ToString(),
                    progress = (int)todo.Progress,
                    tags = ((List<string>)todo.Tags).ToArray()
                });
            }

            return ToolResult.Success(new
            {
                message = $"Found {formattedTodos.Count} todo(s) matching '{query}'",
                count = formattedTodos.Count,
                todos = formattedTodos
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search todos");
            return ToolResult.Failure($"Failed to search todos: {ex.Message}");
        }
    }

    private async Task<ToolResult> ClearCompletedAsync(dynamic todoService)
    {
        try
        {
            int removed = await todoService.ClearCompletedAsync();

            return ToolResult.Success(new
            {
                message = $"Removed {removed} completed todo(s)",
                count = removed
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clear completed todos");
            return ToolResult.Failure($"Failed to clear completed todos: {ex.Message}");
        }
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
}
