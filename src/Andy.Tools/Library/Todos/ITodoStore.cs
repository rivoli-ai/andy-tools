using System.Collections.Generic;

namespace Andy.Tools.Library.Todos;

/// <summary>
/// Backing store for <see cref="TodoManagementTool"/>. A default in-memory implementation
/// (<see cref="InMemoryTodoStore"/>) is registered automatically, so the tool works in any host
/// with no wiring. Hosts that want persistence or to share state with their own UI can register
/// their own <see cref="ITodoStore"/> implementation instead.
/// </summary>
public interface ITodoStore
{
    /// <summary>Adds a todo and returns the created item (with its assigned id).</summary>
    TodoItem Add(string text, TodoPriority priority = TodoPriority.Medium, IEnumerable<string>? tags = null);

    /// <summary>Returns all todos, in creation order.</summary>
    IReadOnlyList<TodoItem> GetAll();

    /// <summary>Returns the todo with the given id, or null if it does not exist.</summary>
    TodoItem? GetById(int id);

    /// <summary>Marks the todo completed. Returns false if it does not exist or is already completed.</summary>
    bool Complete(int id);

    /// <summary>Removes the todo. Returns false if it does not exist.</summary>
    bool Remove(int id);

    /// <summary>
    /// Updates progress (0-100) and optional current-action text. Reaching 100 marks the todo
    /// completed. Returns false if the todo does not exist.
    /// </summary>
    bool UpdateProgress(int id, int progress, string? currentAction = null);

    /// <summary>Returns todos whose text or tags contain <paramref name="query"/> (case-insensitive).</summary>
    IReadOnlyList<TodoItem> Search(string query);

    /// <summary>Removes all completed todos and returns the number removed.</summary>
    int ClearCompleted();
}
