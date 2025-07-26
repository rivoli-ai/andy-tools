# API Reference

Complete API documentation for Andy Tools framework.

## Core Interfaces

### ITool

The base interface that all tools must implement.

```csharp
namespace Andy.Tools.Core
{
    public interface ITool
    {
        /// <summary>
        /// Gets the metadata describing this tool.
        /// </summary>
        ToolMetadata Metadata { get; }
        
        /// <summary>
        /// Executes the tool with the given parameters.
        /// </summary>
        /// <param name="parameters">Input parameters for the tool.</param>
        /// <param name="context">Execution context providing environment and configuration.</param>
        /// <returns>The result of the tool execution.</returns>
        Task<ToolResult> ExecuteAsync(
            Dictionary<string, object?> parameters, 
            ToolExecutionContext context);
    }
}
```

### IToolExecutor

Interface for executing tools by ID.

```csharp
namespace Andy.Tools.Core
{
    public interface IToolExecutor
    {
        /// <summary>
        /// Executes a tool by its ID.
        /// </summary>
        /// <param name="toolId">The unique identifier of the tool.</param>
        /// <param name="parameters">Parameters to pass to the tool.</param>
        /// <param name="context">Optional execution context.</param>
        /// <returns>The result of the tool execution.</returns>
        Task<ToolResult> ExecuteAsync(
            string toolId,
            Dictionary<string, object?> parameters,
            ToolExecutionContext? context = null);
        
        /// <summary>
        /// Checks if a tool with the given ID exists.
        /// </summary>
        bool ToolExists(string toolId);
        
        /// <summary>
        /// Gets metadata for a tool without executing it.
        /// </summary>
        ToolMetadata? GetToolMetadata(string toolId);
    }
}
```

## Core Types

### ToolMetadata

Describes a tool's capabilities and requirements.

```csharp
namespace Andy.Tools.Core
{
    public class ToolMetadata
    {
        /// <summary>
        /// Unique identifier for the tool.
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// Display name of the tool.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Detailed description of what the tool does.
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Tool version string.
        /// </summary>
        public string Version { get; set; } = "1.0.0";
        
        /// <summary>
        /// Category for organizing tools.
        /// </summary>
        public ToolCategory Category { get; set; } = ToolCategory.Utility;
        
        /// <summary>
        /// Parameters accepted by the tool.
        /// </summary>
        public ToolParameter[] Parameters { get; set; } = Array.Empty<ToolParameter>();
        
        /// <summary>
        /// Required permissions for this tool.
        /// </summary>
        public ToolPermissionFlags RequiredPermissions { get; set; } = ToolPermissionFlags.None;
        
        /// <summary>
        /// Additional properties for extensibility.
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new();
        
        /// <summary>
        /// Tags for tool discovery and filtering.
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Examples of tool usage.
        /// </summary>
        public ToolExample[] Examples { get; set; } = Array.Empty<ToolExample>();
    }
}
```

### ToolParameter

Defines a parameter that a tool accepts.

```csharp
namespace Andy.Tools.Core
{
    public class ToolParameter
    {
        /// <summary>
        /// Parameter name (used as dictionary key).
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Human-readable description.
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Parameter type (string, integer, boolean, etc.).
        /// </summary>
        public string Type { get; set; } = "string";
        
        /// <summary>
        /// Whether this parameter is required.
        /// </summary>
        public bool Required { get; set; }
        
        /// <summary>
        /// Default value if not provided.
        /// </summary>
        public object? DefaultValue { get; set; }
        
        /// <summary>
        /// For string parameters, allowed values.
        /// </summary>
        public string[]? AllowedValues { get; set; }
        
        /// <summary>
        /// For string parameters, regex pattern to match.
        /// </summary>
        public string? Pattern { get; set; }
        
        /// <summary>
        /// For numeric parameters, minimum value.
        /// </summary>
        public double? MinValue { get; set; }
        
        /// <summary>
        /// For numeric parameters, maximum value.
        /// </summary>
        public double? MaxValue { get; set; }
        
        /// <summary>
        /// For array parameters, minimum length.
        /// </summary>
        public int? MinLength { get; set; }
        
        /// <summary>
        /// For array parameters, maximum length.
        /// </summary>
        public int? MaxLength { get; set; }
    }
}
```

