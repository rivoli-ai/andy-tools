namespace Andy.Tools.Framework;

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
