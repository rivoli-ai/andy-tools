# Core Concepts

Understanding the core concepts of Andy Tools is essential for effective usage and extension of the framework.

## Tools

### What is a Tool?

A tool in Andy Tools is a self-contained unit of functionality that:
- Performs a specific, well-defined task
- Has clear input parameters and output format
- Declares its metadata and capabilities
- Handles errors gracefully

### The ITool Interface

```csharp
public interface ITool
{
    ToolMetadata Metadata { get; }
    Task<ToolResult> ExecuteAsync(
        Dictionary<string, object?> parameters, 
        ToolExecutionContext context
    );
}
```

Every tool must implement this interface, providing:
- **Metadata**: Describes the tool, its parameters, and requirements
- **ExecuteAsync**: The method that performs the tool's operation

### ToolMetadata

Metadata describes everything about a tool:

```csharp
public class ToolMetadata
{
    public string Id { get; set; }                    // Unique identifier
    public string Name { get; set; }                  // Display name
    public string Description { get; set; }           // What the tool does
    public string Version { get; set; }               // Tool version
    public ToolCategory Category { get; set; }        // Classification
    public ToolParameter[] Parameters { get; set; }   // Input parameters
    public ToolPermissionFlags RequiredPermissions { get; set; }
    public Dictionary<string, object> Properties { get; set; }
}
```

### ToolResult

The standardized output from any tool execution:

```csharp
public class ToolResult
{
    public bool IsSuccessful { get; set; }
    public object? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public Dictionary<string, object?> Metadata { get; set; }
}
```

## Tool Execution

### IToolExecutor

The central interface for executing tools:

```csharp
public interface IToolExecutor
{
    Task<ToolResult> ExecuteAsync(
        string toolId,
        Dictionary<string, object?> parameters,
        ToolExecutionContext? context = null
    );
}
```

### ToolExecutionContext

Provides environment and configuration for tool execution:

```csharp
public class ToolExecutionContext
{
    public string WorkingDirectory { get; set; }
    public CancellationToken CancellationToken { get; set; }
    public IProgress<ToolProgress>? Progress { get; set; }
    public ToolPermissions Permissions { get; set; }
    public ToolResourceLimits ResourceLimits { get; set; }
    public Dictionary<string, object?> Variables { get; set; }
}
```

Key properties:
- **WorkingDirectory**: Base directory for file operations
- **CancellationToken**: Allows cancellation of long-running operations
- **Progress**: Reports progress for operations with multiple steps
- **Permissions**: Security permissions for the execution
- **ResourceLimits**: Constraints on resource usage
- **Variables**: Shared data between tools in a chain

## Parameters

### ToolParameter Definition

Parameters define the inputs a tool accepts:

```csharp
public class ToolParameter
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public string[]? AllowedValues { get; set; }
    public string? Pattern { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
}
```

### Parameter Types

Supported parameter types:
- **string**: Text values
- **integer**: Whole numbers
- **number**: Decimal numbers
- **boolean**: True/false values
- **array**: Lists of values
- **object**: Complex objects (typically Dictionary<string, object?>)

### Parameter Validation

Parameters are automatically validated for:
- Required fields presence
- Type correctness
- Pattern matching (regex)
- Value ranges
- Allowed value lists

## Tool Registry

### IToolRegistry

Manages tool registration and discovery:

```csharp
public interface IToolRegistry
{
    void Register(ITool tool);
    void Register(string id, ITool tool);
    bool TryGetTool(string id, out ITool? tool);
    IEnumerable<ITool> GetAllTools();
    bool Contains(string id);
    bool Unregister(string id);
}
```

### Tool Discovery

Tools can be discovered automatically:
- Assembly scanning for types implementing ITool
- Plugin loading from external assemblies
- Manual registration via dependency injection

## Permissions and Security

### Permission Flags

Tools declare required permissions:

```csharp
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
    EnvironmentVariables = 64
}
```

### Security Context

Execution contexts specify allowed permissions:

```csharp
public class ToolPermissions
{
    public bool FileSystemAccess { get; set; }
    public string[]? AllowedPaths { get; set; }
    public bool NetworkAccess { get; set; }
    public string[]? AllowedDomains { get; set; }
    public bool ProcessExecution { get; set; }
    public bool SystemInformationAccess { get; set; }
}
```

## Resource Management

### Resource Limits

Control resource consumption:

