# Advanced Features

Andy Tools provides several advanced features for complex scenarios and enhanced functionality.

## Tool Chains

Tool chains allow you to orchestrate multiple tools in sequence or parallel, passing data between them.

### Basic Tool Chain

```csharp
using Andy.Tools.Advanced.ToolChains;

var chainBuilder = serviceProvider.GetRequiredService<ToolChainBuilder>();

var chain = chainBuilder
    .WithId("data-processing")
    .WithName("Data Processing Pipeline")
    .AddToolStep("step1", "Read Data", "read_file", 
        new { file_path = "input.json" })
    .AddToolStep("step2", "Process JSON", "json_processor",
        parameters: context => new 
        { 
            json = context.GetStepResult("step1")?.Data?.ToString(),
            query = "$.items[?(@.active == true)]"
        })
    .AddToolStep("step3", "Save Results", "write_file",
        parameters: context => new
        {
            file_path = "output.json",
            content = context.GetStepResult("step2")?.Data?.ToString()
        })
    .Build();

var result = await chain.ExecuteAsync(new ToolExecutionContext());
```

### Parallel Execution

Execute multiple tools in parallel:

```csharp
var chain = chainBuilder
    .WithId("parallel-fetch")
    .WithName("Parallel Data Fetching")
    
    // These steps run in parallel
    .AddToolStep("fetch1", "Fetch API 1", "http_request",
        new { url = "https://api1.example.com/data" })
    .AddToolStep("fetch2", "Fetch API 2", "http_request",
        new { url = "https://api2.example.com/data" })
    .AddToolStep("fetch3", "Fetch API 3", "http_request",
        new { url = "https://api3.example.com/data" })
    
    // This step waits for all parallel steps
    .AddToolStep("combine", "Combine Results", "custom_combiner",
        parameters: context => new
        {
            data1 = context.GetStepResult("fetch1")?.Data,
            data2 = context.GetStepResult("fetch2")?.Data,
            data3 = context.GetStepResult("fetch3")?.Data
        },
        dependencies: new[] { "fetch1", "fetch2", "fetch3" })
    .Build();
```

### Conditional Execution

Add conditions to tool chain steps:

```csharp
var chain = chainBuilder
    .WithId("conditional-processing")
    .WithName("Conditional Processing")
    .AddToolStep("check", "Check File Type", "file_info",
        new { file_path = "input.file" })
    .AddConditionalStep("process_json", "Process JSON", "json_processor",
        condition: context =>
        {
            var fileInfo = context.GetStepResult("check")?.Data as Dictionary<string, object?>;
            return fileInfo?["extension"]?.ToString() == ".json";
        },
        parameters: context => new { file = "input.file" })
    .AddConditionalStep("process_xml", "Process XML", "xml_processor",
        condition: context =>
        {
            var fileInfo = context.GetStepResult("check")?.Data as Dictionary<string, object?>;
            return fileInfo?["extension"]?.ToString() == ".xml";
        },
        parameters: context => new { file = "input.file" })
    .Build();
```

### Error Handling in Chains

Handle errors gracefully in tool chains:

```csharp
var chain = chainBuilder
    .WithId("error-handling")
    .WithName("Chain with Error Handling")
    .WithErrorHandler(async (context, step, error) =>
    {
        // Log the error
        logger.LogError(error, $"Step {step.Name} failed");
        
        // Decide whether to continue
        if (step.Id == "optional_step")
        {
            // Continue execution for optional steps
            return ErrorHandlingResult.Continue;
        }
        
        // Stop execution for critical steps
        return ErrorHandlingResult.Stop;
    })
    .AddToolStep("critical", "Critical Step", "important_tool", new { })
    .AddToolStep("optional_step", "Optional Step", "nice_to_have", new { })
    .Build();
```

## Caching System

The caching system improves performance by storing tool results.

### Basic Caching

Enable caching for your tools:

```csharp
services.AddAdvancedToolFeatures(options =>
{
    options.EnableCaching = true;
    options.CacheTimeToLive = TimeSpan.FromMinutes(10);
});
```

### Using the Cache

The cache works automatically, but you can also interact with it directly:

```csharp
var cache = serviceProvider.GetRequiredService<IToolExecutionCache>();

// Check if a result is cached
var cacheKey = "tool_id:param_hash";
var cachedResult = await cache.GetAsync(cacheKey);

if (cachedResult != null)
{
    // Use cached result
    Console.WriteLine("Cache hit!");
}
else
{
    // Execute tool and cache result
    var result = await executor.ExecuteAsync("tool_id", parameters);
    
    if (result.IsSuccessful)
    {
        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
    }
}
```

### Custom Cache Key Generation

