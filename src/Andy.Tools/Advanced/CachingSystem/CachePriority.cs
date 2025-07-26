namespace Andy.Tools.Advanced.CachingSystem;

/// <summary>
/// Cache priority levels.
/// </summary>
public enum CachePriority
{
    /// <summary>Low priority - first to be evicted.</summary>
    Low,
    /// <summary>Normal priority.</summary>
    Normal,
    /// <summary>High priority - last to be evicted.</summary>
    High,
    /// <summary>Never evict (unless explicitly invalidated).</summary>
    NeverEvict
}
