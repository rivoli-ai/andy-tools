using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Framework;

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
