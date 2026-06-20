using System;
using System.Collections.Generic;

namespace Andy.Tools.Library.Todos;

/// <summary>
/// Priority of a todo item.
/// </summary>
public enum TodoPriority
{
    /// <summary>Low priority.</summary>
    Low = 0,

    /// <summary>Medium priority (default).</summary>
    Medium = 1,

    /// <summary>High priority.</summary>
    High = 2
}

/// <summary>
/// Lifecycle status of a todo item.
/// </summary>
public enum TodoStatus
{
    /// <summary>Not started.</summary>
    Pending = 0,

    /// <summary>In progress.</summary>
    InProgress = 1,

    /// <summary>Completed.</summary>
    Completed = 2,

    /// <summary>Blocked.</summary>
    Blocked = 3,

    /// <summary>Cancelled.</summary>
    Cancelled = 4
}

/// <summary>
/// A single todo item managed by an <see cref="ITodoStore"/>.
/// </summary>
public sealed class TodoItem
{
    /// <summary>Stable, store-assigned identifier.</summary>
    public int Id { get; set; }

    /// <summary>The todo text.</summary>
    public string Text { get; set; } = "";

    /// <summary>The lifecycle status.</summary>
    public TodoStatus Status { get; set; } = TodoStatus.Pending;

    /// <summary>The priority.</summary>
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;

    /// <summary>Progress percentage (0-100).</summary>
    public int Progress { get; set; }

    /// <summary>Optional description of the action currently in progress.</summary>
    public string? CurrentAction { get; set; }

    /// <summary>Tags assigned to the todo.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>When the todo was created (UTC).</summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>When the todo was completed (UTC), if it has been.</summary>
    public DateTime? Completed { get; set; }
}
