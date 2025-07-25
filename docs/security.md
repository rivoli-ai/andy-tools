# Security and Permissions

Andy Tools implements a comprehensive security model to protect systems from malicious or accidental damage. This guide covers the permission system, security best practices, and safe execution patterns.

## Permission System

### Permission Flags

Tools declare required permissions using flags:

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
    EnvironmentVariables = 64,
    AllFileSystem = FileSystemRead | FileSystemWrite | FileSystemDelete,
    All = ~None
}
```

### Declaring Tool Permissions

Tools must declare all permissions they require:

```csharp
public class FileDeleterTool : ToolBase
{
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "delete_files",
        Name = "File Deleter",
        Description = "Deletes files matching a pattern",
        RequiredPermissions = ToolPermissionFlags.FileSystemRead | 
                             ToolPermissionFlags.FileSystemDelete
    };
}
```

### Granting Permissions

Permissions are granted through the execution context:

```csharp
var context = new ToolExecutionContext
{
    Permissions = new ToolPermissions
    {
        FileSystemAccess = true,
        FileSystemOperations = FileSystemOperations.Read | FileSystemOperations.Write,
        AllowedPaths = new[] { "/tmp", "/home/user/data" },
        NetworkAccess = true,
        AllowedDomains = new[] { "api.example.com", "*.trusted.com" },
        ProcessExecution = false,
        SystemInformationAccess = true,
        EnvironmentVariableAccess = EnvironmentVariableAccess.Read
    }
};
```

## File System Security

### Path Validation

Always validate and sanitize file paths:

```csharp
public class SecureFileTool : ToolBase
{
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var filePath = GetParameter<string>(parameters, "file_path");
        
        // Validate path format
        if (!IsValidPath(filePath))
        {
            return ToolResult.Failure("Invalid path format", "INVALID_PATH");
        }
        
        // Prevent directory traversal
        if (filePath.Contains("..") || filePath.Contains("~"))
        {
            return ToolResult.Failure("Path traversal not allowed", "PATH_TRAVERSAL");
        }
        
        // Convert to absolute path
        var absolutePath = Path.GetFullPath(filePath);
        
        // Check against allowed paths
        if (!IsPathAllowed(absolutePath, context))
        {
            return ToolResult.Failure($"Access to path '{absolutePath}' is not allowed", "PATH_DENIED");
        }
        
