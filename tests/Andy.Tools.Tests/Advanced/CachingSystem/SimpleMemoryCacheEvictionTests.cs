using System;
using System.Threading.Tasks;
using Andy.Tools.Advanced.CachingSystem;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Advanced.CachingSystem;

/// <summary>
/// Regression tests for issue #18: the eviction loop could spin forever under the size lock when all
/// remaining entries were NeverEvict, and size accounting drifted when an entry was replaced with a
/// zero-size entry.
/// </summary>
public sealed class SimpleMemoryCacheEvictionTests : IDisposable
{
    private readonly SimpleMemoryCache _cache = new(maxSizeBytes: 100, cleanupInterval: TimeSpan.FromMinutes(5));

    public void Dispose() => _cache.Dispose();

    [Fact]
    public void Set_WhenAllEntriesAreNeverEvict_DoesNotHang()
    {
        // Fill the cache past capacity with NeverEvict entries.
        _cache.Set("a", "x", new SimpleCacheEntryOptions { SizeBytes = 60, Priority = CachePriority.NeverEvict });
        _cache.Set("b", "y", new SimpleCacheEntryOptions { SizeBytes = 60, Priority = CachePriority.NeverEvict });

        // Adding another over-capacity entry must return promptly instead of spinning forever.
        var completed = Task.Run(() =>
            _cache.Set("c", "z", new SimpleCacheEntryOptions { SizeBytes = 60, Priority = CachePriority.NeverEvict }));

        completed.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("eviction must not spin when nothing is evictable");
    }

    [Fact]
    public void Set_EvictsLruWhenOverCapacity()
    {
        _cache.Set("a", "x", new SimpleCacheEntryOptions { SizeBytes = 60, Priority = CachePriority.Normal });
        _cache.Set("b", "y", new SimpleCacheEntryOptions { SizeBytes = 60, Priority = CachePriority.Normal });

        // 'a' (LRU) should have been evicted to make room for 'b'.
        _cache.CurrentSizeBytes.Should().BeLessThanOrEqualTo(100);
        _cache.TryGetValue<string>("b", out _).Should().BeTrue();
    }

    [Fact]
    public void Set_ReplacingEntryWithZeroSize_DoesNotLeakSize()
    {
        _cache.Set("k", "big", new SimpleCacheEntryOptions { SizeBytes = 80 });
        _cache.CurrentSizeBytes.Should().Be(80);

        // Replacing with a zero-size entry must subtract the old size (previously it drifted upward).
        _cache.Set("k", "small", new SimpleCacheEntryOptions { SizeBytes = 0 });
        _cache.CurrentSizeBytes.Should().Be(0);
    }
}
