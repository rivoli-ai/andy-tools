# Troubleshooting Guide

This guide helps you diagnose and resolve common issues with Andy Tools.

## Common Issues

### Tool Not Found

**Symptom**: `ToolNotFoundException: Tool with ID 'tool_name' not found`

**Causes and Solutions**:

1. **Tool not registered**
   ```csharp
   // Check if tool is registered
   var registry = serviceProvider.GetRequiredService<IToolRegistry>();
   if (!registry.Contains("tool_name"))
   {
       // Register the tool
       services.AddTool<YourTool>();
   }
   ```

2. **Built-in tools not enabled**
   ```csharp
   services.AddAndyTools(options =>
   {
       options.RegisterBuiltInTools = true; // Enable built-in tools
   });
   ```

3. **Tool ID mismatch**
   ```csharp
   // Check exact tool ID (case-sensitive)
   var allTools = registry.GetAllTools();
   foreach (var tool in allTools)
   {
       Console.WriteLine($"Available: {tool.Metadata.Id}");
   }
   ```

### Permission Denied

**Symptom**: `SecurityException: Permission denied for operation`

**Solutions**:

1. **Grant required permissions**
   ```csharp
   var context = new ToolExecutionContext
   {
       Permissions = new ToolPermissions
       {
           FileSystemAccess = true,
           FileSystemOperations = FileSystemOperations.Read | FileSystemOperations.Write,
           NetworkAccess = true
       }
   };
   ```

2. **Check tool requirements**
   ```csharp
   var metadata = executor.GetToolMetadata("tool_id");
   Console.WriteLine($"Required permissions: {metadata.RequiredPermissions}");
   ```

3. **Allow specific paths**
   ```csharp
   context.Permissions.AllowedPaths = new[] 
   { 
       "/app/data",
       Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
   };
   ```

### File Not Found

**Symptom**: `FileNotFoundException` when working with files

**Solutions**:

1. **Use absolute paths**
   ```csharp
   var absolutePath = Path.GetFullPath(relativePath);
   var parameters = new Dictionary<string, object?>
   {
       ["file_path"] = absolutePath
   };
   ```

2. **Set working directory**
   ```csharp
   var context = new ToolExecutionContext
   {
       WorkingDirectory = "/path/to/working/directory"
   };
   ```

3. **Verify file exists**
   ```csharp
   if (!File.Exists(filePath))
   {
       Console.WriteLine($"File not found: {filePath}");
       Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
   }
   ```

### Memory Limit Exceeded

**Symptom**: `MemoryLimitExceededException` during execution

**Solutions**:

1. **Increase memory limits**
   ```csharp
   context.ResourceLimits = new ToolResourceLimits
   {
       MaxMemoryMB = 500 // Increase from default
   };
   ```

2. **Process in chunks**
   ```csharp
   // Instead of loading entire file
   var content = await File.ReadAllTextAsync(largefile);
   
   // Process line by line
   using var reader = new StreamReader(largefile);
   string line;
   while ((line = await reader.ReadLineAsync()) != null)
   {
       // Process line
   }
   ```

3. **Enable output limiting**
   ```csharp
   services.Configure<OutputLimitingOptions>(options =>
   {
       options.MaxOutputSizeBytes = 5 * 1024 * 1024; // 5MB
   });
   ```

### Timeout Errors

**Symptom**: `TaskCanceledException` or `TimeoutException`

**Solutions**:

1. **Increase timeout**
   ```csharp
   context.ResourceLimits = new ToolResourceLimits
   {
       MaxExecutionTime = TimeSpan.FromMinutes(10),
       NetworkTimeout = TimeSpan.FromSeconds(60)
   };
   ```

2. **Use cancellation tokens properly**
   ```csharp
   var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
   var context = new ToolExecutionContext
   {
       CancellationToken = cts.Token
   };
   ```

3. **Implement retry logic**
   ```csharp
   var retryPolicy = Policy
       .Handle<TaskCanceledException>()
       .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2, i)));
   
   await retryPolicy.ExecuteAsync(async () =>
   {
       return await executor.ExecuteAsync(toolId, parameters);
   });
   ```

### Network Errors

**Symptom**: `HttpRequestException` or network-related errors

**Solutions**:

