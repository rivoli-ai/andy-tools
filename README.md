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

if (result.IsSuccessful)
{
    Console.WriteLine($"File content: {result.Data}");
}
```

### Using Tool Chains

```csharp
using Andy.Tools.Advanced.ToolChains;

var chainBuilder = serviceProvider.GetRequiredService<ToolChainBuilder>();

// Build the chain, then add steps to it. AddToolStep takes (toolId, parameters, name?).
var chain = chainBuilder
    .WithId("process-files")
    .WithName("File Processing Chain")
    .Build();

chain.AddToolStep("read_file",
    new Dictionary<string, object?> { ["file_path"] = "input.txt" },
    "Read File");

chain.AddToolStep("replace_text",
    new Dictionary<string, object?>
    {
        ["search_pattern"] = "old",
        ["replacement_text"] = "new"
    },
    "Process Text");

chain.AddToolStep("write_file",
    new Dictionary<string, object?> { ["file_path"] = "output.txt" },
    "Write Result");

var result = await chain.ExecuteAsync(initialParameters: null, new ToolExecutionContext());
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

## Dataframe tools (`Andy.Tools.Data`)

The `Andy.Tools.Data` package adds 28 `dataframe_*` tools — load (CSV/JSON/Parquet/Delta), inspect (schema/profile/preview/value_counts/assert), transform (select/filter/with_column/rename/group_by/window/pivot/unpivot/unnest/join/sample/sort/distinct/union/fillna/dropna), and export — letting a model manipulate tabular data with **no SQL or code execution**. They are thin Andy `ITool` adapters over the framework-independent [`Andy.Data`](https://github.com/rivoli-ai/andy-data) engine (consumed as the published `Andy.Data` / `Andy.Data.Abstractions` NuGet packages). See [docs/tools-reference.md](docs/tools-reference.md#data--dataframe-tools) for the full per-tool reference.

```csharp
services.AddAndyTools();
services.AddAndyDataFrameTools();   // registers all dataframe_* tools
// optional path scoping: services.AddSingleton<IPathPolicy, MyPolicy>();
// optional Andy.Permissions glue: provider.UseAndyDataFramePermissions();  // Andy.Tools.Data.Permissions
```

## Built-in Tools

The tools registered by default are defined in `BuiltInToolsExtensions`.

### File System Tools
- **ReadFileTool** (`read_file`) - Read file contents
- **WriteFileTool** (`write_file`) - Write content to files
- **CopyFileTool** (`copy_file`) - Copy files (enforces `AllowedPaths` and the `Destructive` capability)
- **MoveFileTool** (`move_file`) - Move/rename files
- **DeleteFileTool** (`delete_file`) - Delete files (enforces `AllowedPaths` and the `Destructive` capability)
- **ListDirectoryTool** (`list_directory`) - List directory contents with filtering

### Text Processing Tools
- **FormatTextTool** (`format_text`) - Format text (JSON, XML, etc.)
- **ReplaceTextTool** (`replace_text`) - Find and replace text
- **SearchTextTool** (`search_text`) - Search text with regex support

### Web Tools
- **HttpRequestTool** (`http_request`) - Make HTTP requests
- **JsonProcessorTool** (`json_processor`) - Process JSON data

### System Tools
- **SystemInfoTool** (`system_info`) - Get system information
- **ProcessInfoTool** (`process_info`) - Get process information
- **ExecuteCommandTool** (`execute_command`) - Run a shell command (requires process-execution permission)

### Utility Tools
- **DateTimeTool** (`datetime_tool`) - Date/time operations
- **EncodingTool** (`encoding_tool`) - Encode/decode/hash text (Base64, URL, etc.)

### Productivity Tools
- **TodoManagementTool** (`todo_management`) - Create and manage todos via the built-in todo system
- **TodoExecutor** (`todo_executor`) - Manage a todo list

### Git Tools
- **GitDiffTool** (`git_diff`) - Get git diff information

## MCP (Model Context Protocol) Client

The optional `Andy.Tools.Mcp` package lets Andy.Tools consume tools exposed by external
[MCP](https://modelcontextprotocol.io) servers. It connects to the configured servers on
startup (via the [`Andy.MCP`](https://www.nuget.org/packages/Andy.MCP) library), discovers
their tools, and registers each one in the `IToolRegistry` so it executes through the normal
`IToolExecutor` pipeline like any built-in tool.

The core `Andy.Tools` package stays dependency-free: MCP support lives entirely in the
separate `Andy.Tools.Mcp` package.

```csharp
using Andy.MCP.Configuration;
using Andy.Tools.Mcp;

services.AddAndyTools();

services.AddMcpTools(o =>
{
    // stdio transport (launches a server process)
    o.Servers.Add(new McpServerConfig
    {
        Name = "fs",
        Transport = "stdio",
        Command = "npx",
        Arguments = "-y @modelcontextprotocol/server-filesystem /tmp",
    });

    // or HTTP transport
    o.Servers.Add(new McpServerConfig
    {
        Name = "remote",
        Transport = "http",
        Url = "https://example.com/mcp",
    });
});
```

Discovered tools are registered with the id `mcp__<server>__<tool>` (for example
`mcp__fs__read_file`). They appear in the registry alongside built-in tools and can be
executed through `IToolExecutor` using that id. A tool's MCP input schema is mapped to
`ToolParameter`s, and each MCP tool requires the `Network` permission. If a configured
server is unavailable at startup, it is logged and skipped without crashing the host.

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
        Category = ToolCategory.TextProcessing,
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
        MaxExecutionTimeMs = 30_000,
        MaxMemoryBytes = 100 * 1024 * 1024,
        MaxFileSizeBytes = 50 * 1024 * 1024,
        MaxFileCount = 5
    }
};
```

## Configuration

### Basic Configuration

```csharp
services.AddAndyTools(options =>
{
    options.EnableDetailedTracing = true;
    options.RegisterBuiltInTools = true;
    options.EnableObservability = true;
    options.DefaultResourceLimits.MaxExecutionTimeMs = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;
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