Implement custom cache key generation:

```csharp
public class CustomCacheKeyGenerator : ICacheKeyGenerator
{
    public string GenerateKey(string toolId, Dictionary<string, object?> parameters)
    {
        var sortedParams = parameters
            .OrderBy(p => p.Key)
            .Select(p => $"{p.Key}:{p.Value}")
            .ToArray();
        
        var paramString = string.Join("|", sortedParams);
        
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(paramString));
        var hashString = Convert.ToBase64String(hash);
        
        return $"{toolId}:{hashString}";
    }
}

// Register custom generator
services.AddSingleton<ICacheKeyGenerator, CustomCacheKeyGenerator>();
```

### Cache Invalidation

Invalidate cache entries when needed:

```csharp
public class CacheInvalidationService
{
    private readonly IToolExecutionCache _cache;
    
    public async Task InvalidateToolCache(string toolId)
    {
        // Invalidate all cache entries for a tool
        await _cache.InvalidateByPrefixAsync($"{toolId}:");
    }
    
    public async Task InvalidateSpecificEntry(string toolId, Dictionary<string, object?> parameters)
    {
        var key = GenerateCacheKey(toolId, parameters);
        await _cache.RemoveAsync(key);
    }
    
    public async Task InvalidateAll()
    {
        await _cache.ClearAsync();
    }
}
```

### Distributed Caching

Use Redis for distributed caching:

```csharp
public class RedisToolCache : IToolExecutionCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    
    public RedisToolCache(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = _redis.GetDatabase();
    }
    
    public async Task<ToolResult?> GetAsync(string cacheKey)
    {
        var value = await _db.StringGetAsync(cacheKey);
        if (value.IsNullOrEmpty)
            return null;
        
        return JsonSerializer.Deserialize<ToolResult>(value);
    }
    
    public async Task SetAsync(string cacheKey, ToolResult result, TimeSpan? ttl)
    {
        var json = JsonSerializer.Serialize(result);
        await _db.StringSetAsync(cacheKey, json, ttl);
    }
}

// Register Redis cache
services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));
services.AddSingleton<IToolExecutionCache, RedisToolCache>();
```

## Metrics Collection

Track performance and usage metrics for your tools.

### Built-in Metrics

Andy Tools automatically collects:
- Execution count per tool
- Success/failure rates
- Execution duration
- Resource usage

### Accessing Metrics

```csharp
var metricsCollector = serviceProvider.GetRequiredService<IToolMetricsCollector>();

// Get metrics for a specific tool
var metrics = await metricsCollector.GetToolMetricsAsync("read_file");

Console.WriteLine($"Total executions: {metrics.TotalExecutions}");
Console.WriteLine($"Success rate: {metrics.SuccessRate:P}");
Console.WriteLine($"Average duration: {metrics.AverageDuration.TotalMilliseconds}ms");
Console.WriteLine($"P95 duration: {metrics.P95Duration.TotalMilliseconds}ms");
```

### Custom Metrics

Add custom metrics to your tools:

```csharp
public class MetricAwareTool : ToolBase
{
    private readonly IToolMetricsCollector _metrics;
    
    public MetricAwareTool(IToolMetricsCollector metrics)
    {
        _metrics = metrics;
    }
    
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        // Record custom metric
        _metrics.RecordCustomMetric(Metadata.Id, "input_size", 
            parameters["data"]?.ToString()?.Length ?? 0);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Tool logic here
            var result = await ProcessDataAsync(parameters);
            
            // Record processing time
            _metrics.RecordCustomMetric(Metadata.Id, "processing_time_ms", 
                stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            // Record error type
            _metrics.RecordCustomMetric(Metadata.Id, $"error_{ex.GetType().Name}", 1);
            throw;
        }
    }
}
```

### Metrics Export

Export metrics to monitoring systems:

```csharp
public class PrometheusMetricsExporter : IMetricsExporter
{
    public async Task ExportAsync(ToolMetrics metrics)
    {
        // Format metrics for Prometheus
        var prometheusFormat = new StringBuilder();
        
        prometheusFormat.AppendLine($"# HELP tool_executions_total Total tool executions");
        prometheusFormat.AppendLine($"# TYPE tool_executions_total counter");
        prometheusFormat.AppendLine($"tool_executions_total{{tool=\"{metrics.ToolId}\"}} {metrics.TotalExecutions}");
        
        prometheusFormat.AppendLine($"# HELP tool_success_rate Tool success rate");
        prometheusFormat.AppendLine($"# TYPE tool_success_rate gauge");
        prometheusFormat.AppendLine($"tool_success_rate{{tool=\"{metrics.ToolId}\"}} {metrics.SuccessRate}");
        
        // Export to Prometheus endpoint
        await PushToPrometheus(prometheusFormat.ToString());
    }
}
```