```csharp
public class ToolResourceLimits
{
    public TimeSpan? MaxExecutionTime { get; set; }
    public long? MaxMemoryMB { get; set; }
    public long? MaxFileSizeMB { get; set; }
    public int? MaxConcurrentOperations { get; set; }
    public long? MaxOutputSizeBytes { get; set; }
}
```

### Output Limiting

Large outputs are automatically truncated:
- Text truncation preserves structure
- JSON/XML truncation maintains validity
- Binary data is limited by size
- Truncation is indicated in metadata

## Tool Lifecycle

### Initialization

1. Tool is discovered or registered
2. Metadata is validated
3. Tool is added to registry
4. Tool becomes available for execution

### Execution Flow

1. **Request**: Application calls ExecuteAsync
2. **Validation**: Parameters are validated
3. **Security Check**: Permissions are verified
4. **Resource Allocation**: Limits are applied
5. **Execution**: Tool performs its operation
6. **Result Processing**: Output is limited if needed
7. **Cleanup**: Resources are released
8. **Response**: Result is returned

### Error Handling

Tools should:
- Catch and handle expected errors
- Return meaningful error messages
- Include error codes for programmatic handling
- Clean up resources even on failure

## Best Practices

### Tool Design

1. **Single Responsibility**: Each tool should do one thing well
2. **Clear Parameters**: Use descriptive names and types
3. **Predictable Output**: Consistent result structure
4. **Error Handling**: Graceful failure with helpful messages
5. **Documentation**: Comprehensive metadata

### Performance

1. **Async Operations**: Use async/await throughout
2. **Cancellation Support**: Respect cancellation tokens
3. **Progress Reporting**: For long operations
4. **Resource Efficiency**: Clean up promptly

### Security

1. **Least Privilege**: Request minimal permissions
2. **Input Validation**: Never trust user input
3. **Path Sanitization**: Prevent directory traversal
4. **Output Sanitization**: Remove sensitive data

## Common Patterns

### Reading Tool Results

```csharp
var result = await executor.ExecuteAsync("some_tool", parameters);

if (result.IsSuccessful)
{
    // Most tools return Dictionary<string, object?>
    if (result.Data is Dictionary<string, object?> data)
    {
        var content = data["content"]?.ToString();
        // Process content
    }
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

### Handling Progress

```csharp
var progress = new Progress<ToolProgress>(p =>
{
    Console.WriteLine($"{p.PercentComplete}% - {p.Message}");
});

var context = new ToolExecutionContext
{
    Progress = progress
};

await executor.ExecuteAsync("long_running_tool", parameters, context);
```

### Using Cancellation

```csharp
var cts = new CancellationTokenSource();
var context = new ToolExecutionContext
{
    CancellationToken = cts.Token
};

// Start execution
var task = executor.ExecuteAsync("slow_tool", parameters, context);

// Cancel after 5 seconds
cts.CancelAfter(TimeSpan.FromSeconds(5));

try
{
    var result = await task;
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation was cancelled");
}
```

## Extension Points

### Custom Tool Base Classes

Extend ToolBase for common functionality:

```csharp
public abstract class FileToolBase : ToolBase
{
    protected string ResolvePath(string path, ToolExecutionContext context)
    {
        if (Path.IsPathRooted(path))
            return path;
        
        return Path.Combine(context.WorkingDirectory, path);
    }
    
    protected void ValidateFileAccess(string path, ToolExecutionContext context)
    {
        // Common file validation logic
    }
}
```

### Tool Decorators

Add cross-cutting concerns via decoration:

```csharp
public class LoggingToolDecorator : ITool
{
    private readonly ITool _innerTool;
    private readonly ILogger _logger;
    
    public async Task<ToolResult> ExecuteAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        _logger.LogInformation($"Executing {_innerTool.Metadata.Id}");
        
        var result = await _innerTool.ExecuteAsync(parameters, context);
        
        _logger.LogInformation($"Completed {_innerTool.Metadata.Id}: {result.IsSuccessful}");
        
        return result;
    }
}
```

## Summary

Understanding these core concepts enables you to:
- Use tools effectively with proper parameter passing
- Handle results correctly with appropriate error checking
- Configure execution contexts for security and resource management
- Create custom tools following established patterns
- Extend the framework with new capabilities

Next, explore the [Built-in Tools Reference](tools-reference.md) to see these concepts in action.