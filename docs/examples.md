# Examples and Tutorials

This guide provides practical examples of using Andy Tools in various scenarios.

## Basic Examples

### Reading and Writing Files

```csharp
using Andy.Tools;
using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;

// Setup
var services = new ServiceCollection();
services.AddAndyTools();
var serviceProvider = services.BuildServiceProvider();
var executor = serviceProvider.GetRequiredService<IToolExecutor>();

// Read a file
var readParams = new Dictionary<string, object?>
{
    ["file_path"] = "input.txt"
};

var readResult = await executor.ExecuteAsync("read_file", readParams);

if (readResult.IsSuccessful && readResult.Data is Dictionary<string, object?> data)
{
    var content = data["content"]?.ToString();
    Console.WriteLine($"File content: {content}");
    
    // Modify content
    var modifiedContent = content?.ToUpper();
    
    // Write to new file
    var writeParams = new Dictionary<string, object?>
    {
        ["file_path"] = "output.txt",
        ["content"] = modifiedContent
    };
    
    var writeResult = await executor.ExecuteAsync("write_file", writeParams);
    
    if (writeResult.IsSuccessful)
    {
        Console.WriteLine("File written successfully");
    }
}
```

### Processing Text

```csharp
// Format ugly JSON
var uglyJson = "{\"name\":\"John\",\"age\":30,\"city\":\"New York\"}";

var formatParams = new Dictionary<string, object?>
{
    ["input_text"] = uglyJson,
    ["operation"] = "format_json"
};

var formatResult = await executor.ExecuteAsync("format_text", formatParams);

if (formatResult.IsSuccessful && formatResult.Data is Dictionary<string, object?> data)
{
    var prettyJson = data["formatted_text"]?.ToString();
    Console.WriteLine("Formatted JSON:");
    Console.WriteLine(prettyJson);
}

// Search and replace
var textContent = "The quick brown fox jumps over the lazy dog";

var replaceParams = new Dictionary<string, object?>
{
    ["text"] = textContent,
    ["search_pattern"] = "fox",
    ["replacement"] = "cat",
    ["use_regex"] = false
};

var replaceResult = await executor.ExecuteAsync("replace_text", replaceParams);

if (replaceResult.IsSuccessful && replaceResult.Data is Dictionary<string, object?> result)
{
    Console.WriteLine($"Modified text: {result["result"]}");
    Console.WriteLine($"Replacements made: {result["count"]}");
}
```

### Making HTTP Requests

```csharp
// Simple GET request
var getParams = new Dictionary<string, object?>
{
    ["url"] = "https://api.example.com/users/123",
    ["method"] = "GET"
};

var getResult = await executor.ExecuteAsync("http_request", getParams);

if (getResult.IsSuccessful && getResult.Data is Dictionary<string, object?> response)
{
    var statusCode = response["status_code"];
    var content = response["content"]?.ToString();
    
    Console.WriteLine($"Status: {statusCode}");
    Console.WriteLine($"Response: {content}");
}

// POST request with headers and body
var postParams = new Dictionary<string, object?>
{
    ["url"] = "https://api.example.com/users",
    ["method"] = "POST",
    ["headers"] = new Dictionary<string, object?>
    {
        ["Content-Type"] = "application/json",
        ["Authorization"] = "Bearer your-token-here"
    },
    ["body"] = JsonSerializer.Serialize(new 
    { 
        name = "Jane Doe",
        email = "jane@example.com"
    })
};

var postResult = await executor.ExecuteAsync("http_request", postParams);
```

## Intermediate Examples

### File Operations with Progress

```csharp
// Copy large file with progress tracking
var progress = new Progress<ToolProgress>(p =>
{
    Console.WriteLine($"Copying: {p.PercentComplete}% - {p.Message}");
});

var context = new ToolExecutionContext
{
    Progress = progress,
    WorkingDirectory = "/data"
};

var copyParams = new Dictionary<string, object?>
{
    ["source_path"] = "large_file.zip",
    ["destination_path"] = "backup/large_file.zip",
    ["overwrite"] = true
};

var copyResult = await executor.ExecuteAsync("copy_file", copyParams, context);

if (copyResult.IsSuccessful)
{
    var data = copyResult.Data as Dictionary<string, object?>;
    Console.WriteLine($"Copied {data?["size"]} bytes");
}
```

### Working with JSON Data