## Resource Monitoring

Monitor resource usage during tool execution.

### Memory Monitoring

```csharp
public class MemoryMonitor : IResourceMonitor
{
    public async Task<ResourceUsage> MonitorAsync(Func<Task> operation)
    {
        var startMemory = GC.GetTotalMemory(true);
        var startTime = DateTime.UtcNow;
        
        await operation();
        
        var endMemory = GC.GetTotalMemory(false);
        var endTime = DateTime.UtcNow;
        
        return new ResourceUsage
        {
            MemoryUsedBytes = endMemory - startMemory,
            Duration = endTime - startTime,
            PeakMemoryBytes = GC.GetTotalMemory(false)
        };
    }
}
```

### CPU Monitoring

```csharp
public class CpuMonitor : IResourceMonitor
{
    public async Task<ResourceUsage> MonitorAsync(Func<Task> operation)
    {
        var startCpu = Process.GetCurrentProcess().TotalProcessorTime;
        var startTime = Stopwatch.StartNew();
        
        await operation();
        
        var endCpu = Process.GetCurrentProcess().TotalProcessorTime;
        startTime.Stop();
        
        var cpuUsed = endCpu - startCpu;
        var cpuPercent = cpuUsed.TotalMilliseconds / startTime.ElapsedMilliseconds * 100;
        
        return new ResourceUsage
        {
            CpuPercent = cpuPercent,
            CpuTime = cpuUsed,
            Duration = startTime.Elapsed
        };
    }
}
```

## Tool Discovery

Automatically discover and load tools from assemblies.

### Assembly Scanning

```csharp
services.AddToolDiscovery(options =>
{
    // Scan specific assemblies
    options.AssembliesToScan = new[]
    {
        typeof(Program).Assembly,
        typeof(CustomTool).Assembly
    };
    
    // Or scan by pattern
    options.AssemblyPattern = "MyCompany.*.Tools.dll";
    
    // Filter tools
    options.ToolFilter = type => 
        type.Namespace?.StartsWith("MyCompany.Tools") ?? false;
});
```

### Plugin System

Load tools from external plugins:

```csharp
public class PluginLoader : IPluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    
    public async Task<IEnumerable<ITool>> LoadPluginsAsync(string pluginDirectory)
    {
        var tools = new List<ITool>();
        var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll");
        
        foreach (var pluginFile in pluginFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(pluginFile);
                var toolTypes = assembly.GetTypes()
                    .Where(t => typeof(ITool).IsAssignableFrom(t) && !t.IsAbstract);
                
                foreach (var toolType in toolTypes)
                {
                    var tool = Activator.CreateInstance(toolType) as ITool;
                    if (tool != null)
                    {
                        tools.Add(tool);
                        _logger.LogInformation($"Loaded tool {tool.Metadata.Id} from {pluginFile}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load plugin from {pluginFile}");
            }
        }
        
        return tools;
    }
}
```

### Hot Reload

Support dynamic tool loading without restart:

```csharp
public class HotReloadService : IHostedService
{
    private readonly IToolRegistry _registry;
    private readonly IPluginLoader _pluginLoader;
    private FileSystemWatcher _watcher;
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _watcher = new FileSystemWatcher
        {
            Path = "/app/plugins",
            Filter = "*.dll",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        
        _watcher.Changed += async (sender, e) => await ReloadPlugin(e.FullPath);
        _watcher.Created += async (sender, e) => await LoadPlugin(e.FullPath);
        _watcher.Deleted += async (sender, e) => await UnloadPlugin(e.FullPath);
        
        _watcher.EnableRaisingEvents = true;
        return Task.CompletedTask;
    }
    
    private async Task ReloadPlugin(string pluginPath)
    {
        // Unload old version
        await UnloadPlugin(pluginPath);
        
        // Load new version
        await LoadPlugin(pluginPath);
    }
}
```

## Output Limiting

Control and limit tool output sizes.

### Automatic Truncation

```csharp
services.Configure<OutputLimitingOptions>(options =>
{
    options.MaxOutputSizeBytes = 10 * 1024 * 1024; // 10MB
    options.TruncationMessage = "\n[Output truncated - exceeded size limit]";
    options.PreserveSuffix = true; // Keep end of output
    options.SuffixSizeBytes = 1024; // Keep last 1KB
});
```

### Custom Output Limiter

