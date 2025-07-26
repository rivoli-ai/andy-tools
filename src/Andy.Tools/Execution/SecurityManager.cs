using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Execution;

/// <summary>
/// Default implementation of the security manager.
/// </summary>
public class SecurityManager : ISecurityManager
{
    private readonly ILogger<SecurityManager> _logger;
    private readonly ConcurrentBag<SecurityViolation> _violations = [];
    private readonly HashSet<string> _blockedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "::1"
    };
    private readonly HashSet<string> _blockedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd.exe",
        "powershell.exe",
        "bash",
        "sh",
        "python.exe",
        "node.exe",
        "ruby.exe"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityManager"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public SecurityManager(ILogger<SecurityManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IList<string> ValidateExecution(ToolMetadata toolMetadata, ToolPermissions permissions)
    {
        var violations = new List<string>();

        // Check tool-specific permissions first
        if (permissions.ToolSpecificPermissions.TryGetValue(toolMetadata.Name, out var toolAllowed) && !toolAllowed)
        {
            violations.Add($"Tool '{toolMetadata.Name}' is explicitly disabled in current permission profile");
            return violations; // Early return if tool is explicitly disabled
        }

        // Check required capabilities against granted permissions
        if (toolMetadata.RequiredCapabilities.HasFlag(ToolCapability.FileSystem) && !permissions.FileSystemAccess)
        {
            violations.Add("Tool requires file system access but it is not granted");
        }

        if (toolMetadata.RequiredCapabilities.HasFlag(ToolCapability.Network) && !permissions.NetworkAccess)
        {
            violations.Add("Tool requires network access but it is not granted");
        }

        if (toolMetadata.RequiredCapabilities.HasFlag(ToolCapability.ProcessExecution) && !permissions.ProcessExecution)
        {
            violations.Add("Tool requires process execution but it is not granted");
        }

        if (toolMetadata.RequiredCapabilities.HasFlag(ToolCapability.Environment) && !permissions.EnvironmentAccess)
        {
            violations.Add("Tool requires environment access but it is not granted");
        }

        // Check for destructive operations
        if (toolMetadata.RequiredCapabilities.HasFlag(ToolCapability.Destructive) && !permissions.CustomPermissions.ContainsKey("allow_destructive"))
        {
            violations.Add("Tool performs destructive operations but explicit permission is not granted");
        }

        // Check for elevated privileges
        if (toolMetadata.RequiredCapabilities.HasFlag(ToolCapability.Elevated) && !permissions.CustomPermissions.ContainsKey("allow_elevated"))
        {
            violations.Add("Tool requires elevated privileges but explicit permission is not granted");
        }

        return violations;
    }

    /// <inheritdoc />
    public bool IsFileAccessAllowed(string filePath, ToolPermissions permissions, FileAccessType accessType)
    {
        if (!permissions.FileSystemAccess)
        {
            return false;
        }

        // Check for invalid path characters or common invalid patterns
        if (filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || 
            filePath.Contains('<') || filePath.Contains('>'))
        {
            return false;
        }

        // Normalize the path
        try
        {
            filePath = Path.GetFullPath(filePath);
        }
        catch
        {
            return false; // Invalid path
        }

        // Check if path is in blocked list first (blocked paths take precedence)
        if (permissions.BlockedPaths != null && permissions.BlockedPaths.Count > 0)
        {
            var blocked = permissions.BlockedPaths.Any(blockedPath =>
            {
                try
                {
                    var normalizedBlockedPath = Path.GetFullPath(blockedPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
                    return filePath.StartsWith(normalizedBlockedPath, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            });

            if (blocked)
            {
                return false;
            }
        }

        // Check if path is in allowed list
        if (permissions.AllowedPaths != null && permissions.AllowedPaths.Count > 0)
        {
            var allowed = permissions.AllowedPaths.Any(allowedPath =>
            {
                try
                {
                    var normalizedAllowedPath = Path.GetFullPath(allowedPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
                    return filePath.StartsWith(normalizedAllowedPath, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            });

            if (!allowed)
            {
                return false;
            }
        }

        // Check for sensitive system directories
        var sensitiveDirectories = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86))
        };

        foreach (var sensitiveDir in sensitiveDirectories.Where(d => !string.IsNullOrEmpty(d)))
        {
            if (filePath.StartsWith(sensitiveDir, StringComparison.OrdinalIgnoreCase))
            {
                // Only allow read access to system directories unless explicitly allowed
                if (accessType != FileAccessType.Read && !permissions.CustomPermissions.ContainsKey("allow_system_write"))
                {
                    return false;
                }
            }
        }

        // Block access to certain file extensions for execute operations
        if (accessType == FileAccessType.Execute)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var blockedExtensions = new[] { ".exe", ".bat", ".cmd", ".ps1", ".sh", ".py", ".js", ".vbs" };

            if (blockedExtensions.Contains(extension) && !permissions.CustomPermissions.ContainsKey("allow_executable"))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public bool IsNetworkAccessAllowed(string host, ToolPermissions permissions)
    {
        if (!permissions.NetworkAccess)
        {
            return false;
        }

        // Check if host is in blocked list first (blocked hosts take precedence)
        if (permissions.BlockedHosts != null && permissions.BlockedHosts.Count > 0)
        {
            var blocked = permissions.BlockedHosts.Any(blockedHost =>
                string.Equals(host, blockedHost, StringComparison.OrdinalIgnoreCase) ||
                (blockedHost.StartsWith("*.") && host.EndsWith(blockedHost[1..], StringComparison.OrdinalIgnoreCase)));

            if (blocked)
            {
                return false;
            }
        }

        // Block localhost access unless explicitly allowed
        if (_blockedHosts.Contains(host) && !permissions.CustomPermissions.ContainsKey("allow_localhost"))
        {
            return false;
        }

        // Check if host is in allowed list
        if (permissions.AllowedHosts != null && permissions.AllowedHosts.Count > 0)
        {
            var allowed = permissions.AllowedHosts.Any(allowedHost =>
                string.Equals(host, allowedHost, StringComparison.OrdinalIgnoreCase) ||
                (allowedHost.StartsWith("*.") && host.EndsWith(allowedHost[1..], StringComparison.OrdinalIgnoreCase)));

            return allowed;
        }

        // Block private IP ranges unless explicitly allowed
        return !IsPrivateIpAddress(host) || permissions.CustomPermissions.ContainsKey("allow_private_networks");
    }

    /// <inheritdoc />
    public bool IsProcessExecutionAllowed(string processName, ToolPermissions permissions)
    {
        if (!permissions.ProcessExecution)
        {
            return false;
        }

        // Block dangerous processes unless explicitly allowed
        return !_blockedProcesses.Contains(processName) || permissions.CustomPermissions.ContainsKey("allow_dangerous_processes");
    }

    /// <inheritdoc />
    public void RecordViolation(string toolId, string correlationId, string violation, SecurityViolationSeverity severity)
    {
        var securityViolation = new SecurityViolation
        {
            ToolId = toolId,
            CorrelationId = correlationId,
            Description = violation,
            Severity = severity,
            Timestamp = DateTimeOffset.UtcNow
        };

        _violations.Add(securityViolation);

        _logger.LogWarning("Security violation recorded for tool {ToolId} (correlation: {CorrelationId}): {Violation}",
            toolId, correlationId, violation);
    }

    /// <inheritdoc />
    public IReadOnlyList<SecurityViolation> GetViolations(string correlationId)
    {
        return _violations
            .Where(v => string.Equals(v.CorrelationId, correlationId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(v => v.Timestamp)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<SecurityViolation> GetAllViolations(DateTimeOffset? since = null)
    {
        var query = _violations.AsEnumerable();

        if (since.HasValue)
        {
            query = query.Where(v => v.Timestamp >= since.Value);
        }

        return query
            .OrderByDescending(v => v.Timestamp)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public int ClearOldViolations(TimeSpan maxAge)
    {
        var cutoff = DateTimeOffset.UtcNow - maxAge;
        
        // ConcurrentBag doesn't support removal, so we need to recreate it
        var allViolations = _violations.ToList();
        var recentViolations = allViolations.Where(v => v.Timestamp >= cutoff).ToList();
        var oldViolations = allViolations.Where(v => v.Timestamp < cutoff).ToList();
        
        // Clear and repopulate with recent violations only
        _violations.Clear();
        foreach (var violation in recentViolations)
        {
            _violations.Add(violation);
        }

        _logger.LogInformation("Cleared {Count} old security violations older than {MaxAge}",
            oldViolations.Count, maxAge);

        return oldViolations.Count;
    }

    private static bool IsPrivateIpAddress(string host)
    {
        if (!System.Net.IPAddress.TryParse(host, out var ipAddress))
        {
            return false;
        }

        var bytes = ipAddress.GetAddressBytes();

        // IPv4 private ranges
        if (bytes.Length == 4)
        {
            // 10.0.0.0/8
            if (bytes[0] == 10)
            {
                return true;
            }

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }
        }

        return false;
    }
}
