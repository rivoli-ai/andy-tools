using Andy.Tools.Advanced.CachingSystem;
using Andy.Tools.Advanced.MetricsCollection;
using Andy.Tools.Advanced.ToolChains;
using Andy.Tools.Core;
using Andy.Tools.Core.OutputLimiting;
using Andy.Tools.Execution;
using Andy.Tools.Registry;
using Andy.Tools.Validation;
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

        // Replace IToolExecutor with caching version if enabled
        if (options.EnableCaching)
        {
            // Remove the existing registration
            var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IToolExecutor));
            if (existingDescriptor != null)
            {
                services.Remove(existingDescriptor);
            }

            // Add the caching decorator
            services.AddSingleton<IToolExecutor>(sp =>
            {
                // Create the original executor
                var registry = sp.GetRequiredService<IToolRegistry>();
                var validator = sp.GetRequiredService<IToolValidator>();
                var securityManager = sp.GetRequiredService<ISecurityManager>();
                var resourceMonitor = sp.GetRequiredService<IResourceMonitor>();
                var outputLimiter = sp.GetRequiredService<IToolOutputLimiter>();
                var executorLogger = sp.GetRequiredService<ILogger<Execution.ToolExecutor>>();
                
                var innerExecutor = new Execution.ToolExecutor(
                    registry, validator, securityManager, resourceMonitor, 
                    outputLimiter, sp, executorLogger);

                // Wrap with caching
                var cache = sp.GetRequiredService<IToolExecutionCache>();
                var cachingLogger = sp.GetRequiredService<ILogger<CachingToolExecutor>>();
                return new CachingToolExecutor(innerExecutor, cache, cachingLogger);
            });
        }

        return services;
    }

    /// <summary>
    /// Adds tool chain support to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddToolChains(this IServiceCollection services)
    {
        // Ensure core dependencies are registered
        services.TryAddSingleton<IToolExecutor, Execution.ToolExecutor>();
        services.TryAddSingleton<IToolRegistry, ToolRegistry>();
        services.TryAddSingleton<IToolValidator, ToolValidator>();
        services.TryAddSingleton<ISecurityManager, SecurityManager>();
        services.TryAddSingleton<IResourceMonitor, ResourceMonitor>();
        services.TryAddSingleton<IToolOutputLimiter, ToolOutputLimiter>();
        services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
        
        // Register the ToolChainBuilder
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
