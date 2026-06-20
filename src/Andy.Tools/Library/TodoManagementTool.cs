using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Andy.Tools.Library.Todos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Library;

/// <summary>
/// Tool that manages a todo list. The backing store is an <see cref="ITodoStore"/> resolved from
/// DI when available, falling back to a process-wide in-memory store so the tool works in any host
/// with no wiring.
/// </summary>
public class TodoManagementTool : ToolBase
{
    // Process-wide fallback used when no ITodoStore is registered in DI, so the tool is always
    // functional even in a host that did not wire one up.
    private static readonly ITodoStore SharedFallbackStore = new InMemoryTodoStore();

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
    protected override Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        // The store operations are synchronous; satisfy the async tool contract without a needless
        // state machine.
        return Task.FromResult(Execute(parameters, context));
    }

    private ToolResult Execute(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        if (!parameters.TryGetValue("action", out var actionObj) || actionObj is not string action)
        {
            return ToolResult.Failure("Action parameter is required and must be a string");
        }

        action = action.ToLowerInvariant();

        try
        {
            var store = ResolveStore(context);

            return action switch
            {
                "add" => AddTodo(store, parameters),
                "add_batch" => AddBatchTodos(store, parameters),
                "list" => ListTodos(store, parameters),
                "complete" or "done" => CompleteTodo(store, parameters),
                "remove" or "delete" => RemoveTodo(store, parameters),
                "update_progress" => UpdateProgress(store, parameters),
                "search" => SearchTodos(store, parameters),
                "clear_completed" => ClearCompleted(store),
                _ => ToolResult.Failure($"Unknown action: {action}")
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing todo management action: {Action}", action);
            return ToolResult.Failure($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves the todo store: a host-registered <see cref="ITodoStore"/> if one is available,
    /// otherwise a process-wide in-memory fallback so the tool always functions.
    /// </summary>
    private ITodoStore ResolveStore(ToolExecutionContext context)
    {
        if (_serviceProvider == null
            && context.AdditionalData.TryGetValue("ServiceProvider", out var spObj)
            && spObj is IServiceProvider sp)
        {
            _serviceProvider = sp;
            _logger ??= sp.GetService<ILogger<TodoManagementTool>>();
        }

        var store = _serviceProvider?.GetService<ITodoStore>();
        if (store == null)
        {
            _logger?.LogDebug("No ITodoStore registered; using the process-wide in-memory fallback store");
            store = SharedFallbackStore;
        }

        return store;
    }


    private ToolResult AddTodo(ITodoStore store, Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("text", out var textObj) || ToStringOrNull(textObj) is not string text || string.IsNullOrWhiteSpace(text))
        {
            return ToolResult.Failure("Text parameter is required for add action");
        }

        var priority = ParsePriority(parameters.TryGetValue("priority", out var p) ? p : null);
        var tags = ParseTags(parameters.TryGetValue("tags", out var t) ? t : null);

        var todo = store.Add(text, priority, tags);
        return ToolResult.Success(new
        {
            message = $"Added todo #{todo.Id}: {todo.Text}",
            id = todo.Id,
            text = todo.Text,
            priority = todo.Priority.ToString().ToLowerInvariant(),
            tags = todo.Tags.ToArray()
        });
    }

    private ToolResult AddBatchTodos(ITodoStore store, Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("todos", out var todosObj))
        {
            return ToolResult.Failure("Todos parameter is required for add_batch action");
        }

        if (!TryEnumerateTodoItems(todosObj, out var items))
        {
            return ToolResult.Failure("The 'todos' parameter must be an array of todo items.");
        }

        var addedTodos = new List<object>();
        var skipped = 0;
        foreach (var item in items)
        {
            if (!TryParseTodoItem(item, out var text, out var priority, out var tags))
            {
                skipped++;
                continue;
            }

            var todo = store.Add(text, priority, tags);
            addedTodos.Add(new
            {
                id = todo.Id,
                text = todo.Text,
                priority = todo.Priority.ToString().ToLowerInvariant(),
                tags = todo.Tags.ToArray()
            });
        }

        if (addedTodos.Count == 0)
        {
            return ToolResult.Failure("No valid todos were added. Each item must be a non-empty string or an object with a 'text' property.");
        }

        var message = $"Added {addedTodos.Count} todo(s) to your list";
        if (skipped > 0)
        {
            message += $" ({skipped} item(s) skipped - missing text)";
        }

        return ToolResult.Success(new
        {
            message,
            count = addedTodos.Count,
            skipped,
            todos = addedTodos
        });
    }

    private ToolResult ListTodos(ITodoStore store, Dictionary<string, object?> parameters)
    {
        IEnumerable<TodoItem> todos = store.GetAll();

        if (parameters.TryGetValue("status", out var statusObj) && ToStringOrNull(statusObj) is string statusFilter
            && TryParseStatus(statusFilter, out var status))
        {
            todos = todos.Where(td => td.Status == status);
        }

        if (parameters.TryGetValue("tag", out var tagObj) && ToStringOrNull(tagObj) is string tagFilter)
        {
            todos = todos.Where(td => td.Tags.Any(tag => tag.Equals(tagFilter, StringComparison.OrdinalIgnoreCase)));
        }

        var formatted = todos.Select(FormatTodo).ToList();

        var summary = $"Found {formatted.Count} todo(s)";
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
            count = formatted.Count,
            todos = formatted
        });
    }

    private ToolResult CompleteTodo(ITodoStore store, Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("id", out var idObj) || !TryGetInt(idObj, out var todoId))
        {
            return ToolResult.Failure("ID parameter is required and must be a number for complete action");
        }

        if (store.Complete(todoId))
        {
            return ToolResult.Success(new { message = $"Marked todo #{todoId} as completed", id = todoId });
        }

        return ToolResult.Failure($"Failed to complete todo #{todoId}. It may not exist or is already completed.");
    }

    private ToolResult RemoveTodo(ITodoStore store, Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("id", out var idObj) || !TryGetInt(idObj, out var todoId))
        {
            return ToolResult.Failure("ID parameter is required and must be a number for remove action");
        }

        if (store.Remove(todoId))
        {
            return ToolResult.Success(new { message = $"Removed todo #{todoId}", id = todoId });
        }

        return ToolResult.Failure($"Failed to remove todo #{todoId}. It may not exist.");
    }

    private ToolResult UpdateProgress(ITodoStore store, Dictionary<string, object?> parameters)
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

        var currentAction = parameters.TryGetValue("currentAction", out var actionObj) ? ToStringOrNull(actionObj) : null;

        if (store.UpdateProgress(todoId, progress, currentAction))
        {
            return ToolResult.Success(new
            {
                message = $"Updated todo #{todoId} progress to {progress}%",
                id = todoId,
                progress,
                currentAction
            });
        }

        return ToolResult.Failure($"Failed to update progress for todo #{todoId}. It may not exist.");
    }

    private ToolResult SearchTodos(ITodoStore store, Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("query", out var queryObj) || ToStringOrNull(queryObj) is not string query)
        {
            return ToolResult.Failure("Query parameter is required for search action");
        }

        var formatted = store.Search(query).Select(FormatTodo).ToList();
        return ToolResult.Success(new
        {
            message = $"Found {formatted.Count} todo(s) matching '{query}'",
            count = formatted.Count,
            todos = formatted
        });
    }

    private ToolResult ClearCompleted(ITodoStore store)
    {
        var removed = store.ClearCompleted();
        return ToolResult.Success(new { message = $"Removed {removed} completed todo(s)", count = removed });
    }

    private static object FormatTodo(TodoItem todo) => new
    {
        id = todo.Id,
        text = todo.Text,
        status = todo.Status.ToString().ToLowerInvariant(),
        priority = todo.Priority.ToString().ToLowerInvariant(),
        progress = todo.Progress,
        currentAction = todo.CurrentAction ?? "",
        tags = todo.Tags.ToArray(),
        createdAt = todo.Created.ToString("yyyy-MM-dd HH:mm"),
        completedAt = todo.Completed?.ToString("yyyy-MM-dd HH:mm") ?? ""
    };

    /// <summary>
    /// Parses one batch item into text/priority/tags. Accepts a bare string, a <see cref="JsonElement"/>
    /// (string or object), or any dictionary shape (<see cref="Dictionary{TKey, TValue}"/> with object or
    /// nullable-object values, or a non-generic <see cref="IDictionary"/>). Returns false when no usable
    /// non-empty text can be extracted. Internal for unit testing.
    /// </summary>
    internal static bool TryParseTodoItem(object? item, out string text, out TodoPriority priority, out List<string> tags)
    {
        text = "";
        priority = TodoPriority.Medium;
        tags = new List<string>();

        switch (item)
        {
            case null:
                return false;

            case string s:
                text = s;
                break;

            case JsonElement json when json.ValueKind == JsonValueKind.String:
                text = json.GetString() ?? "";
                break;

            case JsonElement json when json.ValueKind == JsonValueKind.Object:
                text = json.TryGetProperty("text", out var textProp) ? ToStringOrNull(textProp) ?? "" : "";
                if (json.TryGetProperty("priority", out var prioProp))
                {
                    priority = ParsePriority(prioProp);
                }
                if (json.TryGetProperty("tags", out var tagsProp))
                {
                    tags = ParseTags(tagsProp);
                }
                break;

            case IDictionary<string, object?> dict:
                text = ToStringOrNull(GetCaseInsensitive(dict, "text")) ?? "";
                priority = ParsePriority(GetCaseInsensitive(dict, "priority"));
                tags = ParseTags(GetCaseInsensitive(dict, "tags"));
                break;

            case IDictionary nonGenericDict:
                text = ToStringOrNull(GetCaseInsensitive(nonGenericDict, "text")) ?? "";
                priority = ParsePriority(GetCaseInsensitive(nonGenericDict, "priority"));
                tags = ParseTags(GetCaseInsensitive(nonGenericDict, "tags"));
                break;

            default:
                // Arbitrary object (e.g. an anonymous { text, priority, tags } as used in the tool's
                // own examples). Read named properties by reflection rather than fabricating a todo
                // from its ToString().
                text = ToStringOrNull(GetPropertyValue(item, "text")) ?? "";
                priority = ParsePriority(GetPropertyValue(item, "priority"));
                tags = ParseTags(GetPropertyValue(item, "tags"));
                break;
        }

        return !string.IsNullOrWhiteSpace(text);
    }

    private static bool TryEnumerateTodoItems(object? todosObj, out IEnumerable<object?> items)
    {
        switch (todosObj)
        {
            case JsonElement json when json.ValueKind == JsonValueKind.Array:
                items = json.EnumerateArray().Cast<object?>().ToList();
                return true;

            // A bare string is not an array of items.
            case string:
                items = Array.Empty<object?>();
                return false;

            case IEnumerable enumerable:
                items = enumerable.Cast<object?>().ToList();
                return true;

            default:
                items = Array.Empty<object?>();
                return false;
        }
    }

    private static TodoPriority ParsePriority(object? value)
    {
        var s = ToStringOrNull(value);
        return s?.ToLowerInvariant() switch
        {
            "high" => TodoPriority.High,
            "low" => TodoPriority.Low,
            _ => TodoPriority.Medium
        };
    }

    private static List<string> ParseTags(object? value)
    {
        var result = new List<string>();
        switch (value)
        {
            case null:
                break;

            case string single when !string.IsNullOrWhiteSpace(single):
                result.Add(single);
                break;

            case JsonElement json when json.ValueKind == JsonValueKind.Array:
                foreach (var el in json.EnumerateArray())
                {
                    var s = ToStringOrNull(el);
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        result.Add(s!);
                    }
                }
                break;

            case IEnumerable enumerable when value is not string:
                foreach (var el in enumerable)
                {
                    var s = ToStringOrNull(el);
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        result.Add(s!);
                    }
                }
                break;
        }

        return result;
    }

    // Unwraps a value to a string, transparently handling JsonElement so callers don't get
    // "System.Text.Json.JsonElement" from a naive ToString().
    private static string? ToStringOrNull(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            JsonElement json => json.ValueKind switch
            {
                JsonValueKind.String => json.GetString(),
                JsonValueKind.Number => json.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => json.ToString()
            },
            _ => value.ToString()
        };
    }

    private static object? GetCaseInsensitive(IDictionary<string, object?> dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value;
        }

        var match = dict.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        return match != null ? dict[match] : null;
    }

    private static object? GetCaseInsensitive(IDictionary dict, string key)
    {
        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Key is string k && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        return null;
    }

    private static object? GetPropertyValue(object? obj, string name)
    {
        if (obj == null)
        {
            return null;
        }

        var prop = obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop?.GetValue(obj);
    }

    private static bool TryParseStatus(string value, out TodoStatus status)
    {
        switch (value.ToLowerInvariant())
        {
            case "pending": status = TodoStatus.Pending; return true;
            case "inprogress": status = TodoStatus.InProgress; return true;
            case "completed": status = TodoStatus.Completed; return true;
            case "blocked": status = TodoStatus.Blocked; return true;
            case "cancelled": status = TodoStatus.Cancelled; return true;
            default: status = TodoStatus.Pending; return false;
        }
    }

    private static bool TryGetInt(object? value, out int result)
    {
        result = 0;

        switch (value)
        {
            case int i:
                result = i;
                return true;
            case long l when l >= int.MinValue && l <= int.MaxValue:
                result = (int)l;
                return true;
            case double d when d >= int.MinValue && d <= int.MaxValue:
                result = (int)d;
                return true;
            case JsonElement json when json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out result):
                return true;
            case string s when int.TryParse(s, out result):
                return true;
            default:
                return false;
        }
    }
}
