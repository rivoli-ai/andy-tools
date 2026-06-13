using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Andy.Tools.Advanced.CachingSystem;
using Andy.Tools.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Andy.Tools.Tests.Advanced.CachingSystem;

/// <summary>
/// Regression tests for issue #17: metadata.ExpiresAt was never set (broken cleanup/stats), dependency
/// invalidation was inverted (never cascaded), and the cache key ignored execution context.
/// </summary>
public sealed class CachingCorrectnessTests : IDisposable
{
    private readonly MemoryToolExecutionCache _cache;

    public CachingCorrectnessTests()
    {
        _cache = new MemoryToolExecutionCache(
            Options.Create(new ToolCacheOptions
            {
                MaxSizeBytes = 1024 * 1024,
                DefaultTimeToLive = TimeSpan.FromMinutes(5),
                CleanupInterval = TimeSpan.FromMinutes(5),
                EnableStatistics = true
            }),
            NullLogger<MemoryToolExecutionCache>.Instance);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task InvalidatingADependency_CascadesToDependents()
    {
        await _cache.SetAsync("parent", ToolResult.Success("p"), new CacheOptions());
        await _cache.SetAsync("child", ToolResult.Success("c"),
            new CacheOptions { Dependencies = ["parent"] });

        (await _cache.GetAsync("parent")).Should().NotBeNull();
        (await _cache.GetAsync("child")).Should().NotBeNull();

        // Invalidating the dependency must remove the dependent entry too.
        await _cache.InvalidateAsync("parent");

        (await _cache.GetAsync("parent")).Should().BeNull();
        (await _cache.GetAsync("child")).Should().BeNull("entries depending on 'parent' must be invalidated");
    }

    [Fact]
    public async Task DependencyCascade_DoesNotInfiniteLoopOnCycle()
    {
        // a depends on b, b depends on a.
        await _cache.SetAsync("a", ToolResult.Success("a"), new CacheOptions { Dependencies = ["b"] });
        await _cache.SetAsync("b", ToolResult.Success("b"), new CacheOptions { Dependencies = ["a"] });

        await _cache.InvalidateAsync("a"); // must terminate

        (await _cache.GetAsync("a")).Should().BeNull();
        (await _cache.GetAsync("b")).Should().BeNull();
    }

    [Fact]
    public void GenerateCacheKey_DiffersByWorkingDirectoryAndEnvironment()
    {
        var p = new Dictionary<string, object?> { ["x"] = 1 };

        var keyCwdA = _cache.GenerateCacheKey("tool", p, new CacheKeyContext
        {
            AdditionalContext = { ["__cwd"] = "/a" }
        });
        var keyCwdB = _cache.GenerateCacheKey("tool", p, new CacheKeyContext
        {
            AdditionalContext = { ["__cwd"] = "/b" }
        });
        keyCwdA.Should().NotBe(keyCwdB);

        var keyEnvA = _cache.GenerateCacheKey("tool", p, new CacheKeyContext
        {
            AdditionalContext = { ["__env:TOKEN"] = "one" }
        });
        var keyEnvB = _cache.GenerateCacheKey("tool", p, new CacheKeyContext
        {
            AdditionalContext = { ["__env:TOKEN"] = "two" }
        });
        keyEnvA.Should().NotBe(keyEnvB);
    }
}
