# Getting Started with Andy Tools

This guide will help you get up and running with Andy Tools quickly.

## Prerequisites

- .NET 8.0 SDK or later
- A code editor (Visual Studio, VS Code, or Rider recommended)
- Git for cloning the repository

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/rivoli-ai/andy-tools.git
cd andy-tools
```

### 2. Build the Solution

```bash
dotnet build
```

### 3. Run Tests (Optional)

```bash
dotnet test
```

## Your First Tool Execution

### 1. Create a New Console Application

```bash
dotnet new console -n MyAndyToolsApp
cd MyAndyToolsApp
```

### 2. Add Project Reference

Add a reference to the Andy Tools library in your `.csproj` file:

```xml
<ItemGroup>
  <ProjectReference Include="../path/to/Andy.Tools/Andy.Tools.csproj" />
</ItemGroup>
```

### 3. Basic Example

Create a simple program that reads a file:

```csharp
using Andy.Tools;
using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;

// Setup dependency injection
var services = new ServiceCollection();
services.AddAndyTools(options =>
{
    options.RegisterBuiltInTools = true;
});

var serviceProvider = services.BuildServiceProvider();

// Get the tool executor
var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();

// Prepare parameters
var parameters = new Dictionary<string, object?>
{
    ["file_path"] = "test.txt"
};

// Create execution context
var context = new ToolExecutionContext
{
    WorkingDirectory = Environment.CurrentDirectory
};

// Execute the tool
var result = await toolExecutor.ExecuteAsync("read_file", parameters, context);

// Check the result
if (result.IsSuccessful)
{
    // Extract content from the result
    if (result.Data is Dictionary<string, object?> data)
    {
        var content = data["content"]?.ToString();
        Console.WriteLine($"File content: {content}");
    }
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

## Understanding Tool Results

Tool results contain several important properties:

```csharp
public class ToolResult
{
    public bool IsSuccessful { get; set; }
    public object? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object?> Metadata { get; set; }
}
```

### Successful Results

Most tools return data as a `Dictionary<string, object?>` containing:

- **File tools**: `content`, `file_path`, `size`, etc.
- **Web tools**: `content`, `status_code`, `headers`, etc.
- **Processing tools**: `result`, `count`, `items`, etc.

### Error Handling

Always check `IsSuccessful` before accessing data:

```csharp
if (result.IsSuccessful)
{
    // Safe to access result.Data
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
    
    // Check metadata for additional error details
    if (result.Metadata.ContainsKey("error_code"))
    {
        Console.WriteLine($"Error code: {result.Metadata["error_code"]}");
    }
}
```

## Common Patterns

### Working with File Content

```csharp
// Reading files
var readParams = new Dictionary<string, object?>
{
    ["file_path"] = "input.txt"
};

var readResult = await toolExecutor.ExecuteAsync("read_file", readParams);
if (readResult.IsSuccessful && readResult.Data is Dictionary<string, object?> data)
{
    var content = data["content"]?.ToString() ?? "";
    // Process content...
}
```

### Making HTTP Requests

```csharp
// GET request
var httpParams = new Dictionary<string, object?>
{
    ["url"] = "https://api.example.com/data",
    ["method"] = "GET",
    ["headers"] = new Dictionary<string, object?>
    {
        ["User-Agent"] = "MyApp/1.0"
    }
};

var httpResult = await toolExecutor.ExecuteAsync("http_request", httpParams);
if (httpResult.IsSuccessful && httpResult.Data is Dictionary<string, object?> response)
{
    var statusCode = response["status_code"];
    var content = response["content"]?.ToString();
    // Process response...
}
```

### Processing Text

```csharp
// Format JSON
var formatParams = new Dictionary<string, object?>
{
    ["input_text"] = uglyJson,
    ["operation"] = "format_json"
};

var formatResult = await toolExecutor.ExecuteAsync("format_text", formatParams);
```

## Running the Examples

The repository includes comprehensive examples demonstrating all features:

```bash
cd examples/Andy.Tools.Examples

# Run all examples
dotnet run -- all

# Run specific examples
dotnet run -- basic    # Basic usage
dotnet run -- file     # File operations
dotnet run -- text     # Text processing
dotnet run -- web      # Web operations
dotnet run -- system   # System information
dotnet run -- cache    # Caching features
dotnet run -- chain    # Tool chains
dotnet run -- custom   # Custom tools
```

## Next Steps

Now that you have Andy Tools running:

1. Explore the [Built-in Tools Reference](tools-reference.md) to see all available tools
2. Learn about [Core Concepts](core-concepts.md) for deeper understanding
3. Try the [Examples and Tutorials](examples.md) for practical use cases
4. Build your own tools with the [Custom Tools Guide](custom-tools.md)

## Troubleshooting

### Common Issues

**Build Errors**
- Ensure you have .NET 8.0 SDK installed: `dotnet --version`
- Check all project references are correct

**Tool Not Found**
- Verify `RegisterBuiltInTools = true` in configuration
- Check tool ID matches exactly (case-sensitive)

**Permission Errors**
- Set appropriate permissions in `ToolExecutionContext`
- Check file system permissions for file operations

**Missing Data in Results**
- Tools return data in dictionaries - check field names
- Use debugger to inspect result structure

For more help, see the [Troubleshooting Guide](troubleshooting.md).