using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Andy.Tools.Advanced.CachingSystem;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Advanced.CachingSystem;

public class SimpleMemoryCacheTests : IDisposable
{
    private readonly SimpleMemoryCache _cache;

    public SimpleMemoryCacheTests()
    {
        _cache = new SimpleMemoryCache(maxSizeBytes: 1024 * 1024, cleanupInterval: TimeSpan.FromSeconds(1));
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    #region Basic Functionality Tests

    [Fact]
    public void Set_AndGet_ShouldStoreAndRetrieve()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        var options = new SimpleCacheEntryOptions();

        // Act
        _cache.Set(key, value, options);
        var found = _cache.TryGetValue<string>(key, out var retrievedValue);

        // Assert
        found.Should().BeTrue();
        retrievedValue.Should().Be(value);
    }

    [Fact]
    public void Get_NonExistentKey_ShouldReturnDefault()
    {
        // Act
        var found = _cache.TryGetValue<string>("non-existent", out var value);

        // Assert
        found.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void Remove_ShouldDeleteEntry()
    {
        // Arrange
        var key = "test-key";
        _cache.Set(key, "value", new SimpleCacheEntryOptions());

        // Act
        _cache.Remove(key);
        var found = _cache.TryGetValue<string>(key, out _);

        // Assert
        found.Should().BeFalse();
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        // Arrange
        _cache.Set("key1", "value1", new SimpleCacheEntryOptions());
        _cache.Set("key2", "value2", new SimpleCacheEntryOptions());
        _cache.Set("key3", "value3", new SimpleCacheEntryOptions());

        // Act
        _cache.Clear();

        // Assert
        _cache.Count.Should().Be(0);
        _cache.TryGetValue<string>("key1", out _).Should().BeFalse();
        _cache.TryGetValue<string>("key2", out _).Should().BeFalse();
        _cache.TryGetValue<string>("key3", out _).Should().BeFalse();
    }

    #endregion

    #region Expiration Tests

    [Fact]
    public void TryGetValue_ExpiredEntry_ShouldReturnFalseAndRemove()
    {
        // Arrange
        var key = "test-key";
        var options = new SimpleCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddMilliseconds(50)
        };
        _cache.Set(key, "value", options);

        Thread.Sleep(100); // Wait for expiration

        // Act
        var found = _cache.TryGetValue<string>(key, out _);

        // Assert
        found.Should().BeFalse();
        _cache.Count.Should().Be(0); // Entry should be removed
    }


    [Fact]
    public void SlidingExpiration_WithoutAccess_ShouldExpire()
    {
        // Arrange
        var key = "test-key";
        var options = new SimpleCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMilliseconds(100)
        };
        _cache.Set(key, "value", options);

        // Act - Wait without accessing
        Thread.Sleep(150);
        var found = _cache.TryGetValue<string>(key, out _);

