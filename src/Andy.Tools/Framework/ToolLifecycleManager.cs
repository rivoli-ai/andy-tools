using Andy.Tools.Core;
using Andy.Tools.Discovery;
using Andy.Tools.Execution;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Framework;

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
                
                // If configured to fail on registration errors, rethrow the exception
                if (_options.FailOnExplicitToolRegistrationError)
                {
                    throw new InvalidOperationException(
                        $"Failed to register explicit tool: {registrationInfo.ToolType.FullName}", ex);
                }
            }
        }
    }
}
