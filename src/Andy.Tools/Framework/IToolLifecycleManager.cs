namespace Andy.Tools.Framework;

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
