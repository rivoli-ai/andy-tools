using Andy.Tools.Core;
using Andy.Tools.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Examples;

public static class SecurityExamples
{
    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Security and Permissions Examples ===\n");
        Console.WriteLine("⚠️  These examples demonstrate security features.\n");

        var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();
        var securityManager = serviceProvider.GetRequiredService<ISecurityManager>();

        // Example 1: Permission restrictions
        Console.WriteLine("1. Permission Restrictions:");
        await DemonstratePermissions(toolExecutor);

        // Example 2: Resource limits
        Console.WriteLine("\n2. Resource Limits:");
        await DemonstrateResourceLimits(toolExecutor);

        // Example 3: Security violations
        Console.WriteLine("\n3. Security Violation Monitoring:");
        await DemonstrateSecurityViolations(toolExecutor, securityManager);

        // Example 4: Safe execution patterns
        Console.WriteLine("\n4. Safe Execution Patterns:");
        await DemonstrateSafeExecution(toolExecutor);
    }

    private static async Task DemonstratePermissions(IToolExecutor toolExecutor)
    {
        // Create a restricted context - no file system access
        var restrictedContext = new ToolExecutionContext
        {
            Permissions = new ToolPermissions
            {
                FileSystemAccess = false,
                NetworkAccess = false,
                ProcessExecution = false,
                EnvironmentAccess = true // Only allow environment access
            }
        };

        // Try to read a file with restricted permissions
        var readParams = new Dictionary<string, object?>
        {
            ["file_path"] = "/etc/hosts"
        };

        Console.WriteLine("Attempting to read file with restricted permissions...");
        var result = await toolExecutor.ExecuteAsync("read_file", readParams, restrictedContext);
        
        if (!result.IsSuccessful)
        {
            Console.WriteLine($"Expected failure: {result.ErrorMessage}");
        }

        // Try an allowed operation
        var dateParams = new Dictionary<string, object?>
        {
            ["operation"] = "now"
        };

        Console.WriteLine("\nExecuting allowed operation (datetime)...");
        var dateResult = await toolExecutor.ExecuteAsync("datetime", dateParams, restrictedContext);
        
        if (dateResult.IsSuccessful)
        {
            Console.WriteLine($"Success: {dateResult.Data}");
        }
    }

    private static async Task DemonstrateResourceLimits(IToolExecutor toolExecutor)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "andy-tools-security");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Set strict resource limits
            var limitedContext = new ToolExecutionContext
            {
                WorkingDirectory = tempDir,
                ResourceLimits = new ToolResourceLimits
                {
                    MaxExecutionTimeMs = 2000,
                    MaxMemoryBytes = 50 * 1024 * 1024,
                    MaxFileSizeBytes = 1024 * 1024, // 1MB limit
                    MaxFileCount = 5
                }
            };

            // Try to create a file that exceeds size limit
            var largeContent = new string('X', 2 * 1024 * 1024); // 2MB of data
            var writeParams = new Dictionary<string, object?>
            {
                ["file_path"] = "large-file.txt",
                ["content"] = largeContent
            };

            Console.WriteLine("Attempting to write file exceeding size limit...");
            var result = await toolExecutor.ExecuteAsync("write_file", writeParams, limitedContext);
            
            if (!result.IsSuccessful)
            {
                Console.WriteLine($"Expected failure: {result.ErrorMessage}");
            }

            // Try within limits
            var smallContent = "This content is within limits.";
            var smallParams = new Dictionary<string, object?>
            {
                ["file_path"] = "small-file.txt",
                ["content"] = smallContent
            };

            Console.WriteLine("\nWriting file within limits...");
            var smallResult = await toolExecutor.ExecuteAsync("write_file", smallParams, limitedContext);
            
            if (smallResult.IsSuccessful)
            {
                Console.WriteLine("Success: File written within resource limits");
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static async Task DemonstrateSecurityViolations(IToolExecutor toolExecutor, ISecurityManager securityManager)
    {
        // Subscribe to security events
        var violations = new List<SecurityViolation>();
        
        // Security violations will be tracked internally by the manager

        var context = new ToolExecutionContext
        {
            Permissions = new ToolPermissions
            {
                FileSystemAccess = true,
                NetworkAccess = false // Network is disabled
            }
        };

        // Attempt network operation with disabled permissions
        var httpParams = new Dictionary<string, object?>
        {
            ["url"] = "https://example.com",
            ["method"] = "GET"
        };

        Console.WriteLine("Attempting network request with network permissions disabled...");
        var result = await toolExecutor.ExecuteAsync("http_request", httpParams, context);
        
        if (!result.IsSuccessful)
        {
            Console.WriteLine($"Request blocked: {result.ErrorMessage}");
        }

        // Check violations
        if (violations.Count > 0)
        {
            Console.WriteLine($"\nTotal violations recorded: {violations.Count}");
            foreach (var violation in violations)
            {
                Console.WriteLine($"- Tool: {violation.ToolId}, Description: {violation.Description}, Severity: {violation.Severity}");
            }
        }
    }

    private static async Task DemonstrateSafeExecution(IToolExecutor toolExecutor)
    {
        var safeDir = Path.Combine(Path.GetTempPath(), "andy-tools-safe-exec");
        Directory.CreateDirectory(safeDir);

        try
        {
            // Create a sandbox context
            var sandboxContext = new ToolExecutionContext
            {
                WorkingDirectory = safeDir,
                SessionId = Guid.NewGuid().ToString(),
                UserId = "sandbox-user",
                Permissions = new ToolPermissions
                {
                    FileSystemAccess = true,
                    NetworkAccess = false,
                    ProcessExecution = false,
                    EnvironmentAccess = false
                },
                ResourceLimits = new ToolResourceLimits
                {
                    MaxExecutionTimeMs = 10000,
                    MaxMemoryBytes = 100 * 1024 * 1024,
                    MaxFileSizeBytes = 10 * 1024 * 1024
                },
                AdditionalData = new Dictionary<string, object?>
                {
                    ["sandbox"] = true,
                    ["allow_parent_access"] = false
                }
            };

            // Safe operations within sandbox
            Console.WriteLine("Executing safe operations in sandbox...");

            // Create a file in sandbox
            var createParams = new Dictionary<string, object?>
            {
                ["file_path"] = "sandbox-file.txt",
                ["content"] = "This file is created in the sandbox"
            };

            var createResult = await toolExecutor.ExecuteAsync("write_file", createParams, sandboxContext);
            Console.WriteLine($"Create file in sandbox: {(createResult.IsSuccessful ? "Success" : createResult.ErrorMessage)}");

            // Try to access parent directory (should fail with RestrictToWorkingDirectory)
            var parentParams = new Dictionary<string, object?>
            {
                ["file_path"] = "../outside-sandbox.txt",
                ["content"] = "This should not be created"
            };

            var parentResult = await toolExecutor.ExecuteAsync("write_file", parentParams, sandboxContext);
            Console.WriteLine($"Access parent directory: {(parentResult.IsSuccessful ? "Unexpected success!" : "Properly blocked")}");

            // List sandbox contents
            var listParams = new Dictionary<string, object?>
            {
                ["directory_path"] = "."
            };

            var listResult = await toolExecutor.ExecuteAsync("list_directory", listParams, sandboxContext);
            
            if (listResult.IsSuccessful && listResult.Data is Dictionary<string, object?> data)
            {
                if (data.TryGetValue("items", out var items) && items is List<object> files)
                {
                    Console.WriteLine($"Sandbox contains {files.Count} file(s)");
                }
            }
        }
        finally
        {
            try { Directory.Delete(safeDir, true); } catch { }
        }
    }
}