using Andy.Tools.Core;
using Andy.Tools.Core.OutputLimiting;
using Andy.Tools.Discovery;
using Andy.Tools.Execution;
using Andy.Tools.Library;
using Andy.Tools.Observability;
using Andy.Tools.Registry;
using Andy.Tools.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

/// <summary>
/// Configuration options for the tool framework.
/// </summary>
public class ToolFrameworkOptions
{
    /// <summary>
    /// Gets or sets whether to automatically discover and register tools.
    /// </summary>
    public bool AutoDiscoverTools { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically register built-in tools.
    /// </summary>
    public bool RegisterBuiltInTools { get; set; } = true;

    /// <summary>
    /// Gets or sets the discovery options.
    /// </summary>
    public ToolDiscoveryOptions DiscoveryOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the default resource limits for tool execution.
    /// </summary>
    public ToolResourceLimits DefaultResourceLimits { get; set; } = new();

    /// <summary>
    /// Gets or sets the default permissions for tool execution.
    /// </summary>
    public ToolPermissions DefaultPermissions { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to enable security checks.
    /// </summary>
    public bool EnableSecurity { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable resource monitoring.
    /// </summary>
    public bool EnableResourceMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable observability features.
    /// </summary>
    public bool EnableObservability { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable detailed activity tracing.
    /// </summary>
    public bool EnableDetailedTracing { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to export metrics to external systems.
    /// </summary>
    public bool EnableMetricsExport { get; set; } = true;

    /// <summary>
    /// Gets or sets the metrics aggregation interval.
    /// </summary>
    public TimeSpan MetricsAggregationInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the observability data retention period.
    /// </summary>
    public TimeSpan ObservabilityRetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the maximum age for security violations before they are cleaned up.
    /// </summary>
    public TimeSpan SecurityViolationMaxAge { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets directories to scan for plugin tools.
    /// </summary>
    public List<string> PluginDirectories { get; set; } = [];
}

/// <summary>
/// Information about a tool registration for dependency injection.
/// </summary>
public class ToolRegistrationInfo
{
    /// <summary>
    /// Gets or sets the tool type.
    /// </summary>
    public Type ToolType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the configuration for the tool.
    /// </summary>
    public Dictionary<string, object?> Configuration { get; set; } = [];
}

/// <summary>
/// Interface for managing tool lifecycle events.
/// </summary>
public interface IToolLifecycleManager
{
    /// <summary>
    /// Initializes the tool framework.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the initialization.</returns>
    public Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers and registers tools based on the configured options.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of tools discovered and registered.</returns>
    public Task<int> DiscoverAndRegisterToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Shuts down the tool framework gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the shutdown.</returns>
    public Task ShutdownAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs periodic maintenance tasks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the maintenance work.</returns>
    public Task PerformMaintenanceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of the tool framework.
    /// </summary>
    /// <returns>The framework status.</returns>
    public ToolFrameworkStatus GetStatus();
}

/// <summary>
/// Status information about the tool framework.
/// </summary>
public class ToolFrameworkStatus
{
    /// <summary>
    /// Gets or sets whether the framework is initialized.
    /// </summary>
    public bool IsInitialized { get; set; }

    /// <summary>
    /// Gets or sets the number of registered tools.
    /// </summary>
    public int RegisteredToolsCount { get; set; }

    /// <summary>
    /// Gets or sets the number of active tool executions.
    /// </summary>
    public int ActiveExecutionsCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of tool executions.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the framework initialization time.
    /// </summary>
    public DateTimeOffset? InitializedAt { get; set; }

    /// <summary>
    /// Gets or sets the last maintenance time.
    /// </summary>
    public DateTimeOffset? LastMaintenanceAt { get; set; }

    /// <summary>
    /// Gets or sets any startup errors.
    /// </summary>
    public List<string> StartupErrors { get; set; } = [];
}

/// <summary>
/// Tool lifecycle manager implementation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolLifecycleManager"/> class.
/// </remarks>
public class ToolLifecycleManager(
    ToolFrameworkOptions options,
    IToolRegistry registry,
    IToolDiscovery discovery,
    ISecurityManager securityManager,
    IToolExecutor executor,
    IServiceProvider serviceProvider,
    IEnumerable<ToolRegistrationInfo> registrationInfos,
    ILogger<ToolLifecycleManager> logger) : IToolLifecycleManager
{
    private readonly ToolFrameworkOptions _options = options;
    private readonly IToolRegistry _registry = registry;
    private readonly IToolDiscovery _discovery = discovery;
    private readonly ISecurityManager _securityManager = securityManager;
    private readonly IToolExecutor _executor = executor;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ToolLifecycleManager> _logger = logger;
    private readonly List<ToolRegistrationInfo> _registrationInfos = [.. registrationInfos];
    private readonly ToolFrameworkStatus _status = new();

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing Andy Tools framework");

            // Register explicitly configured tools
            RegisterExplicitTools();

            // Discover and register tools if enabled
            if (_options.AutoDiscoverTools)
            {
                await DiscoverAndRegisterToolsAsync(cancellationToken);
            }

            _status.IsInitialized = true;
            _status.InitializedAt = DateTimeOffset.UtcNow;
            _status.RegisteredToolsCount = _registry.Tools.Count;

            _logger.LogInformation("Andy Tools framework initialized successfully with {ToolCount} tools",
                _status.RegisteredToolsCount);
        }
        catch (Exception ex)
        {
            _status.StartupErrors.Add(ex.Message);
            _logger.LogError(ex, "Failed to initialize Andy Tools framework");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DiscoverAndRegisterToolsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Discovering tools with options: AutoDiscover={AutoDiscover}",
                _options.AutoDiscoverTools);

            var discoveredTools = await _discovery.DiscoverToolsAsync(_options.DiscoveryOptions, cancellationToken);
            var registeredCount = 0;

            foreach (var discoveredTool in discoveredTools.Where(t => t.IsValid))
            {
                try
                {
                    // Skip if a tool with the same type is already registered
                    var alreadyRegistered = _registry.Tools.Any(r => r.ToolType == discoveredTool.ToolType);
                    if (alreadyRegistered)
                    {
                        _logger.LogDebug("Skipping already registered tool type: {ToolType}",
                            discoveredTool.ToolType.FullName);
                        continue;
                    }

                    _registry.RegisterTool(discoveredTool.ToolType);
                    registeredCount++;

                    _logger.LogDebug("Registered discovered tool: {ToolType}",
                        discoveredTool.ToolType.FullName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to register discovered tool: {ToolType}",
                        discoveredTool.ToolType.FullName);
                }
            }

            _logger.LogInformation("Discovered and registered {RegisteredCount} tools from {TotalCount} discovered",
                registeredCount, discoveredTools.Count);

            return registeredCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover and register tools");
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Shutting down Andy Tools framework");

            // Cancel any running executions
            var runningExecutions = _executor.GetRunningExecutions();
            foreach (var execution in runningExecutions)
            {
                try
                {
                    await _executor.CancelExecutionsAsync(execution.CorrelationId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cancel execution {CorrelationId} during shutdown",
                        execution.CorrelationId);
                }
            }

            _status.IsInitialized = false;
            _logger.LogInformation("Andy Tools framework shutdown completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Andy Tools framework shutdown");
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task PerformMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Performing Andy Tools framework maintenance");

            // Clean up old security violations
            var clearedViolations = _securityManager.ClearOldViolations(_options.SecurityViolationMaxAge);
            if (clearedViolations > 0)
            {
                _logger.LogInformation("Cleared {Count} old security violations during maintenance", clearedViolations);
            }

            _status.LastMaintenanceAt = DateTimeOffset.UtcNow;

            _logger.LogDebug("Andy Tools framework maintenance completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during Andy Tools framework maintenance");
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public ToolFrameworkStatus GetStatus()
    {
        var currentStatus = new ToolFrameworkStatus
        {
            IsInitialized = _status.IsInitialized,
            RegisteredToolsCount = _registry.Tools.Count,
            ActiveExecutionsCount = _executor.GetRunningExecutions().Count,
            InitializedAt = _status.InitializedAt,
            LastMaintenanceAt = _status.LastMaintenanceAt,
            StartupErrors = [.. _status.StartupErrors]
        };

        var stats = _executor.GetStatistics();
        currentStatus.TotalExecutions = stats.TotalExecutions;

        return currentStatus;
    }

    private void RegisterExplicitTools()
    {
        foreach (var registrationInfo in _registrationInfos)
        {
            try
            {
                _registry.RegisterTool(registrationInfo.ToolType, registrationInfo.Configuration);
                _logger.LogDebug("Registered explicit tool: {ToolType}", registrationInfo.ToolType.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register explicit tool: {ToolType}",
                    registrationInfo.ToolType.FullName);
            }
        }
    }
}

/// <summary>
/// Hosted service for managing the tool framework lifecycle.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolFrameworkHostedService"/> class.
/// </remarks>
/// <param name="lifecycleManager">The lifecycle manager.</param>
/// <param name="logger">The logger.</param>
public class ToolFrameworkHostedService(IToolLifecycleManager lifecycleManager, ILogger<ToolFrameworkHostedService> logger) : IHostedService
{
    private readonly IToolLifecycleManager _lifecycleManager = lifecycleManager;
    private readonly ILogger<ToolFrameworkHostedService> _logger = logger;
    private Timer? _maintenanceTimer;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _lifecycleManager.InitializeAsync(cancellationToken);

            // Start maintenance timer (run every hour)
            _maintenanceTimer = new Timer(PerformMaintenance, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

            _logger.LogInformation("Tool framework hosted service started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start tool framework hosted service");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _maintenanceTimer?.Dispose();
            await _lifecycleManager.ShutdownAsync(cancellationToken);
            _logger.LogInformation("Tool framework hosted service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping tool framework hosted service");
        }
    }

    private async void PerformMaintenance(object? state)
    {
        try
        {
            await _lifecycleManager.PerformMaintenanceAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during scheduled maintenance");
        }
    }
}