1. **Check domain whitelist**
   ```csharp
   context.Permissions.AllowedDomains = new[]
   {
       "api.example.com",
       "*.trusted.com"
   };
   ```

2. **Configure proxy if needed**
   ```csharp
   services.AddHttpClient<HttpRequestTool>()
       .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
       {
           Proxy = new WebProxy("http://proxy.company.com:8080"),
           UseProxy = true
       });
   ```

3. **Handle SSL issues**
   ```csharp
   // For development only - not for production!
   services.AddHttpClient<HttpRequestTool>()
       .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
       {
           ServerCertificateCustomValidationCallback = 
               HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
       });
   ```

## Performance Issues

### Slow Execution

**Symptoms**: Tools taking longer than expected

**Diagnostics**:

1. **Enable metrics**
   ```csharp
   services.AddAdvancedToolFeatures(options =>
   {
       options.EnableMetrics = true;
   });
   
   var metrics = serviceProvider.GetRequiredService<IToolMetricsCollector>();
   var toolMetrics = await metrics.GetToolMetricsAsync("slow_tool");
   Console.WriteLine($"Average duration: {toolMetrics.AverageDuration}");
   Console.WriteLine($"P95 duration: {toolMetrics.P95Duration}");
   ```

2. **Profile execution**
   ```csharp
   var stopwatch = Stopwatch.StartNew();
   var result = await executor.ExecuteAsync(toolId, parameters);
   stopwatch.Stop();
   
   Console.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms");
   if (result.Metadata.ContainsKey("timing"))
   {
       // Tool-specific timing information
   }
   ```

3. **Enable caching**
   ```csharp
   services.AddAdvancedToolFeatures(options =>
   {
       options.EnableCaching = true;
       options.CacheTimeToLive = TimeSpan.FromMinutes(10);
   });
   ```

### High Memory Usage

**Diagnostics**:

1. **Monitor memory usage**
   ```csharp
   var initialMemory = GC.GetTotalMemory(true);
   var result = await executor.ExecuteAsync(toolId, parameters);
   var finalMemory = GC.GetTotalMemory(false);
   
   var memoryUsed = finalMemory - initialMemory;
   Console.WriteLine($"Memory used: {memoryUsed / 1024.0 / 1024.0:F2} MB");
   ```

2. **Force garbage collection**
   ```csharp
   // After processing large data
   GC.Collect();
   GC.WaitForPendingFinalizers();
   GC.Collect();
   ```

3. **Use streaming for large files**
   ```csharp
   // Instead of loading entire file
   // Use streaming tools or process in chunks
   ```

## Debugging

### Enable Detailed Logging

```csharp
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
});

services.AddAndyTools(options =>
{
    options.EnableDetailedLogging = true;
});
```

### Inspect Tool Metadata

```csharp
var executor = serviceProvider.GetRequiredService<IToolExecutor>();
var metadata = executor.GetToolMetadata("problematic_tool");

Console.WriteLine($"Tool: {metadata.Name}");
Console.WriteLine($"Description: {metadata.Description}");
Console.WriteLine($"Required permissions: {metadata.RequiredPermissions}");

foreach (var param in metadata.Parameters)
{
    Console.WriteLine($"Parameter: {param.Name}");
    Console.WriteLine($"  Type: {param.Type}");
    Console.WriteLine($"  Required: {param.Required}");
    Console.WriteLine($"  Default: {param.DefaultValue}");
}
```

### Trace Execution

```csharp
public class TracingToolExecutor : IToolExecutor
{
    private readonly IToolExecutor _inner;
    private readonly ILogger<TracingToolExecutor> _logger;
    
    public async Task<ToolResult> ExecuteAsync(
        string toolId, 
        Dictionary<string, object?> parameters,
        ToolExecutionContext? context)
    {
        _logger.LogInformation($"Executing {toolId} with parameters: " +
            JsonSerializer.Serialize(parameters));
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await _inner.ExecuteAsync(toolId, parameters, context);
            
            _logger.LogInformation($"Tool {toolId} completed in {stopwatch.ElapsedMilliseconds}ms");
            
            if (!result.IsSuccessful)
            {
                _logger.LogError($"Tool {toolId} failed: {result.ErrorMessage}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Tool {toolId} threw exception");
            throw;
        }
    }
}
```