        // Proceed with operation
    }
    
    private bool IsPathAllowed(string path, ToolExecutionContext context)
    {
        if (context.Permissions?.AllowedPaths == null || 
            context.Permissions.AllowedPaths.Length == 0)
        {
            // No path restrictions
            return true;
        }
        
        foreach (var allowedPath in context.Permissions.AllowedPaths)
        {
            var fullAllowedPath = Path.GetFullPath(allowedPath);
            if (path.StartsWith(fullAllowedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }
}
```

### Safe File Operations

Implement safe file operations with proper error handling:

```csharp
protected async Task<ToolResult> SafeFileOperation(
    string filePath,
    Func<string, Task<ToolResult>> operation,
    ToolExecutionContext context)
{
    try
    {
        // Validate path
        if (!IsPathAllowed(filePath, context))
        {
            return ToolResult.Failure("Path not allowed", "ACCESS_DENIED");
        }
        
        // Check file exists for read operations
        if (!File.Exists(filePath))
        {
            return ToolResult.Failure("File not found", "FILE_NOT_FOUND");
        }
        
        // Perform operation
        return await operation(filePath);
    }
    catch (UnauthorizedAccessException)
    {
        return ToolResult.Failure("Access denied", "ACCESS_DENIED");
    }
    catch (IOException ex)
    {
        return ToolResult.Failure($"IO error: {ex.Message}", "IO_ERROR");
    }
    catch (SecurityException)
    {
        return ToolResult.Failure("Security exception", "SECURITY_ERROR");
    }
}
```

## Network Security

### Domain Whitelisting

Restrict network access to approved domains:

```csharp
public class SecureHttpTool : ToolBase
{
    private readonly HttpClient _httpClient;
    
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var url = GetParameter<string>(parameters, "url");
        
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return ToolResult.Failure("Invalid URL", "INVALID_URL");
        }
        
        // Check domain whitelist
        if (!IsDomainAllowed(uri.Host, context))
        {
            return ToolResult.Failure(
                $"Domain '{uri.Host}' is not in the allowed list",
                "DOMAIN_NOT_ALLOWED"
            );
        }
        
        // Enforce HTTPS for sensitive operations
        if (context.Permissions?.RequireHttps == true && uri.Scheme != "https")
        {
            return ToolResult.Failure("HTTPS required", "HTTPS_REQUIRED");
        }
        
        // Proceed with request
    }
    
    private bool IsDomainAllowed(string domain, ToolExecutionContext context)
    {
        var allowedDomains = context.Permissions?.AllowedDomains;
        if (allowedDomains == null || allowedDomains.Length == 0)
        {
            return true; // No restrictions
        }
        
        foreach (var allowed in allowedDomains)
        {
            if (allowed.StartsWith("*."))
            {
                // Wildcard domain
                var suffix = allowed.Substring(1);
                if (domain.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (domain.Equals(allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }
}
```

### Request Timeouts

Always set appropriate timeouts:

```csharp
protected async Task<HttpResponseMessage> SafeHttpRequest(
    HttpRequestMessage request,
    ToolExecutionContext context)
{
    var timeout = context.ResourceLimits?.NetworkTimeout ?? TimeSpan.FromSeconds(30);
    
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
    cts.CancelAfter(timeout);
    
    try
    {
        return await _httpClient.SendAsync(request, cts.Token);
    }
    catch (TaskCanceledException) when (!context.CancellationToken.IsCancellationRequested)
    {
        throw new TimeoutException($"Request timed out after {timeout}");
    }
}
```

## Process Execution Security

### Command Validation

Validate all process execution requests:

```csharp
public class SecureProcessTool : ToolBase
{
    private readonly string[] _allowedCommands = { "git", "npm", "dotnet" };
    
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var command = GetParameter<string>(parameters, "command");
        var args = GetParameter<string>(parameters, "arguments");
        
        // Check if process execution is allowed
        if (!context.Permissions?.ProcessExecution ?? false)
        {
            return ToolResult.Failure("Process execution not allowed", "NO_PERMISSION");
        }
        
        // Validate command
        if (!IsCommandAllowed(command))
        {
            return ToolResult.Failure($"Command '{command}' is not allowed", "COMMAND_NOT_ALLOWED");
        }
        
        // Sanitize arguments
        if (ContainsDangerousCharacters(args))
        {
            return ToolResult.Failure("Invalid characters in arguments", "INVALID_ARGS");
        }
        
        // Execute with restrictions
        var processInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            UseShellExecute = false,  // Never use shell
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        // Apply additional restrictions...
    }
    
    private bool IsCommandAllowed(string command)
    {
        var commandName = Path.GetFileNameWithoutExtension(command);
        return _allowedCommands.Contains(commandName, StringComparer.OrdinalIgnoreCase);
    }
    
    private bool ContainsDangerousCharacters(string input)
    {
        // Check for shell injection attempts
        var dangerous = new[] { ";", "&", "|", "`", "$", "(", ")", "<", ">", "\n", "\r" };
        return dangerous.Any(c => input.Contains(c));
    }
}
```

## Resource Limits

### Memory Limits

Monitor and enforce memory usage:

```csharp
public class MemoryAwareTool : ToolBase
{
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var dataSize = GetParameter<int>(parameters, "data_size");
        var maxMemoryMB = context.ResourceLimits?.MaxMemoryMB ?? 100;
        
        // Estimate memory usage
        var estimatedMB = dataSize / (1024.0 * 1024.0);
        if (estimatedMB > maxMemoryMB)
        {
            return ToolResult.Failure(
                $"Estimated memory usage ({estimatedMB:F2}MB) exceeds limit ({maxMemoryMB}MB)",
                "MEMORY_LIMIT_EXCEEDED"
            );
        }
        
        // Monitor actual usage
        var initialMemory = GC.GetTotalMemory(false);
        
        try
        {
            // Perform operation
            var data = new byte[dataSize];
            
            // Check memory usage
            var currentMemory = GC.GetTotalMemory(false);
            var usedMB = (currentMemory - initialMemory) / (1024.0 * 1024.0);
            
            if (usedMB > maxMemoryMB)
            {
                // Clean up and fail
                data = null;
                GC.Collect();
                
                return ToolResult.Failure(
                    $"Memory limit exceeded: {usedMB:F2}MB > {maxMemoryMB}MB",
                    "MEMORY_LIMIT_EXCEEDED"
                );
            }
            
            return ToolResult.Success(new { size = dataSize, memory_used_mb = usedMB });
        }
        finally
        {
            // Force cleanup
            GC.Collect();
        }
    }
}
```

### Execution Time Limits

Enforce execution timeouts:

```csharp
protected async Task<ToolResult> ExecuteWithTimeout(
    Func<CancellationToken, Task<ToolResult>> operation,
    ToolExecutionContext context)
{
    var timeout = context.ResourceLimits?.MaxExecutionTime ?? TimeSpan.FromMinutes(5);
    
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
    cts.CancelAfter(timeout);
    
    try
    {
        return await operation(cts.Token);
    }
    catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
    {
        return ToolResult.Failure($"Operation timed out after {timeout}", "TIMEOUT");
    }
}
```

## Input Validation

### Parameter Sanitization

Always sanitize user inputs:

```csharp
public static class InputSanitizer
{
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty");
        
        // Remove invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", fileName.Split(invalidChars));
        
        // Prevent special names
        var reserved = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "LPT1" };
        if (reserved.Contains(sanitized.ToUpperInvariant()))
        {
            sanitized = "_" + sanitized;
        }
        
        // Limit length
        if (sanitized.Length > 255)
        {
            sanitized = sanitized.Substring(0, 255);
        }
        
        return sanitized;
    }
    
    public static string SanitizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty");
        
        // Remove null bytes
        path = path.Replace("\0", "");
        
        // Normalize path separators
        path = path.Replace('/', Path.DirectorySeparatorChar);
        path = path.Replace('\\', Path.DirectorySeparatorChar);
        
        // Remove consecutive separators
        var separator = Path.DirectorySeparatorChar.ToString();
        while (path.Contains(separator + separator))
        {
            path = path.Replace(separator + separator, separator);
        }
        
        return path;
    }
}
```

### SQL Injection Prevention

For tools that interact with databases:

```csharp
public class DatabaseQueryTool : ToolBase
{
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var tableName = GetParameter<string>(parameters, "table");
        var columnName = GetParameter<string>(parameters, "column");
        var value = GetParameter<string>(parameters, "value");
        