```csharp
var jsonData = @"{
    ""users"": [
        {""id"": 1, ""name"": ""Alice"", ""active"": true},
        {""id"": 2, ""name"": ""Bob"", ""active"": false},
        {""id"": 3, ""name"": ""Charlie"", ""active"": true}
    ]
}";

// Query JSON with JSONPath
var queryParams = new Dictionary<string, object?>
{
    ["json"] = jsonData,
    ["query"] = "$.users[?(@.active == true)].name",
    ["operation"] = "query"
};

var queryResult = await executor.ExecuteAsync("json_processor", queryParams);

if (queryResult.IsSuccessful && queryResult.Data is Dictionary<string, object?> result)
{
    var names = result["result"];
    Console.WriteLine($"Active users: {JsonSerializer.Serialize(names)}");
}
```

### Directory Operations

```csharp
// List all C# files recursively
var listParams = new Dictionary<string, object?>
{
    ["directory_path"] = "src",
    ["pattern"] = "*.cs",
    ["recursive"] = true
};

var listResult = await executor.ExecuteAsync("list_directory", listParams);

if (listResult.IsSuccessful && listResult.Data is Dictionary<string, object?> data)
{
    var entries = data["entries"] as List<object>;
    var totalSize = data["total_size"];
    
    Console.WriteLine($"Found {entries?.Count} C# files");
    Console.WriteLine($"Total size: {totalSize} bytes");
    
    foreach (var entry in entries?.Take(5) ?? Enumerable.Empty<object>())
    {
        if (entry is Dictionary<string, object?> file)
        {
            Console.WriteLine($"- {file["path"]} ({file["size"]} bytes)");
        }
    }
}
```

## Advanced Examples

### Tool Chains for Complex Workflows

```csharp
// Data processing pipeline
var chainBuilder = serviceProvider.GetRequiredService<ToolChainBuilder>();

var pipeline = chainBuilder
    .WithId("etl-pipeline")
    .WithName("ETL Data Pipeline")
    
    // Extract: Read multiple data sources
    .AddToolStep("extract_csv", "Extract CSV Data", "read_file",
        new { file_path = "data/sales.csv" })
    .AddToolStep("extract_json", "Extract JSON Data", "read_file",
        new { file_path = "data/products.json" })
    
    // Transform: Process the data
    .AddToolStep("parse_csv", "Parse CSV", "custom_csv_parser",
        parameters: ctx => new 
        { 
            csv_content = ctx.GetStepResult("extract_csv")?.Data
        },
        dependencies: new[] { "extract_csv" })
    .AddToolStep("parse_json", "Parse JSON", "json_processor",
        parameters: ctx => new 
        { 
            json = ctx.GetStepResult("extract_json")?.Data,
            operation = "parse"
        },
        dependencies: new[] { "extract_json" })
    
    // Combine data
    .AddToolStep("combine", "Combine Data", "custom_data_combiner",
        parameters: ctx => new
        {
            sales_data = ctx.GetStepResult("parse_csv")?.Data,
            product_data = ctx.GetStepResult("parse_json")?.Data
        },
        dependencies: new[] { "parse_csv", "parse_json" })
    
    // Load: Save results
    .AddToolStep("save_report", "Save Report", "write_file",
        parameters: ctx => new
        {
            file_path = $"reports/combined_report_{DateTime.Now:yyyyMMdd}.json",
            content = JsonSerializer.Serialize(ctx.GetStepResult("combine")?.Data)
        },
        dependencies: new[] { "combine" })
    .Build();

// Execute pipeline
var pipelineResult = await pipeline.ExecuteAsync(new ToolExecutionContext());

if (pipelineResult.IsSuccessful)
{
    Console.WriteLine("Pipeline completed successfully");
    foreach (var step in pipelineResult.StepResults)
    {
        Console.WriteLine($"- {step.Key}: {(step.Value.IsSuccessful ? "Success" : "Failed")}");
    }
}
```

### Secure Tool Execution

```csharp
// Configure strict security context
var secureContext = new ToolExecutionContext
{
    Permissions = new ToolPermissions
    {
        FileSystemAccess = true,
        FileSystemOperations = FileSystemOperations.Read,
        AllowedPaths = new[] { "/app/data", "/app/temp" },
        NetworkAccess = true,
        AllowedDomains = new[] { "api.trusted.com", "*.mycompany.com" },
        RequireHttps = true,
        ProcessExecution = false,
        SystemInformationAccess = false
    },
    ResourceLimits = new ToolResourceLimits
    {
        MaxExecutionTime = TimeSpan.FromSeconds(30),
        MaxMemoryMB = 100,
        MaxFileSizeMB = 50,
        MaxOutputSizeBytes = 1024 * 1024 // 1MB
    }
};

// Try to read a file outside allowed paths
var unauthorizedParams = new Dictionary<string, object?>
{
    ["file_path"] = "/etc/passwd"  // Outside allowed paths
};

var unauthorizedResult = await executor.ExecuteAsync("read_file", unauthorizedParams, secureContext);

if (!unauthorizedResult.IsSuccessful)
{
    Console.WriteLine($"Access denied: {unauthorizedResult.ErrorMessage}");
}
```

