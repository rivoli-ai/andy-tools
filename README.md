# Andy Tools

> ⚠️ **ALPHA RELEASE WARNING** ⚠️
> 
> This software is in ALPHA stage. **NO GUARANTEES** are made about its functionality, stability, or safety.
> 
> **CRITICAL WARNINGS:**
> - This library performs **DESTRUCTIVE OPERATIONS** on files and directories
> - Permission management is **NOT FULLY TESTED** and may have security vulnerabilities
> - **DO NOT USE** in production environments
> - **DO NOT USE** on systems with critical or irreplaceable data
> - **DO NOT USE** on systems without complete, verified backups
> - The authors assume **NO RESPONSIBILITY** for data loss, system damage, or security breaches
> 
> **USE AT YOUR OWN RISK**

## Overview

Andy Tools is a comprehensive .NET library that provides a flexible, extensible framework for building and executing tools. It offers a rich set of built-in tools for file system operations, text processing, web requests, and more, while allowing developers to easily create custom tools.

## Key Features

- **Modular Architecture**: Clean separation between core interfaces, implementations, and advanced features
- **Built-in Tools**: Ready-to-use tools for common operations
- **Security & Permissions**: Fine-grained permission control and security monitoring
- **Resource Management**: Built-in resource limits and monitoring
- **Output Limiting**: Automatic truncation of large outputs
- **Advanced Features**: Tool chains, caching, and metrics collection
- **Extensibility**: Easy to create custom tools by implementing simple interfaces

## Installation

```bash
# Clone the repository
git clone https://github.com/rivoli-ai/andy-tools.git

# Build the solution
cd andy-tools
dotnet build

# Run tests
dotnet test

# Run examples
cd examples/Andy.Tools.Examples
dotnet run -- all              # Run all examples
dotnet run -- basic            # Run basic usage examples
dotnet run -- file             # Run file operations examples
dotnet run -- text             # Run text processing examples
dotnet run -- web              # Run web operations examples
dotnet run -- system           # Run system information examples
dotnet run -- cache            # Run caching examples
dotnet run -- chain            # Run tool chain examples
dotnet run -- custom           # Run custom tool examples
```

## Quick Start

### Basic Usage

```csharp
using Andy.Tools;
using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;

// Setup dependency injection
var services = new ServiceCollection();
services.AddAndyTools();

var serviceProvider = services.BuildServiceProvider();
var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();

// Execute a tool
var parameters = new Dictionary<string, object?>
{
    ["file_path"] = "/path/to/file.txt"
};

var result = await toolExecutor.ExecuteAsync(
    "read_file",
    parameters,
    new ToolExecutionContext()
);

if (result.IsSuccess)
{
    Console.WriteLine($"File content: {result.Data}");
}
```

### Using Tool Chains

```csharp
using Andy.Tools.Advanced.ToolChains;

var chainBuilder = serviceProvider.GetRequiredService<ToolChainBuilder>();

var chain = chainBuilder
    .WithId("process-files")
    .WithName("File Processing Chain")
    .AddToolStep("step1", "Read File", "read_file", new { file_path = "input.txt" })
    .AddToolStep("step2", "Process Text", "replace_text", 
        parameters: context => new 
        { 
            text = context.GetStepResult("step1")?.Data,
            search_pattern = "old",
            replacement = "new"
        })
    .AddToolStep("step3", "Write Result", "write_file",
        parameters: context => new
        {
            file_path = "output.txt",
            content = context.GetStepResult("step2")?.Data
        },
        dependencies: ["step2"])
    .Build();

var result = await chain.ExecuteAsync(new ToolExecutionContext());
```

## Examples

The `examples/Andy.Tools.Examples` project demonstrates all features of Andy Tools:

- **BasicUsageExamples**: Simple tool execution, parameter passing, error handling
- **FileOperationsExamples**: File reading, writing, copying, moving, deleting, directory listing
- **TextProcessingExamples**: JSON/XML formatting, text search and replace, regex operations
- **WebOperationsExamples**: HTTP requests, JSON processing, error handling
- **SystemInfoExamples**: System information, process details, environment variables
- **CachingExamples**: Tool result caching with TTL and cache invalidation
- **ToolChainExamples**: Sequential tool execution, data pipelines, parallel processing
- **CustomToolExamples**: Creating and using custom tools
- **SecurityExamples**: Permission management, secure tool execution

## Built-in Tools

### File System Tools
- **ReadFileTool** - Read file contents
- **WriteFileTool** - Write content to files
- **DeleteFileTool** - Delete files
- **CopyFileTool** - Copy files with progress tracking
- **MoveFileTool** - Move/rename files
- **ListDirectoryTool** - List directory contents with filtering

