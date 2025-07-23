namespace Andy.Tools.Advanced.CachingSystem;

/// <summary>
/// Context for cache key generation.
/// </summary>
public class CacheKeyContext
{
    /// <summary>
    /// Gets or sets the user ID for user-specific caching.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the environment for environment-specific caching.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Gets or sets the version for version-specific caching.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets additional context values.
    /// </summary>
    public Dictionary<string, string> AdditionalContext { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to include parameter types in the cache key.
    /// </summary>
    public bool IncludeParameterTypes { get; set; } = false;

    /// <summary>
    /// Gets or sets parameters to exclude from cache key generation.
    /// </summary>
    public HashSet<string> ExcludedParameters { get; set; } = [];
}
