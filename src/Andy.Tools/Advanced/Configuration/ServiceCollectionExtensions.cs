using Andy.Tools.Advanced.CachingSystem;
using Andy.Tools.Advanced.MetricsCollection;
using Andy.Tools.Advanced.ToolChains;
using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Advanced.Configuration;

/// <summary>
/// Extension methods for registering advanced tool features.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds advanced tool features to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAdvancedToolFeatures(
        this IServiceCollection services,
        Action<AdvancedToolOptions>? configure = null)
    {
        var options = new AdvancedToolOptions();
        configure?.Invoke(options);

        // Register configuration
        services.Configure<ToolCacheOptions>(opt =>
        {
            opt.DefaultTimeToLive = options.CacheTimeToLive;
            opt.MaxSizeBytes = options.MaxCacheSizeBytes;
            opt.EnableStatistics = options.EnableMetrics;
        });

        services.Configure<ToolMetricsOptions>(opt =>
        {
            opt.MaxMetricsPerTool = options.MaxMetricsPerTool;
            opt.MetricsRetentionPeriod = options.MetricsRetentionPeriod;
            opt.EnableDetailedTracking = options.EnableDetailedMetrics;
        });

        // Store options for future use
        services.AddSingleton(options);

        // Register core services
        services.TryAddSingleton<IToolExecutionCache, MemoryToolExecutionCache>();
        services.TryAddSingleton<IToolMetricsCollector, InMemoryToolMetricsCollector>();

        // Additional services can be registered here

        // Register tool chain support
        services.TryAddTransient<ToolChainBuilder>();
        services.TryAddTransient<IToolChain>(sp =>
        {
            var builder = sp.GetRequiredService<ToolChainBuilder>();
            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// Adds tool chain support to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddToolChains(this IServiceCollection services)
    {
        services.TryAddTransient<ToolChainBuilder>();
        return services;
    }

    /// <summary>
    /// Adds a pre-built tool chain to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="chainId">The chain ID.</param>
    /// <param name="configureChain">Action to configure the chain.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddToolChain(
        this IServiceCollection services,
        string chainId,
        Action<IToolChain> configureChain)
    {
        services.AddSingleton<IToolChain>(sp =>
        {
            var builder = sp.GetRequiredService<ToolChainBuilder>();
            var chain = builder.WithId(chainId).Build();
            configureChain(chain);
            return chain;
        });

        return services;
    }
}
