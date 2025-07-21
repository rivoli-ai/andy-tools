namespace Andy.Tools.Core;

/// <summary>
/// Represents a tool registration in the registry.
/// </summary>
public class ToolRegistration
{
    /// <summary>
    /// Gets or sets the tool metadata.
    /// </summary>
    public ToolMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the tool type.
    /// </summary>
    public Type ToolType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the tool factory function.
    /// </summary>
    public Func<IServiceProvider, ITool>? Factory { get; set; }

    /// <summary>
    /// Gets or sets whether this tool is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the configuration for this tool.
    /// </summary>
    public Dictionary<string, object?> Configuration { get; set; } = [];

    /// <summary>
    /// Gets or sets when this tool was registered.
    /// </summary>
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the source of this tool registration (built-in, plugin, etc.).
    /// </summary>
    public string Source { get; set; } = "unknown";

    /// <summary>
    /// Gets or sets the assembly that contains this tool.
    /// </summary>
    public string? AssemblyName { get; set; }
}

/// <summary>
/// Interface for managing tool registrations and discovery.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    public IReadOnlyList<ToolRegistration> Tools { get; }

    /// <summary>
    /// Registers a tool with the registry.
    /// </summary>
    /// <typeparam name="T">The tool type.</typeparam>
    /// <param name="configuration">Optional configuration for the tool.</param>
    /// <returns>The tool registration.</returns>
    public ToolRegistration RegisterTool<T>(Dictionary<string, object?>? configuration = null) where T : class, ITool;

    /// <summary>
    /// Registers a tool with the registry using a factory function.
    /// </summary>
    /// <param name="metadata">The tool metadata.</param>
    /// <param name="factory">The factory function to create tool instances.</param>
    /// <param name="configuration">Optional configuration for the tool.</param>
    /// <returns>The tool registration.</returns>
    public ToolRegistration RegisterTool(ToolMetadata metadata, Func<IServiceProvider, ITool> factory, Dictionary<string, object?>? configuration = null);

    /// <summary>
    /// Registers a tool with the registry using a type.
    /// </summary>
    /// <param name="toolType">The tool type.</param>
    /// <param name="configuration">Optional configuration for the tool.</param>
    /// <returns>The tool registration.</returns>
    public ToolRegistration RegisterTool(Type toolType, Dictionary<string, object?>? configuration = null);

    /// <summary>
    /// Unregisters a tool from the registry.
    /// </summary>
    /// <param name="toolId">The ID of the tool to unregister.</param>
    /// <returns>True if the tool was unregistered, false if it wasn't found.</returns>
    public bool UnregisterTool(string toolId);

    /// <summary>
    /// Gets a tool registration by ID.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <returns>The tool registration, or null if not found.</returns>
    public ToolRegistration? GetTool(string toolId);

    /// <summary>
    /// Gets all tools that match the specified criteria.
    /// </summary>
    /// <param name="category">Optional category filter.</param>
    /// <param name="capabilities">Optional capabilities filter.</param>
    /// <param name="tags">Optional tags filter.</param>
    /// <param name="enabledOnly">Whether to return only enabled tools.</param>
    /// <returns>A list of matching tool registrations.</returns>
    public IReadOnlyList<ToolRegistration> GetTools(
        ToolCategory? category = null,
        ToolCapability? capabilities = null,
        IEnumerable<string>? tags = null,
        bool enabledOnly = true);

    /// <summary>
    /// Searches for tools by name or description.
    /// </summary>
    /// <param name="searchTerm">The search term.</param>
    /// <param name="enabledOnly">Whether to return only enabled tools.</param>
    /// <returns>A list of matching tool registrations.</returns>
    public IReadOnlyList<ToolRegistration> SearchTools(string searchTerm, bool enabledOnly = true);

    /// <summary>
    /// Creates an instance of a tool.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <returns>A new tool instance, or null if the tool is not found.</returns>
    public ITool? CreateTool(string toolId, IServiceProvider serviceProvider);

    /// <summary>
    /// Enables or disables a tool.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="enabled">Whether to enable or disable the tool.</param>
    /// <returns>True if the tool state was changed, false if the tool wasn't found.</returns>
    public bool SetToolEnabled(string toolId, bool enabled);

    /// <summary>
    /// Updates the configuration for a tool.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="configuration">The new configuration.</param>
    /// <returns>True if the configuration was updated, false if the tool wasn't found.</returns>
    public bool UpdateToolConfiguration(string toolId, Dictionary<string, object?> configuration);

    /// <summary>
    /// Gets statistics about the tool registry.
    /// </summary>
    /// <returns>Registry statistics.</returns>
    public ToolRegistryStatistics GetStatistics();

    /// <summary>
    /// Clears all tool registrations.
    /// </summary>
    public void Clear();

    /// <summary>
    /// Event raised when a tool is registered.
    /// </summary>
    public event EventHandler<ToolRegisteredEventArgs>? ToolRegistered;

    /// <summary>
    /// Event raised when a tool is unregistered.
    /// </summary>
    public event EventHandler<ToolUnregisteredEventArgs>? ToolUnregistered;
}

/// <summary>
/// Statistics about the tool registry.
/// </summary>
public class ToolRegistryStatistics
{
    /// <summary>
    /// Gets or sets the total number of registered tools.
    /// </summary>
    public int TotalTools { get; set; }

    /// <summary>
    /// Gets or sets the number of enabled tools.
    /// </summary>
    public int EnabledTools { get; set; }

    /// <summary>
    /// Gets or sets the number of disabled tools.
    /// </summary>
    public int DisabledTools { get; set; }

    /// <summary>
    /// Gets or sets the breakdown by category.
    /// </summary>
    public Dictionary<ToolCategory, int> ByCategory { get; set; } = [];

    /// <summary>
    /// Gets or sets the breakdown by source.
    /// </summary>
    public Dictionary<string, int> BySource { get; set; } = [];

    /// <summary>
    /// Gets or sets the breakdown by required capabilities.
    /// </summary>
    public Dictionary<ToolCapability, int> ByCapabilities { get; set; } = [];

    /// <summary>
    /// Gets or sets when these statistics were generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event arguments for tool registration events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolRegisteredEventArgs"/> class.
/// </remarks>
/// <param name="registration">The tool registration.</param>
public class ToolRegisteredEventArgs(ToolRegistration registration) : EventArgs
{
    /// <summary>
    /// Gets the tool registration.
    /// </summary>
    public ToolRegistration Registration { get; } = registration;
}

/// <summary>
/// Event arguments for tool unregistration events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolUnregisteredEventArgs"/> class.
/// </remarks>
/// <param name="toolId">The tool ID.</param>
/// <param name="registration">The tool registration.</param>
public class ToolUnregisteredEventArgs(string toolId, ToolRegistration registration) : EventArgs
{
    /// <summary>
    /// Gets the ID of the unregistered tool.
    /// </summary>
    public string ToolId { get; } = toolId;

    /// <summary>
    /// Gets the tool registration that was removed.
    /// </summary>
    public ToolRegistration Registration { get; } = registration;
}