        // Validate identifiers
        if (!IsValidIdentifier(tableName) || !IsValidIdentifier(columnName))
        {
            return ToolResult.Failure("Invalid table or column name", "INVALID_IDENTIFIER");
        }
        
        // Use parameterized queries
        var query = $"SELECT * FROM [{tableName}] WHERE [{columnName}] = @value";
        
        using var command = new SqlCommand(query);
        command.Parameters.AddWithValue("@value", value);
        
        // Execute safely...
    }
    
    private bool IsValidIdentifier(string identifier)
    {
        // Only allow alphanumeric and underscore
        return Regex.IsMatch(identifier, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }
}
```

## Security Configuration

### Default Security Settings

Configure default security settings:

```csharp
public class SecurityConfiguration
{
    public bool RequirePermissions { get; set; } = true;
    public bool EnforceSandbox { get; set; } = true;
    public bool LogSecurityEvents { get; set; } = true;
    public int MaxConcurrentTools { get; set; } = 10;
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public long MaxOutputSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
    
    public string[] DefaultAllowedPaths { get; set; } = 
    {
        Path.GetTempPath(),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
    };
    
    public string[] DefaultAllowedDomains { get; set; } = 
    {
        "api.github.com",
        "*.githubusercontent.com"
    };
}

// Apply configuration
services.Configure<SecurityConfiguration>(options =>
{
    options.RequirePermissions = true;
    options.EnforceSandbox = true;
    options.DefaultTimeout = TimeSpan.FromMinutes(2);
});
```

### Security Manager

Implement a centralized security manager:

```csharp
public interface ISecurityManager
{
    Task<bool> ValidatePermissionsAsync(ITool tool, ToolExecutionContext context);
    Task<bool> ValidatePathAccessAsync(string path, FileOperation operation, ToolExecutionContext context);
    Task<bool> ValidateNetworkAccessAsync(Uri uri, ToolExecutionContext context);
    void LogSecurityEvent(string eventType, string details, bool allowed);
}

public class SecurityManager : ISecurityManager
{
    private readonly ILogger<SecurityManager> _logger;
    private readonly SecurityConfiguration _config;
    
    public async Task<bool> ValidatePermissionsAsync(ITool tool, ToolExecutionContext context)
    {
        var required = tool.Metadata.RequiredPermissions;
        var granted = context.Permissions;
        
        // Check each required permission
        if ((required & ToolPermissionFlags.FileSystemRead) != 0 &&
            !granted?.FileSystemAccess == true)
        {
            LogSecurityEvent("FileSystemRead", $"Tool {tool.Metadata.Id} denied", false);
            return false;
        }
        
        // Additional checks...
        
        return true;
    }
}
```

## Isolation and Sandboxing

### Process Isolation

Run tools in isolated processes:

```csharp
public class IsolatedToolExecutor : IToolExecutor
{
    public async Task<ToolResult> ExecuteAsync(
        string toolId,
        Dictionary<string, object?> parameters,
        ToolExecutionContext? context)
    {
        // Create isolated process
        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"tool-runner.dll --tool {toolId}",
            UseShellExecute = false,
            CreateNoWindow = true,
            
            // Isolation settings
            UserName = "sandboxuser",  // Run as limited user
            LoadUserProfile = false,
            
            // Environment restrictions
            Environment =
            {
                ["TOOL_SANDBOX"] = "true",
                ["TOOL_TIMEOUT"] = context?.ResourceLimits?.MaxExecutionTime?.ToString() ?? "300"
            }
        };
        