### Caching for Performance

```csharp
// Enable caching
services.AddAdvancedToolFeatures(options =>
{
    options.EnableCaching = true;
    options.CacheTimeToLive = TimeSpan.FromMinutes(5);
});

// Execute expensive operation multiple times
for (int i = 0; i < 3; i++)
{
    var stopwatch = Stopwatch.StartNew();
    
    var result = await executor.ExecuteAsync("expensive_operation", 
        new Dictionary<string, object?> { ["input"] = "test_data" });
    
    stopwatch.Stop();
    
    var cacheHit = result.Metadata.ContainsKey("cache_hit") && 
                   (bool)result.Metadata["cache_hit"];
    
    Console.WriteLine($"Execution {i + 1}: {stopwatch.ElapsedMilliseconds}ms " +
                     $"(Cache {(cacheHit ? "HIT" : "MISS")})");
}

// Output:
// Execution 1: 2500ms (Cache MISS)
// Execution 2: 5ms (Cache HIT)
// Execution 3: 4ms (Cache HIT)
```

### Error Handling and Retry

```csharp
public async Task<ToolResult> ExecuteWithRetry(
    IToolExecutor executor,
    string toolId,
    Dictionary<string, object?> parameters,
    int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            var result = await executor.ExecuteAsync(toolId, parameters);
            
            if (result.IsSuccessful)
                return result;
            
            // Check if error is retryable
            if (IsRetryableError(result.ErrorCode))
            {
                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    Console.WriteLine($"Attempt {attempt} failed, retrying in {delay}...");
                    await Task.Delay(delay);
                    continue;
                }
            }
            
            return result; // Non-retryable error
        }
        catch (TaskCanceledException)
        {
            if (attempt < maxRetries)
            {
                Console.WriteLine($"Timeout on attempt {attempt}, retrying...");
                continue;
            }
            throw;
        }
    }
    
    return ToolResult.Failure("Max retries exceeded", "MAX_RETRIES");
}

private bool IsRetryableError(string? errorCode)
{
    var retryableCodes = new[] { "TIMEOUT", "NETWORK_ERROR", "RATE_LIMIT" };
    return errorCode != null && retryableCodes.Contains(errorCode);
}
```

### Batch Processing

```csharp
// Process multiple files in parallel
public async Task ProcessFilesInBatch(
    IToolExecutor executor,
    List<string> filePaths,
    int batchSize = 5)
{
    var semaphore = new SemaphoreSlim(batchSize);
    var results = new ConcurrentBag<(string path, ToolResult result)>();
    
    var tasks = filePaths.Select(async filePath =>
    {
        await semaphore.WaitAsync();
        try
        {
            var result = await executor.ExecuteAsync("process_file",
                new Dictionary<string, object?> { ["file_path"] = filePath });
            
            results.Add((filePath, result));
        }
        finally
        {
            semaphore.Release();
        }
    });
    
    await Task.WhenAll(tasks);
    
    // Summary
    var successful = results.Count(r => r.result.IsSuccessful);
    Console.WriteLine($"Processed {successful}/{filePaths.Count} files successfully");
    
    // Report errors
    foreach (var (path, result) in results.Where(r => !r.result.IsSuccessful))
    {
        Console.WriteLine($"Failed: {path} - {result.ErrorMessage}");
    }
}
```

### Custom Tool Integration

```csharp
// Create a custom tool that uses other tools
public class WebScraperTool : ToolBase
{
    private readonly IToolExecutor _executor;
    
    public WebScraperTool(IToolExecutor executor)
    {
        _executor = executor;
    }
    
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "web_scraper",
        Name = "Web Scraper",
        Description = "Scrapes and processes web content",
        RequiredPermissions = ToolPermissionFlags.Network | ToolPermissionFlags.FileSystemWrite
    };
    
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var url = GetParameter<string>(parameters, "url");
        var outputPath = GetParameter<string>(parameters, "output_path", "scraped_data.json");
        
        // Fetch web content
        var fetchResult = await _executor.ExecuteAsync("http_request",
            new Dictionary<string, object?>
            {
                ["url"] = url,
                ["method"] = "GET"
            }, context);
        
        if (!fetchResult.IsSuccessful)
            return fetchResult;
        
        var response = fetchResult.Data as Dictionary<string, object?>;
        var html = response?["content"]?.ToString();
        
        // Extract data (simplified)
        var data = ExtractDataFromHtml(html);
        
        // Save results
        var saveResult = await _executor.ExecuteAsync("write_file",
            new Dictionary<string, object?>
            {
                ["file_path"] = outputPath,
                ["content"] = JsonSerializer.Serialize(data)
            }, context);
        
        return saveResult;
    }
    
    private object ExtractDataFromHtml(string? html)
    {
        // Implement HTML parsing logic
        return new { title = "Extracted Title", content = "..." };
    }
}
```

