using Andy.Tools.Advanced.CachingSystem;
using Andy.Tools.Advanced.Configuration;
using Andy.Tools.Core;
using Andy.Tools.Library;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Examples;

public static class CachingExamples
{
    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Caching Examples ===\n");
        Console.WriteLine("Caching improves performance by storing tool results.\n");

        var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();
        var cache = serviceProvider.GetRequiredService<IToolExecutionCache>();

        // Example 1: Basic caching
        Console.WriteLine("1. Basic Caching:");
        await DemonstrateBasicCaching(toolExecutor);

        // Example 2: Cache expiration
        Console.WriteLine("\n2. Cache Expiration:");
        await DemonstrateCacheExpiration(toolExecutor);

        // Example 3: Cache invalidation
        Console.WriteLine("\n3. Cache Invalidation:");
        await DemonstrateCacheInvalidation(toolExecutor, cache);

        // Example 4: Cache statistics
        Console.WriteLine("\n4. Cache Statistics:");
        await DemonstrateCacheStatistics(cache);
    }

    private static async Task DemonstrateBasicCaching(IToolExecutor toolExecutor)
    {
        // Create a context with caching enabled
        var context = new ToolExecutionContext
        {
            AdditionalData = new Dictionary<string, object?>
            {
                ["EnableCaching"] = true,
                ["CacheTimeToLive"] = TimeSpan.FromMinutes(5),
                ["CachePriority"] = "Normal"
            }
        };

        // First execution - not cached
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "now",
            ["format"] = "yyyy-MM-dd HH:mm:ss.fff"
        };

        Console.WriteLine("First execution (not cached):");
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var result1 = await toolExecutor.ExecuteAsync("datetime", parameters, context);
        sw1.Stop();
        Console.WriteLine($"Result: {result1.Data}");
        Console.WriteLine($"Time: {sw1.ElapsedMilliseconds}ms");

        // Second execution - should be cached
        Console.WriteLine("\nSecond execution (cached):");
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var result2 = await toolExecutor.ExecuteAsync("datetime", parameters, context);
        sw2.Stop();
        Console.WriteLine($"Result: {result2.Data}");
        Console.WriteLine($"Time: {sw2.ElapsedMilliseconds}ms");
        Console.WriteLine($"Cache hit: {result2.Metadata?.GetValueOrDefault("cache_hit") ?? false}");

        // Wait a moment to show time hasn't changed
        await Task.Delay(1000);

        // Third execution - still cached
        Console.WriteLine("\nThird execution (still cached):");
        var result3 = await toolExecutor.ExecuteAsync("datetime", parameters, context);
        Console.WriteLine($"Result: {result3.Data} (note: same as previous)");
    }

    private static async Task DemonstrateCacheExpiration(IToolExecutor toolExecutor)
    {
        // Short TTL for demonstration
        var context = new ToolExecutionContext
        {
            AdditionalData = new Dictionary<string, object?>
            {
                ["EnableCaching"] = true,
                ["CacheTimeToLive"] = TimeSpan.FromSeconds(2),
                ["CachePriority"] = "Low"
            }
        };

        var parameters = new Dictionary<string, object?>
        {
            ["min"] = 1,
            ["max"] = 100
        };

        // Execute and cache
        Console.WriteLine("Initial execution:");
        var result1 = await toolExecutor.ExecuteAsync("random_number", parameters, context);
        Console.WriteLine($"Random number: {result1.Data}");

        // Immediate re-execution (cached)
        Console.WriteLine("\nImmediate re-execution (cached):");
        var result2 = await toolExecutor.ExecuteAsync("random_number", parameters, context);
        Console.WriteLine($"Random number: {result2.Data} (same as above)");

        // Wait for cache to expire
        Console.WriteLine("\nWaiting for cache to expire (2 seconds)...");
        await Task.Delay(2500);

        // Execute again (cache expired)
        Console.WriteLine("\nAfter expiration:");
        var result3 = await toolExecutor.ExecuteAsync("random_number", parameters, context);
        Console.WriteLine($"Random number: {result3.Data} (new value)");
    }

    private static async Task DemonstrateCacheInvalidation(IToolExecutor toolExecutor, IToolExecutionCache cache)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "cache-test.txt");
        
        try
        {
            var context = new ToolExecutionContext
            {
                WorkingDirectory = Path.GetTempPath(),
                AdditionalData = new Dictionary<string, object?>
                {
                    ["EnableCaching"] = true
                }
            };

            // Write initial content
            await File.WriteAllTextAsync(tempFile, "Initial content");

            // Read file (will be cached)
            var readParams = new Dictionary<string, object?>
            {
                ["file_path"] = "cache-test.txt"
            };

            Console.WriteLine("First read (not cached):");
            var result1 = await toolExecutor.ExecuteAsync("read_file", readParams, context);
            Console.WriteLine($"Content: {result1.Data}");

            // Update file content
            await File.WriteAllTextAsync(tempFile, "Updated content");

            // Read again (still cached - shows old content)
            Console.WriteLine("\nSecond read (cached - shows old content):");
            var result2 = await toolExecutor.ExecuteAsync("read_file", readParams, context);
            Console.WriteLine($"Content: {result2.Data}");

            // Invalidate cache for this file
            var cacheKey = GenerateCacheKey("read_file", readParams);
            await cache.InvalidateAsync(cacheKey);
            Console.WriteLine("\nCache invalidated");

            // Read again (cache invalidated - shows new content)
            Console.WriteLine("\nThird read (after invalidation):");
            var result3 = await toolExecutor.ExecuteAsync("read_file", readParams, context);
            Console.WriteLine($"Content: {result3.Data}");
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    private static async Task DemonstrateCacheStatistics(IToolExecutionCache cache)
    {
        var stats = await cache.GetStatisticsAsync();
        
        Console.WriteLine("Cache Statistics:");
        Console.WriteLine($"- Total entries: {stats.TotalEntries}");
        Console.WriteLine($"- Total size: {stats.TotalSizeBytes / 1024.0:F2} KB");
        Console.WriteLine($"- Hit rate: {stats.HitRatio:P1}");
        Console.WriteLine($"- Miss rate: {(1 - stats.HitRatio):P1}");
        Console.WriteLine($"- Eviction count: {stats.EvictionCount}");

        // Get per-tool statistics from cache statistics
        Console.WriteLine("\nPer-tool cache usage:");
        foreach (var kvp in stats.ToolStatistics.OrderByDescending(t => t.Value.HitCount))
        {
            Console.WriteLine($"- {kvp.Key}: {kvp.Value.HitCount} hits, {kvp.Value.MissCount} misses");
        }
    }

    private static string GenerateCacheKey(string toolId, Dictionary<string, object?> parameters)
    {
        // Simplified cache key generation
        var paramString = string.Join(",", parameters.OrderBy(p => p.Key)
            .Select(p => $"{p.Key}={p.Value}"));
        return $"tool:{toolId}:params:{paramString}";
    }
}

// Mock random number tool for demonstration
public class RandomNumberTool : ToolBase
{
    private static readonly Random _random = new();
    
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "random_number",
        Name = "Random Number Generator",
        Description = "Generates a random number",
        Version = "1.0.0",
        Category = ToolCategory.Utility,
        Parameters = new[]
        {
            new ToolParameter
            {
                Name = "min",
                Description = "Minimum value",
                Type = "integer",
                Required = false,
                DefaultValue = 0
            },
            new ToolParameter
            {
                Name = "max",
                Description = "Maximum value",
                Type = "integer",
                Required = false,
                DefaultValue = 100
            }
        }
    };

    protected override Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var min = GetParameter<int>(parameters, "min", 0);
        var max = GetParameter<int>(parameters, "max", 100);
        
        var value = _random.Next(min, max + 1);
        return Task.FromResult(ToolResult.Success(value));
    }
}