### ToolResult

The result returned from tool execution.

```csharp
namespace Andy.Tools.Core
{
    public class ToolResult
    {
        /// <summary>
        /// Indicates if the tool execution was successful.
        /// </summary>
        public bool IsSuccessful { get; set; }
        
        /// <summary>
        /// The data returned by the tool (typically Dictionary<string, object?>).
        /// </summary>
        public object? Data { get; set; }
        
        /// <summary>
        /// Error message if execution failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Error code for programmatic error handling.
        /// </summary>
        public string? ErrorCode { get; set; }
        
        /// <summary>
        /// Additional metadata about the execution.
        /// </summary>
        public Dictionary<string, object?> Metadata { get; set; } = new();
        
        /// <summary>
        /// Execution duration.
        /// </summary>
        public TimeSpan? Duration { get; set; }
        
        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static ToolResult Success(object? data = null)
        {
            return new ToolResult
            {
                IsSuccessful = true,
                Data = data
            };
        }
        
        /// <summary>
        /// Creates a failure result.
        /// </summary>
        public static ToolResult Failure(string errorMessage, string? errorCode = null)
        {
            return new ToolResult
            {
                IsSuccessful = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }
}
```

### ToolExecutionContext

Provides context for tool execution.

```csharp
namespace Andy.Tools.Core
{
    public class ToolExecutionContext
    {
        /// <summary>
        /// Working directory for file operations.
        /// </summary>
        public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;
        
        /// <summary>
        /// Cancellation token for the operation.
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        
        /// <summary>
        /// Progress reporter for long-running operations.
        /// </summary>
        public IProgress<ToolProgress>? Progress { get; set; }
        
        /// <summary>
        /// Security permissions for this execution.
        /// </summary>
        public ToolPermissions Permissions { get; set; } = new();
        
        /// <summary>
        /// Resource limits for this execution.
        /// </summary>
        public ToolResourceLimits ResourceLimits { get; set; } = new();
        
        /// <summary>
        /// Variables shared between tools in a chain.
        /// </summary>
        public Dictionary<string, object?> Variables { get; set; } = new();
        
        /// <summary>
        /// User who initiated the execution.
        /// </summary>
        public string? UserId { get; set; }
        
        /// <summary>
        /// Correlation ID for tracking.
        /// </summary>
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    }
}
```

## Security Types

### ToolPermissions

Defines allowed operations for tool execution.

```csharp
namespace Andy.Tools.Core.Security
{
    public class ToolPermissions
    {
        /// <summary>
        /// Allow file system access.
        /// </summary>
        public bool FileSystemAccess { get; set; }
        
        /// <summary>
        /// Specific file system operations allowed.
        /// </summary>
        public FileSystemOperations FileSystemOperations { get; set; } = FileSystemOperations.None;
        
        /// <summary>
        /// Paths that can be accessed (null = all paths).
        /// </summary>
        public string[]? AllowedPaths { get; set; }
        
        /// <summary>
        /// Allow network access.
        /// </summary>
        public bool NetworkAccess { get; set; }
        
        /// <summary>
        /// Domains that can be accessed (null = all domains).
        /// </summary>
        public string[]? AllowedDomains { get; set; }
        
        /// <summary>
        /// Require HTTPS for network access.
        /// </summary>
        public bool RequireHttps { get; set; } = true;
        
        /// <summary>
        /// Allow process execution.
        /// </summary>
        public bool ProcessExecution { get; set; }
        
        /// <summary>
        /// Commands that can be executed (null = all commands).
        /// </summary>
        public string[]? AllowedCommands { get; set; }
        
        /// <summary>
        /// Allow system information access.
        /// </summary>
        public bool SystemInformationAccess { get; set; }
        
        /// <summary>
        /// Environment variable access level.
        /// </summary>
        public EnvironmentVariableAccess EnvironmentVariableAccess { get; set; } = EnvironmentVariableAccess.None;
    }
}
```

