# Creating Custom Tools

This guide walks you through creating custom tools for Andy Tools, from simple implementations to advanced features.

## Overview

Creating a custom tool involves:
1. Implementing the `ITool` interface or extending `ToolBase`
2. Defining tool metadata and parameters
3. Implementing the execution logic
4. Registering the tool with the framework

## Basic Custom Tool

### Step 1: Create the Tool Class

```csharp
using Andy.Tools.Core;
using Andy.Tools.Library;

public class UpperCaseTool : ToolBase
{
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "uppercase",
        Name = "Upper Case Text",
        Description = "Converts text to uppercase",
        Version = "1.0.0",
        Category = ToolCategory.Text,
        Parameters = new[]
        {
            new ToolParameter
            {
                Name = "text",
                Description = "Text to convert to uppercase",
                Type = "string",
                Required = true
            },
            new ToolParameter
            {
                Name = "culture",
                Description = "Culture to use for conversion",
                Type = "string",
                Required = false,
                DefaultValue = "en-US"
            }
        }
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        // Extract parameters using base class helper
        var text = GetParameter<string>(parameters, "text");
        var culture = GetParameter<string>(parameters, "culture", "en-US");
        
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(ToolResult.Failure(
                "Input text cannot be empty",
                "EMPTY_INPUT"
            ));
        }

        try
        {
            var cultureInfo = new CultureInfo(culture);
            var result = text.ToUpper(cultureInfo);
            
            var data = new Dictionary<string, object?>
            {
                ["result"] = result,
                ["original_length"] = text.Length,
                ["culture_used"] = culture
            };
            
            return Task.FromResult(ToolResult.Success(data));
        }
        catch (CultureNotFoundException)
        {
            return Task.FromResult(ToolResult.Failure(
                $"Invalid culture: {culture}",
                "INVALID_CULTURE"
            ));
        }
    }
}
```

### Step 2: Register the Tool

```csharp
// In your service configuration
services.AddSingleton<ITool, UpperCaseTool>();

// Or use the extension method
services.AddTool<UpperCaseTool>();
```

### Step 3: Use the Tool

```csharp
var executor = serviceProvider.GetRequiredService<IToolExecutor>();

var parameters = new Dictionary<string, object?>
{
    ["text"] = "hello world",
    ["culture"] = "tr-TR"  // Turkish culture for special i handling
};

var result = await executor.ExecuteAsync("uppercase", parameters);

if (result.IsSuccessful && result.Data is Dictionary<string, object?> data)
{
    Console.WriteLine($"Result: {data["result"]}");  // "HELLO WORLD"
}
```

## Advanced Custom Tool

### Tool with Progress Reporting

```csharp
public class FileProcessorTool : ToolBase
{
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "process_files",
        Name = "File Processor",
        Description = "Processes multiple files with progress reporting",
        Version = "1.0.0",
        Category = ToolCategory.FileSystem,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
        Parameters = new[]
        {
            new ToolParameter
            {
                Name = "directory",
                Description = "Directory containing files to process",
                Type = "string",
                Required = true
            },
            new ToolParameter
            {
                Name = "pattern",
                Description = "File pattern to match",
                Type = "string",
                Required = false,
                DefaultValue = "*.*"
            }
        }
    };

    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var directory = GetParameter<string>(parameters, "directory");
        var pattern = GetParameter<string>(parameters, "pattern", "*.*");
        
        // Validate directory exists
        if (!Directory.Exists(directory))
        {
            return ToolResult.Failure("Directory not found", "DIR_NOT_FOUND");
        }
        
        var files = Directory.GetFiles(directory, pattern);
        var processedFiles = new List<Dictionary<string, object?>>();
        
        for (int i = 0; i < files.Length; i++)
        {
            // Check for cancellation
            context.CancellationToken.ThrowIfCancellationRequested();
            
            // Report progress
            var progress = (i + 1) * 100 / files.Length;
            context.Progress?.Report(new ToolProgress
            {
                PercentComplete = progress,
                Message = $"Processing {Path.GetFileName(files[i])}"
            });
            
            // Process file (simulated)
            var fileInfo = new FileInfo(files[i]);
            processedFiles.Add(new Dictionary<string, object?>
            {
                ["path"] = files[i],
                ["name"] = fileInfo.Name,
                ["size"] = fileInfo.Length,
                ["processed_at"] = DateTime.UtcNow
            });
            
            // Simulate processing time
            await Task.Delay(100);
        }
        
        return ToolResult.Success(new Dictionary<string, object?>
        {
            ["processed_count"] = processedFiles.Count,
            ["files"] = processedFiles
        });
    }
}
```