## Real-World Scenarios

### Log Analysis Pipeline

```csharp
// Analyze application logs
var logAnalyzer = chainBuilder
    .WithId("log-analyzer")
    .WithName("Log Analysis Pipeline")
    
    // Read log file
    .AddToolStep("read_logs", "Read Log File", "read_file",
        new { file_path = "app.log" })
    
    // Search for errors
    .AddToolStep("find_errors", "Find Errors", "search_text",
        parameters: ctx => new
        {
            text = ctx.GetStepResult("read_logs")?.Data,
            pattern = @"ERROR|EXCEPTION|FATAL",
            use_regex = true,
            return_matches = true
        })
    
    // Extract timestamps
    .AddToolStep("extract_times", "Extract Timestamps", "search_text",
        parameters: ctx => new
        {
            text = ctx.GetStepResult("read_logs")?.Data,
            pattern = @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}",
            use_regex = true
        })
    
    // Generate report
    .AddToolStep("generate_report", "Generate Report", "custom_report_generator",
        parameters: ctx => new
        {
            errors = ctx.GetStepResult("find_errors")?.Data,
            timestamps = ctx.GetStepResult("extract_times")?.Data
        })
    
    // Save report
    .AddToolStep("save_report", "Save Report", "write_file",
        parameters: ctx => new
        {
            file_path = $"reports/log_analysis_{DateTime.Now:yyyyMMdd}.json",
            content = JsonSerializer.Serialize(ctx.GetStepResult("generate_report")?.Data)
        })
    .Build();

var analysisResult = await logAnalyzer.ExecuteAsync(new ToolExecutionContext());
```

### API Data Aggregation

```csharp
// Aggregate data from multiple APIs
public async Task<Dictionary<string, object>> AggregateApiData(
    IToolExecutor executor,
    List<string> apiEndpoints)
{
    var aggregatedData = new Dictionary<string, object>();
    var tasks = new List<Task<(string endpoint, ToolResult result)>>();
    
    // Fetch from all endpoints in parallel
    foreach (var endpoint in apiEndpoints)
    {
        var task = executor.ExecuteAsync("http_request",
            new Dictionary<string, object?>
            {
                ["url"] = endpoint,
                ["timeout_seconds"] = 10
            })
            .ContinueWith(t => (endpoint, t.Result));
        
        tasks.Add(task);
    }
    
    var results = await Task.WhenAll(tasks);
    
    // Process results
    foreach (var (endpoint, result) in results)
    {
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> response)
        {
            var content = response["content"]?.ToString();
            if (!string.IsNullOrEmpty(content))
            {
                try
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(content);
                    aggregatedData[endpoint] = data;
                }
                catch
                {
                    aggregatedData[endpoint] = content;
                }
            }
        }
        else
        {
            aggregatedData[endpoint] = new { error = result.ErrorMessage };
        }
    }
    
    return aggregatedData;
}
```

### File Backup System

