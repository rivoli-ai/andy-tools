using System.Collections.Concurrent;
using Andy.Tools.Core;
using Andy.Tools.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Registry;

/// <summary>
/// Thread-safe implementation of the tool registry.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolRegistry"/> class.
/// </remarks>
/// <param name="validator">The tool validator.</param>
/// <param name="logger">The logger.</param>
public class ToolRegistry(IToolValidator validator, ILogger<ToolRegistry> logger) : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ToolRegistration> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly IToolValidator _validator = validator;
    private readonly ILogger<ToolRegistry> _logger = logger;
    private readonly object _lockObject = new();

    /// <inheritdoc />
    public IReadOnlyList<ToolRegistration> Tools => _tools.Values.ToList().AsReadOnly();

    /// <inheritdoc />
    public event EventHandler<ToolRegisteredEventArgs>? ToolRegistered;

    /// <inheritdoc />
    public event EventHandler<ToolUnregisteredEventArgs>? ToolUnregistered;

    /// <inheritdoc />
    public ToolRegistration RegisterTool<T>(Dictionary<string, object?>? configuration = null) where T : class, ITool
    {
        return RegisterTool(typeof(T), configuration);
    }

    /// <inheritdoc />
    public ToolRegistration RegisterTool(ToolMetadata metadata, Func<IServiceProvider, ITool> factory, Dictionary<string, object?>? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(factory);

        // Validate metadata
        var validationResult = _validator.ValidateMetadata(metadata);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.Message));
            throw new ArgumentException($"Tool metadata validation failed: {errors}", nameof(metadata));
        }

        // Check if tool already exists
        if (_tools.ContainsKey(metadata.Id))
        {
            throw new InvalidOperationException($"Tool with ID '{metadata.Id}' is already registered");
        }

        var registration = new ToolRegistration
        {
            Metadata = metadata,
            ToolType = typeof(ITool), // Generic type since we're using a factory
            Factory = factory,
            Configuration = configuration ?? [],
            Source = "factory",
            RegisteredAt = DateTimeOffset.UtcNow
        };

        lock (_lockObject)
        {
            _tools.TryAdd(metadata.Id, registration);
        }

        _logger.LogInformation("Registered tool '{ToolName}' (ID: {ToolId}) from factory", metadata.Name, metadata.Id);
        ToolRegistered?.Invoke(this, new ToolRegisteredEventArgs(registration));

        return registration;
    }

    /// <inheritdoc />
    public ToolRegistration RegisterTool(Type toolType, Dictionary<string, object?>? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(toolType);

        // Validate tool type
        var typeValidationResult = _validator.ValidateToolType(toolType);
        if (!typeValidationResult.IsValid)
        {
            var errors = string.Join(", ", typeValidationResult.Errors.Select(e => e.Message));
            throw new ArgumentException($"Tool type validation failed: {errors}", nameof(toolType));
        }

        // Create a temporary instance to get metadata
        ITool? tempInstance = null;
        ToolMetadata metadata;

        try
        {
            tempInstance = (ITool)Activator.CreateInstance(toolType)!;
            metadata = tempInstance.Metadata;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create instance of tool type '{toolType.FullName}' to retrieve metadata", ex);
        }
        finally
        {
            if (tempInstance != null)
            {
                try
                {
                    _ = tempInstance.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispose temporary tool instance for type {ToolType}", toolType.FullName);
                }
            }
        }

        // Validate metadata
        var validationResult = _validator.ValidateMetadata(metadata);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.Message));
            throw new ArgumentException($"Tool metadata validation failed: {errors}");
        }

        // Check if tool already exists
        if (_tools.ContainsKey(metadata.Id))
        {
            throw new InvalidOperationException($"Tool with ID '{metadata.Id}' is already registered");
        }

        var registration = new ToolRegistration
        {
            Metadata = metadata,
            ToolType = toolType,
            Factory = serviceProvider => (ITool)ActivatorUtilities.CreateInstance(serviceProvider, toolType),
            Configuration = configuration ?? [],
            Source = "type",
            AssemblyName = toolType.Assembly.GetName().Name,
            RegisteredAt = DateTimeOffset.UtcNow
        };

        lock (_lockObject)
        {
            _tools.TryAdd(metadata.Id, registration);
        }

        _logger.LogInformation("Registered tool '{ToolName}' (ID: {ToolId}) from type {ToolType}",
            metadata.Name, metadata.Id, toolType.FullName);
        ToolRegistered?.Invoke(this, new ToolRegisteredEventArgs(registration));

        return registration;
    }

    /// <inheritdoc />
    public bool UnregisterTool(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            throw new ArgumentException("Tool ID cannot be null or empty", nameof(toolId));
        }

        lock (_lockObject)
        {
            if (_tools.TryRemove(toolId, out var registration))
            {
                _logger.LogInformation("Unregistered tool '{ToolName}' (ID: {ToolId})",
                    registration.Metadata.Name, toolId);
                ToolUnregistered?.Invoke(this, new ToolUnregisteredEventArgs(toolId, registration));
                return true;
            }
        }

        _logger.LogWarning("Attempted to unregister non-existent tool with ID: {ToolId}", toolId);
        return false;
    }

    /// <inheritdoc />
    public ToolRegistration? GetTool(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return null;
        }

        _tools.TryGetValue(toolId, out var registration);
        return registration;
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolRegistration> GetTools(
        ToolCategory? category = null,
        ToolCapability? capabilities = null,
        IEnumerable<string>? tags = null,
        bool enabledOnly = true)
    {
        var query = _tools.Values.AsEnumerable();

        if (enabledOnly)
        {
            query = query.Where(t => t.IsEnabled);
        }

        if (category.HasValue)
        {
            query = query.Where(t => t.Metadata.Category == category.Value);
        }

        if (capabilities.HasValue)
        {
            query = query.Where(t => t.Metadata.RequiredCapabilities.HasFlag(capabilities.Value));
        }

        if (tags != null)
        {
            var tagsList = tags.ToList();
            if (tagsList.Count > 0)
            {
                query = query.Where(t => tagsList.Any(tag =>
                    t.Metadata.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
            }
        }

        return query.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolRegistration> SearchTools(string searchTerm, bool enabledOnly = true)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return GetTools(enabledOnly: enabledOnly);
        }

        var query = _tools.Values.AsEnumerable();

        if (enabledOnly)
        {
            query = query.Where(t => t.IsEnabled);
        }

        // Search in name, description, and tags
        query = query.Where(t =>
            t.Metadata.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            t.Metadata.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            t.Metadata.Tags.Any(tag => tag.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

        return query.OrderBy(t => t.Metadata.Name).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public ITool? CreateTool(string toolId, IServiceProvider serviceProvider)
    {
        var registration = GetTool(toolId);
        if (registration == null)
        {
            _logger.LogWarning("Attempted to create instance of non-existent tool: {ToolId}", toolId);
            return null;
        }

        if (!registration.IsEnabled)
        {
            _logger.LogWarning("Attempted to create instance of disabled tool: {ToolId}", toolId);
            return null;
        }

        try
        {
            var tool = registration.Factory?.Invoke(serviceProvider);
            if (tool == null)
            {
                _logger.LogError("Factory returned null for tool: {ToolId}", toolId);
                return null;
            }

            _logger.LogDebug("Created instance of tool '{ToolName}' (ID: {ToolId})",
                registration.Metadata.Name, toolId);
            return tool;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create instance of tool '{ToolName}' (ID: {ToolId})",
                registration.Metadata.Name, toolId);
            return null;
        }
    }

    /// <inheritdoc />
    public bool SetToolEnabled(string toolId, bool enabled)
    {
        var registration = GetTool(toolId);
        if (registration == null)
        {
            return false;
        }

        lock (_lockObject)
        {
            registration.IsEnabled = enabled;
        }

        _logger.LogInformation("{Action} tool '{ToolName}' (ID: {ToolId})",
            enabled ? "Enabled" : "Disabled", registration.Metadata.Name, toolId);
        return true;
    }

    /// <inheritdoc />
    public bool UpdateToolConfiguration(string toolId, Dictionary<string, object?> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var registration = GetTool(toolId);
        if (registration == null)
        {
            return false;
        }

        lock (_lockObject)
        {
            registration.Configuration.Clear();
            foreach (var kvp in configuration)
            {
                registration.Configuration[kvp.Key] = kvp.Value;
            }
        }

        _logger.LogInformation("Updated configuration for tool '{ToolName}' (ID: {ToolId})",
            registration.Metadata.Name, toolId);
        return true;
    }

    /// <inheritdoc />
    public ToolRegistryStatistics GetStatistics()
    {
        var tools = _tools.Values.ToList();
        var stats = new ToolRegistryStatistics
        {
            TotalTools = tools.Count,
            EnabledTools = tools.Count(t => t.IsEnabled),
            DisabledTools = tools.Count(t => !t.IsEnabled)
        };

        // Group by category
        foreach (var group in tools.GroupBy(t => t.Metadata.Category))
        {
            stats.ByCategory[group.Key] = group.Count();
        }

        // Group by source
        foreach (var group in tools.GroupBy(t => t.Source))
        {
            stats.BySource[group.Key] = group.Count();
        }

        // Group by capabilities
        foreach (ToolCapability capability in Enum.GetValues<ToolCapability>())
        {
            if (capability == ToolCapability.None)
            {
                continue;
            }

            var count = tools.Count(t => t.Metadata.RequiredCapabilities.HasFlag(capability));
            if (count > 0)
            {
                stats.ByCapabilities[capability] = count;
            }
        }

        return stats;
    }

    /// <inheritdoc />
    public void Clear()
    {
        var removedTools = new List<ToolRegistration>();

        lock (_lockObject)
        {
            removedTools.AddRange(_tools.Values);
            _tools.Clear();
        }

        _logger.LogInformation("Cleared all {ToolCount} tools from registry", removedTools.Count);

        // Raise events for each removed tool
        foreach (var tool in removedTools)
        {
            ToolUnregistered?.Invoke(this, new ToolUnregisteredEventArgs(tool.Metadata.Id, tool));
        }
    }
}