### Tool with Resource Limits

```csharp
public class DataGeneratorTool : ToolBase
{
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "generate_data",
        Name = "Data Generator",
        Description = "Generates test data with resource constraints",
        Version = "1.0.0",
        Category = ToolCategory.Utility,
        Parameters = new[]
        {
            new ToolParameter
            {
                Name = "count",
                Description = "Number of records to generate",
                Type = "integer",
                Required = true,
                MinValue = 1,
                MaxValue = 1000000
            },
            new ToolParameter
            {
                Name = "format",
                Description = "Output format",
                Type = "string",
                Required = false,
                DefaultValue = "json",
                AllowedValues = new[] { "json", "csv", "xml" }
            }
        }
    };

    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var count = GetParameter<int>(parameters, "count");
        var format = GetParameter<string>(parameters, "format", "json");
        
        // Check resource limits
        var estimatedMemoryMB = count * 0.001; // Rough estimate
        if (context.ResourceLimits?.MaxMemoryMB != null &&
            estimatedMemoryMB > context.ResourceLimits.MaxMemoryMB)
        {
            return ToolResult.Failure(
                $"Estimated memory usage ({estimatedMemoryMB}MB) exceeds limit",
                "MEMORY_LIMIT_EXCEEDED"
            );
        }
        
        var data = new List<object>();
        var startTime = DateTime.UtcNow;
        
        for (int i = 0; i < count; i++)
        {
            // Check execution time limit
            if (context.ResourceLimits?.MaxExecutionTime != null)
            {
                var elapsed = DateTime.UtcNow - startTime;
                if (elapsed > context.ResourceLimits.MaxExecutionTime)
                {
                    return ToolResult.Failure(
                        "Execution time limit exceeded",
                        "TIMEOUT"
                    );
                }
            }
            
            // Generate record
            data.Add(new
            {
                id = i + 1,
                name = $"Record {i + 1}",
                timestamp = DateTime.UtcNow,
                value = Random.Shared.NextDouble() * 100
            });
            
            // Yield periodically to prevent blocking
            if (i % 1000 == 0)
            {
                await Task.Yield();
            }
        }
        
        // Format output
        string output = format switch
        {
            "json" => JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            }),
            "csv" => GenerateCsv(data),
            "xml" => GenerateXml(data),
            _ => throw new NotSupportedException($"Format {format} not supported")
        };
        
        return ToolResult.Success(new Dictionary<string, object?>
        {
            ["data"] = output,
            ["count"] = count,
            ["format"] = format,
            ["size_bytes"] = Encoding.UTF8.GetByteCount(output)
        });
    }
    
    private string GenerateCsv(List<object> data)
    {
        // CSV generation logic
        var sb = new StringBuilder();
        sb.AppendLine("id,name,timestamp,value");
        // ... implementation
        return sb.ToString();
    }
    
    private string GenerateXml(List<object> data)
    {
        // XML generation logic
        // ... implementation
        return "<data>...</data>";
    }
}
```

## Tool with External Dependencies

### Using HTTP Client