```csharp
public class JsonAwareOutputLimiter : IOutputLimiter
{
    public string LimitOutput(string output, long maxSizeBytes)
    {
        if (Encoding.UTF8.GetByteCount(output) <= maxSizeBytes)
            return output;
        
        // Try to parse as JSON
        try
        {
            var json = JsonDocument.Parse(output);
            
            // Truncate arrays intelligently
            if (json.RootElement.ValueKind == JsonValueKind.Array)
            {
                var truncated = TruncateJsonArray(json.RootElement, maxSizeBytes);
                return truncated;
            }
        }
        catch
        {
            // Not JSON, use default truncation
        }
        
        return DefaultTruncate(output, maxSizeBytes);
    }
}
```

## Tool Composition

Build complex tools by composing simpler ones.

### Composite Tool Pattern

```csharp
public class BackupAndCompressTool : CompositeToolBase
{
    public BackupAndCompressTool(IToolExecutor executor) : base(executor)
    {
    }
    
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "backup_compress",
        Name = "Backup and Compress",
        Description = "Creates a compressed backup of files"
    };
    
    protected override async Task<ToolResult> ExecuteCompositeAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var sourcePath = GetParameter<string>(parameters, "source");
        var backupPath = GetParameter<string>(parameters, "backup_path");
        
        // Step 1: List files
        var listResult = await ExecuteToolAsync("list_directory",
            new { directory_path = sourcePath, recursive = true },
            context);
        
        if (!listResult.IsSuccessful)
            return listResult;
        
        // Step 2: Create archive
        var archiveResult = await ExecuteToolAsync("create_archive",
            new 
            { 
                files = ExtractFilePaths(listResult.Data),
                output_path = backupPath + ".zip"
            },
            context);
        
        // Step 3: Verify archive
        var verifyResult = await ExecuteToolAsync("verify_archive",
            new { archive_path = backupPath + ".zip" },
            context);
        
        return CombineResults(listResult, archiveResult, verifyResult);
    }
}
```

## Advanced Configuration

### Dynamic Configuration

Configure tools based on runtime conditions:

```csharp
public class DynamicToolConfiguration : IToolConfigurator
{
    private readonly IConfiguration _configuration;
    
    public void Configure(ITool tool, IServiceProvider services)
    {
        if (tool is IConfigurableTool configurable)
        {
            var section = _configuration.GetSection($"Tools:{tool.Metadata.Id}");
            configurable.Configure(section);
        }
        
        // Apply environment-specific settings
        var environment = services.GetRequiredService<IHostEnvironment>();
        if (environment.IsDevelopment())
        {
            ApplyDevelopmentSettings(tool);
        }
    }
}
```

### Feature Flags

Enable/disable tools with feature flags:

```csharp
public class FeatureFlaggedToolRegistry : IToolRegistry
{
    private readonly IToolRegistry _innerRegistry;
    private readonly IFeatureManager _featureManager;
    
    public bool TryGetTool(string id, out ITool? tool)
    {
        if (!_innerRegistry.TryGetTool(id, out tool))
            return false;
        
        // Check if tool is enabled
        var featureFlag = $"Tools.{id}";
        if (!_featureManager.IsEnabledAsync(featureFlag).Result)
        {
            tool = null;
            return false;
        }
        
        return true;
    }
}
```

## Performance Optimization

### Batch Processing

Process multiple items efficiently:

```csharp
public class BatchProcessingTool : ToolBase
{
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var items = GetParameter<List<string>>(parameters, "items");
        var batchSize = GetParameter<int>(parameters, "batch_size", 10);
        
        var results = new ConcurrentBag<ProcessingResult>();
        var semaphore = new SemaphoreSlim(batchSize);
        
        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                var result = await ProcessItemAsync(item);
                results.Add(result);
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        await Task.WhenAll(tasks);
        
        return ToolResult.Success(new
        {
            processed_count = results.Count,
            results = results.ToList()
        });
    }
}
```

### Streaming Results

Stream large results instead of loading into memory:

```csharp
public class StreamingTool : ToolBase
{
    public override bool SupportsStreaming => true;
    
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var filePath = GetParameter<string>(parameters, "file_path");
        
        // Return a streaming result
        return ToolResult.Stream(async (stream, cancellation) =>
        {
            using var fileStream = File.OpenRead(filePath);
            await fileStream.CopyToAsync(stream, cancellation);
        });
    }
}
```

## Summary

Advanced features in Andy Tools enable:

1. **Tool Chains**: Orchestrate complex workflows
2. **Caching**: Improve performance with intelligent caching
3. **Metrics**: Track and monitor tool performance
4. **Resource Monitoring**: Control resource usage
5. **Plugin System**: Extend with external tools
6. **Output Control**: Manage large outputs effectively
7. **Composition**: Build complex tools from simple ones

These features make Andy Tools suitable for enterprise-scale applications while maintaining simplicity for basic use cases.