## Error Messages Reference

### Common Error Codes

| Error Code | Description | Solution |
|------------|-------------|----------|
| `TOOL_NOT_FOUND` | Tool ID not registered | Check tool registration |
| `INVALID_PARAMETERS` | Parameter validation failed | Check parameter types and requirements |
| `FILE_NOT_FOUND` | File doesn't exist | Verify file path |
| `ACCESS_DENIED` | Permission denied | Grant required permissions |
| `PATH_TRAVERSAL` | Path contains .. or ~ | Use absolute paths |
| `TIMEOUT` | Operation timed out | Increase timeout limits |
| `MEMORY_LIMIT_EXCEEDED` | Memory limit exceeded | Increase memory limit or process in chunks |
| `NETWORK_ERROR` | Network request failed | Check network settings and permissions |
| `INVALID_URL` | URL format invalid | Verify URL format |
| `DOMAIN_NOT_ALLOWED` | Domain not in whitelist | Add domain to allowed list |

### Validation Errors

```csharp
// Handle validation errors
var result = await executor.ExecuteAsync(toolId, parameters);

if (!result.IsSuccessful && result.ErrorCode == "INVALID_PARAMETERS")
{
    // Check metadata for details
    if (result.Metadata.TryGetValue("validation_errors", out var errors))
    {
        var validationErrors = errors as Dictionary<string, string>;
        foreach (var error in validationErrors)
        {
            Console.WriteLine($"Parameter '{error.Key}': {error.Value}");
        }
    }
}
```

## Platform-Specific Issues

### Windows

1. **Path separator issues**
   ```csharp
   // Use Path.Combine for cross-platform paths
   var path = Path.Combine("folder", "subfolder", "file.txt");
   ```

2. **File locking**
   ```csharp
   // Ensure proper disposal
   using (var stream = File.OpenRead(path))
   {
       // Process file
   } // File handle released here
   ```

### Linux/macOS

1. **Case sensitivity**
   ```csharp
   // File systems are case-sensitive
   // "File.txt" != "file.txt"
   ```

2. **Permission issues**
   ```bash
   # Ensure proper file permissions
   chmod 644 file.txt
   ```

### Docker/Container

1. **File paths in containers**
   ```csharp
   // Use container-aware paths
   var dataPath = Environment.GetEnvironmentVariable("DATA_PATH") ?? "/app/data";
   ```

2. **Memory limits**
   ```dockerfile
   # Set appropriate limits
   docker run -m 512m my-app
   ```

## Getting Help

### Diagnostic Information

When reporting issues, include:

```csharp
public static void CollectDiagnostics(IServiceProvider serviceProvider)
{
    Console.WriteLine("=== Andy Tools Diagnostics ===");
    Console.WriteLine($"Version: {typeof(ITool).Assembly.GetName().Version}");
    Console.WriteLine($"Platform: {RuntimeInformation.OSDescription}");
    Console.WriteLine($".NET Version: {Environment.Version}");
    
    var registry = serviceProvider.GetRequiredService<IToolRegistry>();
    var tools = registry.GetAllTools().ToList();
    Console.WriteLine($"Registered tools: {tools.Count}");
    
    foreach (var tool in tools.Take(5))
    {
        Console.WriteLine($"  - {tool.Metadata.Id} v{tool.Metadata.Version}");
    }
    
    if (tools.Count > 5)
    {
        Console.WriteLine($"  ... and {tools.Count - 5} more");
    }
}
```

### Support Channels

1. **GitHub Issues**: Report bugs and feature requests
2. **Documentation**: Check the latest docs
3. **Examples**: Review example code
4. **Tests**: Look at test cases for usage patterns

## Prevention Tips

1. **Always validate inputs**
2. **Use appropriate timeouts**
3. **Handle errors gracefully**
4. **Set reasonable resource limits**
5. **Test with production-like data**
6. **Monitor performance metrics**
7. **Keep tools updated**
8. **Follow security best practices**

## Summary

Most issues can be resolved by:
- Checking permissions and resource limits
- Validating file paths and parameters
- Enabling detailed logging
- Using appropriate error handling
- Following platform-specific guidelines

For issues not covered here, refer to the [GitHub repository](https://github.com/rivoli-ai/andy-tools) for support.