```csharp
public class WeatherTool : ToolBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeatherTool> _logger;
    
    public WeatherTool(HttpClient httpClient, ILogger<WeatherTool> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "get_weather",
        Name = "Weather Information",
        Description = "Gets current weather for a location",
        Version = "1.0.0",
        Category = ToolCategory.Web,
        RequiredPermissions = ToolPermissionFlags.Network,
        Parameters = new[]
        {
            new ToolParameter
            {
                Name = "location",
                Description = "City name or coordinates",
                Type = "string",
                Required = true
            },
            new ToolParameter
            {
                Name = "units",
                Description = "Temperature units",
                Type = "string",
                Required = false,
                DefaultValue = "celsius",
                AllowedValues = new[] { "celsius", "fahrenheit" }
            }
        }
    };
    
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var location = GetParameter<string>(parameters, "location");
        var units = GetParameter<string>(parameters, "units", "celsius");
        
        try
        {
            _logger.LogInformation($"Getting weather for {location}");
            
            // Make API call (example URL, replace with real API)
            var url = $"https://api.weather.com/v1/location/{Uri.EscapeDataString(location)}?units={units}";
            
            using var response = await _httpClient.GetAsync(url, context.CancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return ToolResult.Failure(
                    $"Weather API returned {response.StatusCode}",
                    "API_ERROR"
                );
            }
            
            var json = await response.Content.ReadAsStringAsync();
            var weatherData = JsonSerializer.Deserialize<JsonElement>(json);
            
            return ToolResult.Success(new Dictionary<string, object?>
            {
                ["location"] = location,
                ["temperature"] = weatherData.GetProperty("temp").GetDouble(),
                ["description"] = weatherData.GetProperty("description").GetString(),
                ["units"] = units,
                ["timestamp"] = DateTime.UtcNow
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed");
            return ToolResult.Failure("Network error occurred", "NETWORK_ERROR");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Failure("Request timeout", "TIMEOUT");
        }
    }
}
```

### Registering with Dependencies

```csharp
// Configure HTTP client
services.AddHttpClient<WeatherTool>(client =>
{
    client.BaseAddress = new Uri("https://api.weather.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Andy-Tools/1.0");
});

// Register the tool
services.AddTool<WeatherTool>();
```

## Tool Base Class Features

The `ToolBase` class provides many helpful features:

### Parameter Extraction

```csharp
// Basic extraction with type safety
var text = GetParameter<string>(parameters, "text");
var count = GetParameter<int>(parameters, "count");
var enabled = GetParameter<bool>(parameters, "enabled");

// With default values
var format = GetParameter<string>(parameters, "format", "json");
var timeout = GetParameter<int>(parameters, "timeout", 30);

// Nullable parameters
var optionalValue = GetParameterOrDefault<int?>(parameters, "optional");
```

### Validation Helpers

```csharp
protected override async Task<ToolResult> ExecuteInternalAsync(
    Dictionary<string, object?> parameters,
    ToolExecutionContext context)
{
    // Validate required permissions
    if (!HasPermission(context, ToolPermissionFlags.FileSystemWrite))
    {
        return ToolResult.Failure("File write permission required", "NO_PERMISSION");
    }
    
    // Validate file paths
    var filePath = GetParameter<string>(parameters, "file_path");
    if (!IsValidPath(filePath))
    {
        return ToolResult.Failure("Invalid file path", "INVALID_PATH");
    }
    
    // Validate within allowed paths
    if (!IsPathAllowed(filePath, context))
    {
        return ToolResult.Failure("Path not allowed", "PATH_DENIED");
    }
    
    // Continue with execution...
}
```

### Error Handling

```csharp
protected override async Task<ToolResult> ExecuteInternalAsync(
    Dictionary<string, object?> parameters,
    ToolExecutionContext context)
{
    try
    {
        // Tool logic
        return ToolResult.Success(data);
    }
    catch (FileNotFoundException ex)
    {
        return CreateErrorResult("File not found", "FILE_NOT_FOUND", ex);
    }
    catch (UnauthorizedAccessException ex)
    {
        return CreateErrorResult("Access denied", "ACCESS_DENIED", ex);
    }
    catch (Exception ex)
    {
        // Log unexpected errors
        LogError(ex, "Unexpected error in tool execution");
        return CreateErrorResult("An unexpected error occurred", "INTERNAL_ERROR");
    }
}
```

## Testing Custom Tools

### Unit Testing