        // Execute in sandbox...
    }
}
```

### Container Execution

Run tools in containers for maximum isolation:

```yaml
# Docker compose for tool isolation
version: '3.8'
services:
  tool-sandbox:
    image: andytools/sandbox:latest
    security_opt:
      - no-new-privileges:true
      - apparmor:docker-default
    cap_drop:
      - ALL
    read_only: true
    tmpfs:
      - /tmp
    mem_limit: 512m
    cpus: '1.0'
    environment:
      - TOOL_SANDBOX=true
```

## Audit and Monitoring

### Security Event Logging

Log all security-relevant events:

```csharp
public class SecurityAuditLogger
{
    private readonly ILogger<SecurityAuditLogger> _logger;
    
    public void LogToolExecution(string toolId, string userId, bool allowed, string reason = null)
    {
        _logger.LogInformation(
            "Tool execution {Status}: Tool={ToolId}, User={UserId}, Reason={Reason}",
            allowed ? "ALLOWED" : "DENIED",
            toolId,
            userId,
            reason ?? "N/A"
        );
    }
    
    public void LogFileAccess(string path, FileOperation operation, bool allowed)
    {
        _logger.LogInformation(
            "File access {Status}: Path={Path}, Operation={Operation}",
            allowed ? "ALLOWED" : "DENIED",
            path,
            operation
        );
    }
    
    public void LogNetworkAccess(string url, bool allowed)
    {
        _logger.LogInformation(
            "Network access {Status}: URL={URL}",
            allowed ? "ALLOWED" : "DENIED",
            url
        );
    }
}
```

### Metrics Collection

Track security metrics:

```csharp
public class SecurityMetrics
{
    private readonly IMeterFactory _meterFactory;
    private readonly Meter _meter;
    private readonly Counter<long> _deniedExecutions;
    private readonly Counter<long> _allowedExecutions;
    
    public SecurityMetrics(IMeterFactory meterFactory)
    {
        _meterFactory = meterFactory;
        _meter = _meterFactory.Create("AndyTools.Security");
        
        _deniedExecutions = _meter.CreateCounter<long>("tool_executions_denied");
        _allowedExecutions = _meter.CreateCounter<long>("tool_executions_allowed");
    }
    
    public void RecordExecution(string toolId, bool allowed)
    {
        if (allowed)
        {
            _allowedExecutions.Add(1, new KeyValuePair<string, object?>("tool", toolId));
        }
        else
        {
            _deniedExecutions.Add(1, new KeyValuePair<string, object?>("tool", toolId));
        }
    }
}
```

## Security Best Practices

### For Tool Developers

1. **Principle of Least Privilege**: Request only necessary permissions
2. **Input Validation**: Never trust user input
3. **Error Handling**: Don't expose sensitive information in errors
4. **Resource Cleanup**: Always clean up resources, even on failure
5. **Secure Defaults**: Default to the most secure configuration

### For Tool Users

1. **Review Permissions**: Understand what permissions tools require
2. **Limit Scope**: Grant minimal permissions needed
3. **Use Sandboxing**: Run untrusted tools in isolation
4. **Monitor Execution**: Review logs and audit trails
5. **Update Regularly**: Keep tools and framework updated

### Security Checklist

- [ ] All inputs validated and sanitized
- [ ] File paths checked for traversal attacks
- [ ] Network domains whitelisted
- [ ] Process execution validated
- [ ] Resource limits enforced
- [ ] Timeouts configured
- [ ] Errors don't leak sensitive data
- [ ] Permissions properly declared
- [ ] Security events logged
- [ ] Code reviewed for vulnerabilities

## Summary

Security in Andy Tools is multi-layered:

1. **Permission System**: Fine-grained control over tool capabilities
2. **Input Validation**: Comprehensive sanitization and validation
3. **Resource Limits**: Protection against resource exhaustion
4. **Isolation**: Sandboxing and process isolation options
5. **Monitoring**: Comprehensive logging and metrics

By following these security practices, you can build and use tools that are both powerful and safe.