### ToolResourceLimits

Defines resource constraints for tool execution.

```csharp
namespace Andy.Tools.Core.Security
{
    public class ToolResourceLimits
    {
        /// <summary>
        /// Maximum execution time.
        /// </summary>
        public TimeSpan? MaxExecutionTime { get; set; }
        
        /// <summary>
        /// Maximum memory usage in MB.
        /// </summary>
        public long? MaxMemoryMB { get; set; }
        
        /// <summary>
        /// Maximum file size in MB for operations.
        /// </summary>
        public long? MaxFileSizeMB { get; set; }
        
        /// <summary>
        /// Maximum concurrent operations.
        /// </summary>
        public int? MaxConcurrentOperations { get; set; }
        
        /// <summary>
        /// Maximum output size in bytes.
        /// </summary>
        public long? MaxOutputSizeBytes { get; set; }
        
        /// <summary>
        /// Network request timeout.
        /// </summary>
        public TimeSpan? NetworkTimeout { get; set; }
        
        /// <summary>
        /// Maximum number of files that can be processed.
        /// </summary>
        public int? MaxFileCount { get; set; }
    }
}
```

## Framework Interfaces

### IToolRegistry

Manages tool registration and discovery.

```csharp
namespace Andy.Tools.Registry
{
    public interface IToolRegistry
    {
        /// <summary>
        /// Registers a tool instance.
        /// </summary>
        void Register(ITool tool);
        
        /// <summary>
        /// Registers a tool with a specific ID.
        /// </summary>
        void Register(string id, ITool tool);
        
        /// <summary>
        /// Attempts to get a tool by ID.
        /// </summary>
        bool TryGetTool(string id, out ITool? tool);
        
        /// <summary>
        /// Gets all registered tools.
        /// </summary>
        IEnumerable<ITool> GetAllTools();
        
        /// <summary>
        /// Gets tools by category.
        /// </summary>
        IEnumerable<ITool> GetToolsByCategory(ToolCategory category);
        
        /// <summary>
        /// Checks if a tool is registered.
        /// </summary>
        bool Contains(string id);
        
        /// <summary>
        /// Unregisters a tool.
        /// </summary>
        bool Unregister(string id);
        
        /// <summary>
        /// Event raised when a tool is registered.
        /// </summary>
        event EventHandler<ToolRegisteredEventArgs>? ToolRegistered;
        
        /// <summary>
        /// Event raised when a tool is unregistered.
        /// </summary>
        event EventHandler<ToolUnregisteredEventArgs>? ToolUnregistered;
    }
}
```

### IToolValidator

Validates tool parameters before execution.

```csharp
namespace Andy.Tools.Validation
{
    public interface IToolValidator
    {
        /// <summary>
        /// Validates parameters against tool metadata.
        /// </summary>
        ValidationResult Validate(
            ITool tool, 
            Dictionary<string, object?> parameters);
        
        /// <summary>
        /// Validates a single parameter.
        /// </summary>
        ValidationResult ValidateParameter(
            ToolParameter parameter, 
            object? value);
        
        /// <summary>
        /// Validates execution context.
        /// </summary>
        ValidationResult ValidateContext(
            ITool tool, 
            ToolExecutionContext context);
    }
}
```

### IToolLifecycleManager

Manages tool initialization and shutdown.

```csharp
namespace Andy.Tools.Framework
{
    public interface IToolLifecycleManager
    {
        /// <summary>
        /// Initializes all registered tools.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Shuts down all tools gracefully.
        /// </summary>
        Task ShutdownAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the current lifecycle state.
        /// </summary>
        LifecycleState State { get; }
        
        /// <summary>
        /// Event raised when lifecycle state changes.
        /// </summary>
        event EventHandler<LifecycleStateChangedEventArgs>? StateChanged;
    }
}
```

## Advanced Interfaces

### IToolChain

Represents a chain of tools to execute.