        // Assert
        found.Should().BeFalse();
    }

    #endregion

    #region Size Management Tests

    [Fact]
    public void Set_ExceedingMaxSize_ShouldEvictLRU()
    {
        // Arrange
        var smallCache = new SimpleMemoryCache(maxSizeBytes: 300, cleanupInterval: TimeSpan.FromMinutes(1));
        
        // Add entries that fill the cache
        smallCache.Set("key1", "value1", new SimpleCacheEntryOptions { SizeBytes = 100 });
        smallCache.Set("key2", "value2", new SimpleCacheEntryOptions { SizeBytes = 100 });
        smallCache.Set("key3", "value3", new SimpleCacheEntryOptions { SizeBytes = 100 });

        // Access key2 to make it more recently used
        smallCache.TryGetValue<string>("key2", out _);

        // Act - Add a new entry that requires eviction
        smallCache.Set("key4", "value4", new SimpleCacheEntryOptions { SizeBytes = 100 });

        // Assert - key1 should be evicted (least recently used)
        smallCache.TryGetValue<string>("key1", out _).Should().BeFalse();
        smallCache.TryGetValue<string>("key2", out _).Should().BeTrue();
        smallCache.TryGetValue<string>("key3", out _).Should().BeTrue();
        smallCache.TryGetValue<string>("key4", out _).Should().BeTrue();

        smallCache.Dispose();
    }

    [Fact]
    public void Set_WithNeverEvictPriority_ShouldNotBeEvicted()
    {
        // Arrange
        var smallCache = new SimpleMemoryCache(maxSizeBytes: 200, cleanupInterval: TimeSpan.FromMinutes(1));
        
        smallCache.Set("important", "value", new SimpleCacheEntryOptions 
        { 
            SizeBytes = 100, 
            Priority = CachePriority.NeverEvict 
        });
        smallCache.Set("normal", "value", new SimpleCacheEntryOptions 
        { 
            SizeBytes = 100, 
            Priority = CachePriority.Normal 
        });

        // Act - Add entry that requires eviction
        smallCache.Set("new", "value", new SimpleCacheEntryOptions { SizeBytes = 100 });

        // Assert - normal entry should be evicted, not the NeverEvict one
        smallCache.TryGetValue<string>("important", out _).Should().BeTrue();
        smallCache.TryGetValue<string>("normal", out _).Should().BeFalse();
        smallCache.TryGetValue<string>("new", out _).Should().BeTrue();

        smallCache.Dispose();
    }

    [Fact]
    public void CurrentSizeBytes_ShouldTrackCacheSize()
    {
        // Arrange & Act
        _cache.Set("key1", "value1", new SimpleCacheEntryOptions { SizeBytes = 100 });
        var size1 = _cache.CurrentSizeBytes;

        _cache.Set("key2", "value2", new SimpleCacheEntryOptions { SizeBytes = 200 });
        var size2 = _cache.CurrentSizeBytes;

        _cache.Remove("key1");
        var size3 = _cache.CurrentSizeBytes;

        // Assert
        size1.Should().Be(100);
        size2.Should().Be(300);
        size3.Should().Be(200);
    }

    #endregion

    #region Eviction Callbacks Tests

    [Fact]
    public void Remove_ShouldTriggerEvictionCallback()
    {
        // Arrange
        var evictionCalled = false;
        var evictionKey = "";
        var evictionReason = EvictionReason.None;

        var options = new SimpleCacheEntryOptions();
        options.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
        {
            EvictionCallback = (key, value, reason, state) =>
            {
                evictionCalled = true;
                evictionKey = key.ToString()!;
                evictionReason = reason;
            }
        });

        _cache.Set("test-key", "test-value", options);

        // Act
        _cache.Remove("test-key");

        // Assert
        evictionCalled.Should().BeTrue();
        evictionKey.Should().Be("test-key");
        evictionReason.Should().Be(EvictionReason.Removed);
    }

    [Fact]
    public void Replace_ShouldTriggerEvictionCallbackWithReplacedReason()
    {
        // Arrange
        var evictionCalled = false;
        var evictionReason = EvictionReason.None;

        var options = new SimpleCacheEntryOptions();
        options.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
        {
            EvictionCallback = (key, value, reason, state) =>
            {
                evictionCalled = true;
                evictionReason = reason;
            }
        });

        _cache.Set("test-key", "old-value", options);

        // Act - Replace the entry
        _cache.Set("test-key", "new-value", new SimpleCacheEntryOptions());

        // Assert
        evictionCalled.Should().BeTrue();
        evictionReason.Should().Be(EvictionReason.Replaced);
    }

    [Fact]
    public void Clear_ShouldTriggerAllEvictionCallbacks()
    {
        // Arrange
        var evictionCount = 0;

        var options = new SimpleCacheEntryOptions();
        options.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
        {
            EvictionCallback = (key, value, reason, state) => evictionCount++
        });

        _cache.Set("key1", "value1", options);
        _cache.Set("key2", "value2", options);
        _cache.Set("key3", "value3", options);

        // Act
        _cache.Clear();

        // Assert
        evictionCount.Should().Be(3);
    }

    #endregion

    #region Priority Tests

    [Fact]
    public void Eviction_ShouldRespectPriorityOrder()
    {
        // Arrange
        var smallCache = new SimpleMemoryCache(maxSizeBytes: 300, cleanupInterval: TimeSpan.FromMinutes(1));
        
        smallCache.Set("low", "value", new SimpleCacheEntryOptions 
        { 
            SizeBytes = 100, 
            Priority = CachePriority.Low 
        });
        smallCache.Set("normal", "value", new SimpleCacheEntryOptions 
        { 
            SizeBytes = 100, 
            Priority = CachePriority.Normal 
        });
        smallCache.Set("high", "value", new SimpleCacheEntryOptions 
        { 
            SizeBytes = 100, 
            Priority = CachePriority.High 
        });

        // Act - Add entry that requires eviction
        smallCache.Set("new", "value", new SimpleCacheEntryOptions { SizeBytes = 100 });

        // Assert - Low priority should be evicted first
        smallCache.TryGetValue<string>("low", out _).Should().BeFalse();
        smallCache.TryGetValue<string>("normal", out _).Should().BeTrue();
        smallCache.TryGetValue<string>("high", out _).Should().BeTrue();

        smallCache.Dispose();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var random = new Random();
        var exceptions = new List<Exception>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var key = $"key{index % 10}";
                    var operation = random.Next(4);

                    switch (operation)
                    {
                        case 0: // Set
                            _cache.Set(key, $"value{index}", new SimpleCacheEntryOptions { SizeBytes = 10 });
                            break;
                        case 1: // Get
                            _cache.TryGetValue<string>(key, out _);
                            break;
                        case 2: // Remove
                            _cache.Remove(key);
                            break;
                        case 3: // Clear
                            if (index % 20 == 0) // Don't clear too often
                                _cache.Clear();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentSetAndGet_ShouldNotCorruptData()
    {
        // Arrange
        var iterations = 1000;
        var setTask = Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                _cache.Set($"key{i % 50}", i, new SimpleCacheEntryOptions());
            }
        });

        var getTask = Task.Run(() =>
        {
            var results = new List<bool>();
            for (int i = 0; i < iterations; i++)
            {
                if (_cache.TryGetValue<int>($"key{i % 50}", out var value))
                {
                    results.Add(value >= 0 && value < iterations);
                }
            }
            return results;
        });

        // Act
        await Task.WhenAll(setTask, getTask);
        var getResults = await getTask;

        // Assert
        getResults.Should().OnlyContain(x => x == true);
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void Keys_ShouldReturnAllCacheKeys()
    {
        // Arrange
        _cache.Set("key1", "value1", new SimpleCacheEntryOptions());
        _cache.Set("key2", "value2", new SimpleCacheEntryOptions());
        _cache.Set("key3", "value3", new SimpleCacheEntryOptions());

        // Act
        var keys = _cache.Keys.ToList();

        // Assert
        keys.Should().HaveCount(3);
        keys.Should().Contain("key1");
        keys.Should().Contain("key2");
        keys.Should().Contain("key3");
    }

    [Fact]
    public void Count_ShouldReturnNumberOfEntries()
    {
        // Arrange & Act
        _cache.Set("key1", "value1", new SimpleCacheEntryOptions());
        var count1 = _cache.Count;

        _cache.Set("key2", "value2", new SimpleCacheEntryOptions());
        var count2 = _cache.Count;

        _cache.Remove("key1");
        var count3 = _cache.Count;

        // Assert
        count1.Should().Be(1);
        count2.Should().Be(2);
        count3.Should().Be(1);
    }

    #endregion

    #region Cleanup Timer Tests

    [Fact]
    public async Task CleanupTimer_ShouldRemoveExpiredEntries()
    {
        // Arrange
        var cache = new SimpleMemoryCache(maxSizeBytes: 1024, cleanupInterval: TimeSpan.FromMilliseconds(100));
        
        cache.Set("expired", "value", new SimpleCacheEntryOptions 
        { 
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddMilliseconds(50) 
        });
        cache.Set("valid", "value", new SimpleCacheEntryOptions 
        { 
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(10) 
        });

        // Act - Wait for cleanup to run
        await Task.Delay(200);

        // Assert
        cache.TryGetValue<string>("expired", out _).Should().BeFalse();
        cache.TryGetValue<string>("valid", out _).Should().BeTrue();

        cache.Dispose();
    }

    #endregion

    #region Type Safety Tests

    [Fact]
    public void TryGetValue_WithWrongType_ShouldThrow()
    {
        // Arrange
        _cache.Set("key", "string value", new SimpleCacheEntryOptions());

        // Act & Assert
        Assert.Throws<InvalidCastException>(() =>
        {
            _cache.TryGetValue<int>("key", out _);
        });
    }

    [Fact]
    public void TryGetValue_WithCorrectType_ShouldSucceed()
    {
        // Arrange
        var complexObject = new TestObject { Id = 1, Name = "Test" };
        _cache.Set("key", complexObject, new SimpleCacheEntryOptions());

        // Act
        var found = _cache.TryGetValue<TestObject>("key", out var retrieved);

        // Assert
        found.Should().BeTrue();
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(1);
        retrieved.Name.Should().Be("Test");
    }

    private class TestObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    #endregion
}