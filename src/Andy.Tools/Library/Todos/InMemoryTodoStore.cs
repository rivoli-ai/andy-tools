using System;
using System.Collections.Generic;
using System.Linq;

namespace Andy.Tools.Library.Todos;

/// <summary>
/// Default thread-safe, in-process <see cref="ITodoStore"/>. State lives for the lifetime of the
/// instance; register it as a singleton (the default via AddBuiltInTools) so todos persist across
/// tool invocations within a session.
/// </summary>
public sealed class InMemoryTodoStore : ITodoStore
{
    private readonly object _gate = new();
    private readonly List<TodoItem> _todos = new();
    private int _nextId = 1;

    /// <inheritdoc />
    public TodoItem Add(string text, TodoPriority priority = TodoPriority.Medium, IEnumerable<string>? tags = null)
    {
        lock (_gate)
        {
            var todo = new TodoItem
            {
                Id = _nextId++,
                Text = text,
                Priority = priority,
                Status = TodoStatus.Pending,
                Tags = tags?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? new List<string>(),
                Created = DateTime.UtcNow
            };
            _todos.Add(todo);
            return Clone(todo);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<TodoItem> GetAll()
    {
        lock (_gate)
        {
            return _todos.Select(Clone).ToList();
        }
    }

    /// <inheritdoc />
    public TodoItem? GetById(int id)
    {
        lock (_gate)
        {
            var todo = _todos.FirstOrDefault(t => t.Id == id);
            return todo == null ? null : Clone(todo);
        }
    }

    /// <inheritdoc />
    public bool Complete(int id)
    {
        lock (_gate)
        {
            var todo = _todos.FirstOrDefault(t => t.Id == id);
            if (todo == null || todo.Status == TodoStatus.Completed)
            {
                return false;
            }

            todo.Status = TodoStatus.Completed;
            todo.Progress = 100;
            todo.Completed = DateTime.UtcNow;
            return true;
        }
    }

    /// <inheritdoc />
    public bool Remove(int id)
    {
        lock (_gate)
        {
            var todo = _todos.FirstOrDefault(t => t.Id == id);
            return todo != null && _todos.Remove(todo);
        }
    }

    /// <inheritdoc />
    public bool UpdateProgress(int id, int progress, string? currentAction = null)
    {
        lock (_gate)
        {
            var todo = _todos.FirstOrDefault(t => t.Id == id);
            if (todo == null)
            {
                return false;
            }

            todo.Progress = Math.Clamp(progress, 0, 100);
            todo.CurrentAction = currentAction;
            if (todo.Progress >= 100)
            {
                todo.Status = TodoStatus.Completed;
                todo.Completed = DateTime.UtcNow;
            }
            else if (todo.Progress > 0 && todo.Status == TodoStatus.Pending)
            {
                todo.Status = TodoStatus.InProgress;
            }

            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<TodoItem> Search(string query)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return _todos.Select(Clone).ToList();
            }

            return _todos
                .Where(t => t.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || t.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .Select(Clone)
                .ToList();
        }
    }

    /// <inheritdoc />
    public int ClearCompleted()
    {
        lock (_gate)
        {
            return _todos.RemoveAll(t => t.Status == TodoStatus.Completed);
        }
    }

    // Hand out copies so callers can't mutate stored state without going through the store.
    private static TodoItem Clone(TodoItem t) => new()
    {
        Id = t.Id,
        Text = t.Text,
        Status = t.Status,
        Priority = t.Priority,
        Progress = t.Progress,
        CurrentAction = t.CurrentAction,
        Tags = new List<string>(t.Tags),
        Created = t.Created,
        Completed = t.Completed
    };
}