```csharp
namespace Andy.Tools.Advanced.ToolChains
{
    public interface IToolChain
    {
        /// <summary>
        /// Unique identifier for the chain.
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// Display name of the chain.
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Steps in the chain.
        /// </summary>
        IReadOnlyList<IToolChainStep> Steps { get; }
        
        /// <summary>
        /// Executes the entire chain.
        /// </summary>
        Task<ToolChainResult> ExecuteAsync(
            ToolExecutionContext context,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Event raised when a step completes.
        /// </summary>
        event EventHandler<StepCompletedEventArgs>? StepCompleted;
    }
}
```

### IToolExecutionCache

Caches tool execution results.

```csharp
namespace Andy.Tools.Advanced.CachingSystem
{
    public interface IToolExecutionCache
    {
        /// <summary>
        /// Gets a cached result.
        /// </summary>
        Task<ToolResult?> GetAsync(string cacheKey);
        
        /// <summary>
        /// Sets a cached result.
        /// </summary>
        Task SetAsync(
            string cacheKey, 
            ToolResult result, 
            TimeSpan? ttl = null);
        
        /// <summary>
        /// Removes a cached result.
        /// </summary>
        Task<bool> RemoveAsync(string cacheKey);
        
        /// <summary>
        /// Clears all cached results.
        /// </summary>
        Task ClearAsync();
        
        /// <summary>
        /// Invalidates cache entries by prefix.
        /// </summary>
        Task InvalidateByPrefixAsync(string prefix);
        
        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        Task<CacheStatistics> GetStatisticsAsync();
    }
}
```

### IToolMetricsCollector

Collects execution metrics.

```csharp
namespace Andy.Tools.Advanced.MetricsCollection
{
    public interface IToolMetricsCollector
    {
        /// <summary>
        /// Records a tool execution.
        /// </summary>
        void RecordExecution(
            string toolId, 
            TimeSpan duration, 
            bool success);
        
        /// <summary>
        /// Records resource usage.
        /// </summary>
        void RecordResourceUsage(
            string toolId, 
            ResourceUsage usage);
        
        /// <summary>
        /// Records a custom metric.
        /// </summary>
        void RecordCustomMetric(
            string toolId, 
            string metricName, 
            double value);
        
        /// <summary>
        /// Gets metrics for a tool.
        /// </summary>
        Task<ToolMetrics> GetToolMetricsAsync(string toolId);
        
        /// <summary>
        /// Gets aggregated metrics.
        /// </summary>
        Task<AggregatedMetrics> GetAggregatedMetricsAsync(
            DateTime from, 
            DateTime to);
    }
}
```

## Extension Methods

### ServiceCollection Extensions

```csharp
namespace Microsoft.Extensions.DependencyInjection
{
    public static class AndyToolsServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Andy Tools core services.
        /// </summary>
        public static IServiceCollection AddAndyTools(
            this IServiceCollection services,
            Action<ToolFrameworkOptions>? configure = null);
        
        /// <summary>
        /// Adds a specific tool.
        /// </summary>
        public static IServiceCollection AddTool<TTool>(
            this IServiceCollection services)
            where TTool : class, ITool;
        
        /// <summary>
        /// Adds advanced features.
        /// </summary>
        public static IServiceCollection AddAdvancedToolFeatures(
            this IServiceCollection services,
            Action<AdvancedFeaturesOptions>? configure = null);
        
        /// <summary>
        /// Adds tool discovery.
        /// </summary>
        public static IServiceCollection AddToolDiscovery(
            this IServiceCollection services,
            Action<DiscoveryOptions>? configure = null);
    }
}
```

## Configuration Options

### ToolFrameworkOptions

Core framework configuration.

```csharp
namespace Andy.Tools.Framework
{
    public class ToolFrameworkOptions
    {
        /// <summary>
        /// Enable detailed logging.
        /// </summary>
        public bool EnableDetailedLogging { get; set; }
        
        /// <summary>
        /// Register built-in tools.
        /// </summary>
        public bool RegisterBuiltInTools { get; set; } = true;
        
        /// <summary>
        /// Enable observability features.
        /// </summary>
        public bool EnableObservability { get; set; } = true;
        
        /// <summary>
        /// Default execution timeout.
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);
        
        /// <summary>
        /// Enable automatic validation.
        /// </summary>
        public bool EnableValidation { get; set; } = true;
        
        /// <summary>
        /// Maximum concurrent executions.
        /// </summary>
        public int MaxConcurrentExecutions { get; set; } = 100;
    }
}
```