```csharp
[TestClass]
public class UpperCaseToolTests
{
    private UpperCaseTool _tool;
    
    [TestInitialize]
    public void Setup()
    {
        _tool = new UpperCaseTool();
    }
    
    [TestMethod]
    public async Task Execute_ValidInput_ReturnsUpperCase()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["text"] = "hello world"
        };
        var context = new ToolExecutionContext();
        
        // Act
        var result = await _tool.ExecuteAsync(parameters, context);
        
        // Assert
        Assert.IsTrue(result.IsSuccessful);
        Assert.IsInstanceOfType(result.Data, typeof(Dictionary<string, object?>));
        
        var data = (Dictionary<string, object?>)result.Data;
        Assert.AreEqual("HELLO WORLD", data["result"]);
    }
    
    [TestMethod]
    public async Task Execute_EmptyInput_ReturnsError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["text"] = ""
        };
        var context = new ToolExecutionContext();
        
        // Act
        var result = await _tool.ExecuteAsync(parameters, context);
        
        // Assert
        Assert.IsFalse(result.IsSuccessful);
        Assert.AreEqual("EMPTY_INPUT", result.ErrorCode);
    }
}
```

### Integration Testing

```csharp
[TestClass]
public class ToolIntegrationTests
{
    private ServiceProvider _serviceProvider;
    private IToolExecutor _executor;
    
    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddAndyTools();
        services.AddTool<UpperCaseTool>();
        
        _serviceProvider = services.BuildServiceProvider();
        _executor = _serviceProvider.GetRequiredService<IToolExecutor>();
    }
    
    [TestMethod]
    public async Task UpperCaseTool_RegisteredAndExecutable()
    {
        // Act
        var result = await _executor.ExecuteAsync("uppercase", 
            new Dictionary<string, object?> { ["text"] = "test" });
        
        // Assert
        Assert.IsTrue(result.IsSuccessful);
    }
}
```

## Best Practices

### 1. Parameter Design

- Use clear, descriptive parameter names
- Provide sensible defaults where appropriate
- Use parameter validation (min/max, patterns, allowed values)
- Document all parameters thoroughly

### 2. Error Handling

- Return specific error codes for different failure scenarios
- Include helpful error messages for users
- Log errors for debugging
- Clean up resources on failure

### 3. Performance

- Use async/await for I/O operations
- Report progress for long-running operations
- Support cancellation tokens
- Avoid blocking the thread pool

### 4. Security

- Declare all required permissions
- Validate all inputs thoroughly
- Sanitize file paths and user input
- Never expose sensitive information in results

### 5. Output Format

- Return structured data (usually Dictionary<string, object?>)
- Use consistent field names
- Include metadata about the operation
- Keep output size reasonable

## Advanced Patterns

### Composite Tools

Create tools that orchestrate other tools:

```csharp
public class BackupTool : ToolBase
{
    private readonly IToolExecutor _executor;
    
    public BackupTool(IToolExecutor executor)
    {
        _executor = executor;
    }
    
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var sourcePath = GetParameter<string>(parameters, "source");
        var backupPath = GetParameter<string>(parameters, "backup_path");
        
        // List files to backup
        var listResult = await _executor.ExecuteAsync("list_directory",
            new Dictionary<string, object?>
            {
                ["directory_path"] = sourcePath,
                ["recursive"] = true
            }, context);
        
        if (!listResult.IsSuccessful)
            return ToolResult.Failure("Failed to list source files");
        
        // Process each file...
        // Use copy_file tool for each file
        
        return ToolResult.Success(backupInfo);
    }
}
```

### Configurable Tools

Create tools that can be configured at registration:

```csharp
public class ConfigurableValidatorTool : ToolBase
{
    private readonly ValidationOptions _options;
    
    public ConfigurableValidatorTool(IOptions<ValidationOptions> options)
    {
        _options = options.Value;
    }
    
    // Use configuration in execution
}

// Registration
services.Configure<ValidationOptions>(config =>
{
    config.MaxLength = 1000;
    config.AllowedPatterns = new[] { "^[A-Z]", "^[0-9]" };
});
services.AddTool<ConfigurableValidatorTool>();
```

## Summary

Creating custom tools for Andy Tools is straightforward:

1. Extend `ToolBase` or implement `ITool`
2. Define clear metadata and parameters
3. Implement robust execution logic
4. Handle errors gracefully
5. Test thoroughly
6. Follow security best practices

With these patterns, you can create powerful, reusable tools that integrate seamlessly with the Andy Tools ecosystem.