```csharp
// Create incremental backups
public class BackupService
{
    private readonly IToolExecutor _executor;
    private readonly ILogger<BackupService> _logger;
    
    public async Task<bool> CreateBackup(string sourcePath, string backupRoot)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupRoot, $"backup_{timestamp}");
        
        // List all files to backup
        var listResult = await _executor.ExecuteAsync("list_directory",
            new Dictionary<string, object?>
            {
                ["directory_path"] = sourcePath,
                ["recursive"] = true
            });
        
        if (!listResult.IsSuccessful)
        {
            _logger.LogError($"Failed to list files: {listResult.ErrorMessage}");
            return false;
        }
        
        var data = listResult.Data as Dictionary<string, object?>;
        var entries = data?["entries"] as List<object> ?? new List<object>();
        
        var files = entries
            .Where(e => e is Dictionary<string, object?> entry && 
                       entry["type"]?.ToString() == "file")
            .ToList();
        
        _logger.LogInformation($"Backing up {files.Count} files");
        
        // Copy files with progress
        var progress = new Progress<ToolProgress>(p =>
        {
            _logger.LogInformation($"Backup progress: {p.PercentComplete}%");
        });
        
        var context = new ToolExecutionContext { Progress = progress };
        
        foreach (var file in files)
        {
            if (file is Dictionary<string, object?> fileEntry)
            {
                var relativePath = GetRelativePath(sourcePath, 
                    fileEntry["path"]?.ToString() ?? "");
                var destPath = Path.Combine(backupPath, relativePath);
                
                var copyResult = await _executor.ExecuteAsync("copy_file",
                    new Dictionary<string, object?>
                    {
                        ["source_path"] = fileEntry["path"],
                        ["destination_path"] = destPath,
                        ["create_directories"] = true
                    }, context);
                
                if (!copyResult.IsSuccessful)
                {
                    _logger.LogError($"Failed to copy {fileEntry["path"]}: " +
                                   $"{copyResult.ErrorMessage}");
                }
            }
        }
        
        // Create backup metadata
        var metadata = new
        {
            timestamp,
            source = sourcePath,
            file_count = files.Count,
            created_at = DateTime.UtcNow
        };
        
        await _executor.ExecuteAsync("write_file",
            new Dictionary<string, object?>
            {
                ["file_path"] = Path.Combine(backupPath, "backup_metadata.json"),
                ["content"] = JsonSerializer.Serialize(metadata, 
                    new JsonSerializerOptions { WriteIndented = true })
            });
        
        _logger.LogInformation($"Backup completed: {backupPath}");
        return true;
    }
    
    private string GetRelativePath(string basePath, string fullPath)
    {
        return Path.GetRelativePath(basePath, fullPath);
    }
}
```

## Testing Examples

### Unit Testing Tools

```csharp
[TestClass]
public class ToolExecutionTests
{
    private ServiceProvider _serviceProvider;
    private IToolExecutor _executor;
    
    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddAndyTools();
        services.AddLogging();
        
        _serviceProvider = services.BuildServiceProvider();
        _executor = _serviceProvider.GetRequiredService<IToolExecutor>();
    }
    
    [TestMethod]
    public async Task ReadFile_ValidPath_ReturnsContent()
    {
        // Arrange
        var testFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(testFile, "Test content");
        
        try
        {
            // Act
            var result = await _executor.ExecuteAsync("read_file",
                new Dictionary<string, object?> { ["file_path"] = testFile });
            
            // Assert
            Assert.IsTrue(result.IsSuccessful);
            Assert.IsNotNull(result.Data);
            
            var data = result.Data as Dictionary<string, object?>;
            Assert.AreEqual("Test content", data?["content"]);
        }
        finally
        {
            File.Delete(testFile);
        }
    }
    
    [TestMethod]
    public async Task HttpRequest_InvalidUrl_ReturnsError()
    {
        // Act
        var result = await _executor.ExecuteAsync("http_request",
            new Dictionary<string, object?> { ["url"] = "not-a-valid-url" });
        
        // Assert
        Assert.IsFalse(result.IsSuccessful);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.AreEqual("INVALID_URL", result.ErrorCode);
    }
}
```

### Integration Testing

```csharp
[TestClass]
public class ToolChainIntegrationTests
{
    [TestMethod]
    public async Task DataPipeline_CompleteFlow_Success()
    {
        // Setup test data
        var testData = new { items = new[] { 1, 2, 3 } };
        await File.WriteAllTextAsync("test_input.json", 
            JsonSerializer.Serialize(testData));
        
        try
        {
            // Build and execute pipeline
            var pipeline = BuildTestPipeline();
            var result = await pipeline.ExecuteAsync(new ToolExecutionContext());
            
            // Verify results
            Assert.IsTrue(result.IsSuccessful);
            Assert.IsTrue(File.Exists("test_output.json"));
            
            var output = await File.ReadAllTextAsync("test_output.json");
            Assert.IsTrue(output.Contains("processed"));
        }
        finally
        {
            // Cleanup
            File.Delete("test_input.json");
            File.Delete("test_output.json");
        }
    }
}
```

## Summary

These examples demonstrate:

1. **Basic Usage**: Simple file operations, text processing, HTTP requests
2. **Intermediate**: Progress tracking, JSON processing, directory operations  
3. **Advanced**: Tool chains, security, caching, error handling
4. **Real-World**: Log analysis, API aggregation, backup systems
5. **Testing**: Unit and integration testing patterns

For more details on specific features, refer to:
- [Core Concepts](core-concepts.md)
- [Built-in Tools Reference](tools-reference.md)
- [Creating Custom Tools](custom-tools.md)
- [Advanced Features](advanced-features.md)