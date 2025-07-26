using Andy.Tools.Core;
using Andy.Tools.Core.OutputLimiting;
using Andy.Tools.Discovery;
using Andy.Tools.Execution;
using Andy.Tools.Framework;
using Andy.Tools.Library;
using Andy.Tools.Observability;
using Andy.Tools.Registry;
using Andy.Tools.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Andy.Tools;

/// <summary>
/// Extension methods for registering tool services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Andy Tools framework to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAndyTools(this IServiceCollection services)
    {
        return services.AddAndyTools(_ => { });
    }

    /// <summary>
    /// Adds the Andy Tools framework to the service collection with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure tool options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAndyTools(this IServiceCollection services, Action<ToolFrameworkOptions> configure)
    {
        var options = new ToolFrameworkOptions();
        configure(options);

        // Register options
        services.AddSingleton(options);

        // Register core services
        services.TryAddSingleton<IToolValidator, ToolValidator>();
        services.TryAddSingleton<IToolRegistry, ToolRegistry>();
        services.TryAddSingleton<IToolDiscovery, ToolDiscoveryService>();
        services.TryAddSingleton<ISecurityManager, SecurityManager>();
        services.TryAddSingleton<IResourceMonitor, ResourceMonitor>();
        services.TryAddSingleton<IToolOutputLimiter, ToolOutputLimiter>();
        services.TryAddSingleton<IToolExecutor, ToolExecutor>();
        services.TryAddSingleton<IPermissionProfileService, PermissionProfileService>();

        // Configure output limiter options
        services.Configure<ToolOutputLimiterOptions>(configuration =>
        {
            var config = services.BuildServiceProvider().GetService<IConfiguration>();
            config?.GetSection(ToolOutputLimiterOptions.SectionName).Bind(configuration);
        });

        // Integration services removed - they depend on Andy.GeminiClient

        // Register observability services if enabled
        if (options.EnableObservability)
        {
            services.Configure<ToolObservabilityOptions>(opt =>
            {
                opt.EnableDetailedTracing = options.EnableDetailedTracing;
                opt.EnableMetricsExport = options.EnableMetricsExport;
                opt.MetricsAggregationInterval = options.MetricsAggregationInterval;
                opt.RetentionPeriod = options.ObservabilityRetentionPeriod;
            });
            services.TryAddSingleton<IToolObservabilityService, ToolObservabilityService>();
        }

        // Register lifecycle service
        services.TryAddSingleton<IToolLifecycleManager, ToolLifecycleManager>();
        services.AddHostedService<ToolFrameworkHostedService>();

        // Register built-in tools if enabled
        if (options.RegisterBuiltInTools)
        {
            services.AddBuiltInTools();
        }

        return services;
    }

    /// <summary>
    /// Registers a tool with the service collection for dependency injection.
    /// </summary>
    /// <typeparam name="T">The tool type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Optional tool configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTool<T>(this IServiceCollection services, Dictionary<string, object?>? configuration = null)
        where T : class, ITool
    {
        services.TryAddTransient<T>();

        // Store tool registration information for later registration
        services.AddSingleton<ToolRegistrationInfo>(new ToolRegistrationInfo
        {
            ToolType = typeof(T),
            Configuration = configuration ?? []
        });

        return services;
    }

    /// <summary>
    /// Registers multiple tools from an assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan for tools.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddToolsFromAssembly(this IServiceCollection services, System.Reflection.Assembly assembly)
    {
        var toolTypes = assembly.GetTypes()
            .Where(t => typeof(ITool).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        foreach (var toolType in toolTypes)
        {
            services.TryAddTransient(toolType);
            services.AddSingleton(new ToolRegistrationInfo
            {
                ToolType = toolType,
                Configuration = []
            });
        }

        return services;
    }
}