### Text Processing Tools
- **FormatTextTool** - Format text (JSON, XML, etc.)
- **ReplaceTextTool** - Find and replace text
- **SearchTextTool** - Search text with regex support

### Web Tools
- **HttpRequestTool** - Make HTTP requests
- **JsonProcessorTool** - Process JSON data with JSONPath

### System Tools
- **SystemInfoTool** - Get system information
- **ProcessInfoTool** - Get process information
- **DateTimeTool** - Date/time operations
- **EncodingTool** - Encode/decode text (Base64, URL, etc.)

### Git Tools
- **GitDiffTool** - Get git diff information

## Architecture

```
Andy.Tools/
├── Core/                    # Core interfaces and types
│   ├── ITool.cs
│   ├── IToolExecutor.cs
│   ├── ToolResult.cs
│   └── OutputLimiting/     # Output truncation
├── Framework/              # Framework infrastructure
│   ├── ToolFrameworkOptions.cs
│   └── IToolLifecycleManager.cs
├── Library/                # Built-in tool implementations
│   ├── FileSystem/
│   ├── Text/
│   ├── Web/
│   └── ToolBase.cs        # Base class for tools
├── Advanced/              # Advanced features
│   ├── ToolChains/        # Tool orchestration
│   ├── CachingSystem/     # Result caching
│   ├── MetricsCollection/ # Performance metrics
│   └── Configuration/     # DI configuration
├── Execution/             # Execution infrastructure
│   ├── SecurityManager.cs
│   └── ResourceMonitor.cs
├── Discovery/             # Tool discovery
├── Registry/              # Tool registration
├── Validation/            # Parameter validation
└── Observability/         # Monitoring and logging
```

## Creating Custom Tools

### Simple Tool Example

```csharp
using Andy.Tools.Library;
using Andy.Tools.Core;

public class UpperCaseTool : ToolBase
{
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "uppercase",
        Name = "Upper Case",
        Description = "Converts text to uppercase",
        Version = "1.0.0",
        Category = ToolCategory.Text,
        Parameters = new[]
        {
            new ToolParameter
            {
                Name = "text",
                Description = "Text to convert",
                Type = "string",
                Required = true
            }
        }
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, 
        ToolExecutionContext context)
    {
        var text = GetParameter<string>(parameters, "text");
        
        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult(ToolResult.Failure("Text cannot be empty"));
        }

        var result = text.ToUpper();
        return Task.FromResult(ToolResult.Success(result));
    }
}
```

### Registering Custom Tools

```csharp
services.AddTool<UpperCaseTool>();
```

## Security and Permissions

Tools can declare required permissions:

```csharp
public override ToolMetadata Metadata { get; } = new()
{
    // ... other metadata ...
    RequiredPermissions = ToolPermissionFlags.FileSystemWrite | ToolPermissionFlags.Network
};
```

Configure permissions for execution:

```csharp
var context = new ToolExecutionContext
{
    Permissions = new ToolPermissions
    {
        FileSystemAccess = true,
        NetworkAccess = false,
        ProcessExecution = false
    }
};
```

## Resource Limits

Set resource limits for tool execution:

```csharp
var context = new ToolExecutionContext
{
    ResourceLimits = new ToolResourceLimits
    {
        MaxExecutionTime = TimeSpan.FromSeconds(30),
        MaxMemoryMB = 100,
        MaxFileSizeMB = 50,
        MaxConcurrentOperations = 5
    }
};
```

## Configuration

### Basic Configuration

```csharp
services.AddAndyTools(options =>
{
    options.EnableDetailedLogging = true;
    options.RegisterBuiltInTools = true;
    options.EnableObservability = true;
    options.DefaultTimeout = TimeSpan.FromMinutes(5);
});
```

### Advanced Configuration

```csharp
services.AddAdvancedToolFeatures(options =>
{
    options.EnableCaching = true;
    options.CacheTimeToLive = TimeSpan.FromMinutes(10);
    options.EnableMetrics = true;
    options.MaxMetricsPerTool = 10000;
});
```

## Testing

The library includes comprehensive unit tests. Run tests with:

```bash
dotnet test --logger "console;verbosity=detailed"
```

Generate coverage report:

```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"coverage/**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the Apache License 2.0 - see the LICENSE file for details.

## Acknowledgments

- Built with .NET 8
- Uses Microsoft.Extensions.DependencyInjection for IoC
- Leverages System.Text.Json for JSON processing

## Support

For issues, questions, or contributions, please visit the [GitHub repository](https://github.com/rivoli-ai/andy-tools).

---

Remember: This is ALPHA software. Always backup your data and test thoroughly in a safe environment before any real use.