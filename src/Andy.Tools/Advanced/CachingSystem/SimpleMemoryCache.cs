using System.Collections.Concurrent;

namespace Andy.Tools.Advanced.CachingSystem;

/// <summary>
/// A simple thread-safe in-memory cache implementation without external dependencies.
/// </summary>
public class SimpleMemoryCache : IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly Timer _cleanupTimer;
    private readonly object _sizeLock = new();
    private long _currentSizeBytes;
    private readonly long _maxSizeBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleMemoryCache"/> class.
    /// </summary>
    /// <param name="maxSizeBytes">Maximum cache size in bytes.</param>
    /// <param name="cleanupInterval">Interval for cleanup of expired entries.</param>
    public SimpleMemoryCache(long maxSizeBytes, TimeSpan cleanupInterval)
    {
        _maxSizeBytes = maxSizeBytes;
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, cleanupInterval, cleanupInterval);
    }

    /// <summary>
    /// Gets a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The cached value, if found.</param>
    /// <returns>True if the value was found and not expired; otherwise, false.</returns>
    public bool TryGetValue<T>(string key, out T? value)
    {
        value = default;

        if (_cache.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired)
            {
                value = (T?)entry.Value;
                entry.LastAccessedAt = DateTimeOffset.UtcNow;
                return true;
            }

            // Remove expired entry
            Remove(key);
        }

        return false;
    }

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="options">Cache entry options.</param>
    public void Set(string key, object value, SimpleCacheEntryOptions options)
    {
        var entry = new CacheEntry
        {
            Key = key,
            Value = value,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            Priority = options.Priority,
            SizeBytes = options.SizeBytes,
            PostEvictionCallbacks = options.PostEvictionCallbacks.ToList()
        };

        // Set expiration
        if (options.AbsoluteExpiration.HasValue)
        {
            entry.AbsoluteExpiration = options.AbsoluteExpiration.Value;
        }
        else if (options.SlidingExpiration.HasValue)
        {
            entry.SlidingExpiration = options.SlidingExpiration.Value;
            entry.AbsoluteExpiration = DateTimeOffset.UtcNow.Add(options.SlidingExpiration.Value);
        }

        // Check if we need to make room
        if (_maxSizeBytes > 0 && options.SizeBytes > 0)
        {
            lock (_sizeLock)
            {
                // Remove existing entry size if updating
                if (_cache.TryGetValue(key, out var existingEntry))
                {
                    _currentSizeBytes -= existingEntry.SizeBytes;
                }

                // Evict entries if necessary
                while (_currentSizeBytes + options.SizeBytes > _maxSizeBytes && _cache.Count > 0)
                {
                    EvictLeastRecentlyUsed();
                }

                _currentSizeBytes += options.SizeBytes;
            }
        }

        // Add or update the entry
        _cache.AddOrUpdate(key, entry, (k, existing) =>
        {
            // Trigger eviction callbacks for replaced entry
            foreach (var callback in existing.PostEvictionCallbacks)
            {
                callback.EvictionCallback?.Invoke(k, existing.Value, EvictionReason.Replaced, callback.State);
            }
            return entry;
        });
    }

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    public void Remove(string key)
    {
        if (_cache.TryRemove(key, out var entry))
        {
            lock (_sizeLock)
            {
                _currentSizeBytes -= entry.SizeBytes;
            }

            // Trigger eviction callbacks
            foreach (var callback in entry.PostEvictionCallbacks)
            {
                callback.EvictionCallback?.Invoke(key, entry.Value, EvictionReason.Removed, callback.State);
            }
        }
    }

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    public void Clear()
    {
        var entries = _cache.ToArray();
        _cache.Clear();

        lock (_sizeLock)
        {
            _currentSizeBytes = 0;
        }

        // Trigger eviction callbacks
        foreach (var kvp in entries)
        {
            foreach (var callback in kvp.Value.PostEvictionCallbacks)
            {
                callback.EvictionCallback?.Invoke(kvp.Key, kvp.Value.Value, EvictionReason.Removed, callback.State);
            }
        }
    }

    /// <summary>
    /// Gets all cache keys.
    /// </summary>
    public IEnumerable<string> Keys => _cache.Keys;

    /// <summary>
    /// Gets the current number of entries in the cache.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Gets the current cache size in bytes.
    /// </summary>
    public long CurrentSizeBytes => _currentSizeBytes;

    private void CleanupExpiredEntries(object? state)
    {
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            if (_cache.TryRemove(key, out var entry))
            {
                lock (_sizeLock)
                {
                    _currentSizeBytes -= entry.SizeBytes;
                }

                // Trigger eviction callbacks
                foreach (var callback in entry.PostEvictionCallbacks)
                {
                    callback.EvictionCallback?.Invoke(key, entry.Value, EvictionReason.Expired, callback.State);
                }
            }
        }
    }

    private void EvictLeastRecentlyUsed()
    {
        // Find entries eligible for eviction (not NeverEvict priority)
        var evictableEntries = _cache
            .Where(kvp => kvp.Value.Priority != CachePriority.NeverEvict)
            .OrderBy(kvp => kvp.Value.Priority)
            .ThenBy(kvp => kvp.Value.LastAccessedAt)
            .ToList();

        if (evictableEntries.Count > 0)
        {
            var entryToEvict = evictableEntries.First();
            if (_cache.TryRemove(entryToEvict.Key, out var entry))
            {
                _currentSizeBytes -= entry.SizeBytes;

                // Trigger eviction callbacks
                foreach (var callback in entry.PostEvictionCallbacks)
                {
                    callback.EvictionCallback?.Invoke(entryToEvict.Key, entry.Value, EvictionReason.Capacity, callback.State);
                }
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        Clear();
    }

    private class CacheEntry
    {
        public string Key { get; set; } = string.Empty;
        public object? Value { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastAccessedAt { get; set; }
        public DateTimeOffset? AbsoluteExpiration { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public CachePriority Priority { get; set; }
        public long SizeBytes { get; set; }
        public List<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; set; } = [];

        public bool IsExpired
        {
            get
            {
                if (AbsoluteExpiration.HasValue && DateTimeOffset.UtcNow >= AbsoluteExpiration.Value)
                {
                    return true;
                }

                if (SlidingExpiration.HasValue)
                {
                    var timeSinceLastAccess = DateTimeOffset.UtcNow - LastAccessedAt;
                    if (timeSinceLastAccess >= SlidingExpiration.Value)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}

/// <summary>
/// Options for configuring a cache entry.
/// </summary>
public class SimpleCacheEntryOptions
{
    /// <summary>
    /// Gets or sets the absolute expiration date for the cache entry.
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; set; }

    /// <summary>
    /// Gets or sets how long a cache entry can be inactive before it should be evicted.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the priority for keeping the cache entry in the cache during cleanup.
    /// </summary>
    public CachePriority Priority { get; set; } = CachePriority.Normal;

    /// <summary>
    /// Gets or sets the size of the cache entry value in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the callbacks will be fired after the cache entry is evicted from the cache.
    /// </summary>
    public List<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; set; } = [];
}

/// <summary>
/// Specifies the reasons why an entry was evicted from the cache.
/// </summary>
public enum EvictionReason
{
    /// <summary>
    /// The item was removed explicitly.
    /// </summary>
    Removed,

    /// <summary>
    /// The item was replaced.
    /// </summary>
    Replaced,

    /// <summary>
    /// The item expired.
    /// </summary>
    Expired,

    /// <summary>
    /// The item was evicted due to memory pressure.
    /// </summary>
    Capacity,

    /// <summary>
    /// The item was evicted for an unknown reason.
    /// </summary>
    None
}

/// <summary>
/// Represents a callback that will be fired after a cache entry is evicted.
/// </summary>
public class PostEvictionCallbackRegistration
{
    /// <summary>
    /// Gets or sets the callback delegate to fire after an entry is evicted.
    /// </summary>
    public Action<object, object?, EvictionReason, object?>? EvictionCallback { get; set; }

    /// <summary>
    /// Gets or sets the state to pass to the callback.
    /// </summary>
    public object? State { get; set; }
}