### AdvancedFeaturesOptions

Advanced features configuration.

```csharp
namespace Andy.Tools.Advanced.Configuration
{
    public class AdvancedFeaturesOptions
    {
        /// <summary>
        /// Enable result caching.
        /// </summary>
        public bool EnableCaching { get; set; }
        
        /// <summary>
        /// Default cache TTL.
        /// </summary>
        public TimeSpan CacheTimeToLive { get; set; } = TimeSpan.FromMinutes(10);
        
        /// <summary>
        /// Enable metrics collection.
        /// </summary>
        public bool EnableMetrics { get; set; }
        
        /// <summary>
        /// Maximum metrics per tool.
        /// </summary>
        public int MaxMetricsPerTool { get; set; } = 10000;
        
        /// <summary>
        /// Enable tool chains.
        /// </summary>
        public bool EnableToolChains { get; set; } = true;
        
        /// <summary>
        /// Enable output limiting.
        /// </summary>
        public bool EnableOutputLimiting { get; set; } = true;
        
        /// <summary>
        /// Maximum output size in bytes.
        /// </summary>
        public long MaxOutputSizeBytes { get; set; } = 10 * 1024 * 1024;
    }
}
```

## Event Arguments

### ToolRegisteredEventArgs

```csharp
namespace Andy.Tools.Registry
{
    public class ToolRegisteredEventArgs : EventArgs
    {
        public string ToolId { get; }
        public ITool Tool { get; }
        public DateTime RegisteredAt { get; }
        
        public ToolRegisteredEventArgs(string toolId, ITool tool)
        {
            ToolId = toolId;
            Tool = tool;
            RegisteredAt = DateTime.UtcNow;
        }
    }
}
```

### StepCompletedEventArgs

```csharp
namespace Andy.Tools.Advanced.ToolChains
{
    public class StepCompletedEventArgs : EventArgs
    {
        public IToolChainStep Step { get; }
        public ToolResult Result { get; }
        public TimeSpan Duration { get; }
        public int StepIndex { get; }
        public int TotalSteps { get; }
        
        public StepCompletedEventArgs(
            IToolChainStep step, 
            ToolResult result,
            TimeSpan duration,
            int stepIndex,
            int totalSteps)
        {
            Step = step;
            Result = result;
            Duration = duration;
            StepIndex = stepIndex;
            TotalSteps = totalSteps;
        }
    }
}
```

## Enumerations

### ToolCategory

```csharp
namespace Andy.Tools.Core
{
    public enum ToolCategory
    {
        FileSystem,
        Text,
        Web,
        System,
        Data,
        Development,
        Security,
        Utility,
        Custom
    }
}
```

### ToolPermissionFlags

```csharp
namespace Andy.Tools.Core.Security
{
    [Flags]
    public enum ToolPermissionFlags
    {
        None = 0,
        FileSystemRead = 1,
        FileSystemWrite = 2,
        FileSystemDelete = 4,
        Network = 8,
        ProcessExecution = 16,
        SystemInformation = 32,
        EnvironmentVariables = 64,
        AllFileSystem = FileSystemRead | FileSystemWrite | FileSystemDelete,
        All = ~None
    }
}
```

### FileSystemOperations

```csharp
namespace Andy.Tools.Core.Security
{
    [Flags]
    public enum FileSystemOperations
    {
        None = 0,
        Read = 1,
        Write = 2,
        Delete = 4,
        Create = 8,
        List = 16,
        All = ~None
    }
}
```

### EnvironmentVariableAccess

```csharp
namespace Andy.Tools.Core.Security
{
    public enum EnvironmentVariableAccess
    {
        None,
        Read,
        Write,
        ReadWrite
    }
}
```

## Summary

This API reference covers the core interfaces, types, and extension points in Andy Tools. For implementation examples, see the [Examples and Tutorials](examples.md) guide.