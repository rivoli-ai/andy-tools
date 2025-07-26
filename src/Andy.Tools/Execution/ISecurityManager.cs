using Andy.Tools.Core;

namespace Andy.Tools.Execution;

/// <summary>
/// Interface for managing tool execution security.
/// </summary>
public interface ISecurityManager
{
    /// <summary>
    /// Validates whether a tool can be executed with the given permissions.
    /// </summary>
    /// <param name="toolMetadata">The tool metadata.</param>
    /// <param name="permissions">The granted permissions.</param>
    /// <returns>A list of security violations, or empty if allowed.</returns>
    public IList<string> ValidateExecution(ToolMetadata toolMetadata, ToolPermissions permissions);

    /// <summary>
    /// Checks if a file path access is allowed.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="permissions">The granted permissions.</param>
    /// <param name="accessType">The type of access (read, write, delete).</param>
    /// <returns>True if access is allowed.</returns>
    public bool IsFileAccessAllowed(string filePath, ToolPermissions permissions, FileAccessType accessType);

    /// <summary>
    /// Checks if a network host access is allowed.
    /// </summary>
    /// <param name="host">The host to access.</param>
    /// <param name="permissions">The granted permissions.</param>
    /// <returns>True if access is allowed.</returns>
    public bool IsNetworkAccessAllowed(string host, ToolPermissions permissions);

    /// <summary>
    /// Checks if process execution is allowed.
    /// </summary>
    /// <param name="processName">The process name.</param>
    /// <param name="permissions">The granted permissions.</param>
    /// <returns>True if execution is allowed.</returns>
    public bool IsProcessExecutionAllowed(string processName, ToolPermissions permissions);

    /// <summary>
    /// Records a security violation.
    /// </summary>
    /// <param name="toolId">The tool ID.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="violation">The violation description.</param>
    /// <param name="severity">The violation severity.</param>
    public void RecordViolation(string toolId, string correlationId, string violation, SecurityViolationSeverity severity);

    /// <summary>
    /// Gets security violations for a specific correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>A list of security violations.</returns>
    public IReadOnlyList<SecurityViolation> GetViolations(string correlationId);

    /// <summary>
    /// Gets all security violations.
    /// </summary>
    /// <param name="since">Optional filter to get violations since a specific time.</param>
    /// <returns>A list of security violations.</returns>
    public IReadOnlyList<SecurityViolation> GetAllViolations(DateTimeOffset? since = null);

    /// <summary>
    /// Clears violations older than the specified age.
    /// </summary>
    /// <param name="maxAge">The maximum age of violations to keep.</param>
    /// <returns>The number of violations cleared.</returns>
    public int ClearOldViolations(TimeSpan maxAge);
}

/// <summary>
/// Represents the type of file access.
/// </summary>
public enum FileAccessType
{
    /// <summary>Read access to a file.</summary>
    Read,
    /// <summary>Write access to a file.</summary>
    Write,
    /// <summary>Delete access to a file.</summary>
    Delete,
    /// <summary>Execute access to a file.</summary>
    Execute
}

/// <summary>
/// Represents a security violation.
/// </summary>
public class SecurityViolation
{
    /// <summary>
    /// Gets or sets the tool ID that caused the violation.
    /// </summary>
    public string ToolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correlation ID.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the violation description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the severity of the violation.
    /// </summary>
    public SecurityViolationSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets when the violation occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets additional context about the violation.
    /// </summary>
    public Dictionary<string, object?> Context { get; set; } = [];
}

/// <summary>
/// Interface for monitoring resource usage during tool execution.
/// </summary>
public interface IResourceMonitor
{
    /// <summary>
    /// Starts monitoring resource usage for a tool execution.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="limits">The resource limits.</param>
    /// <returns>A monitoring session.</returns>
    public IResourceMonitoringSession StartMonitoring(string correlationId, ToolResourceLimits limits);

    /// <summary>
    /// Gets current resource usage for a monitoring session.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>Current resource usage, or null if not found.</returns>
    public ToolResourceUsage? GetCurrentUsage(string correlationId);

    /// <summary>
    /// Stops monitoring and returns final resource usage.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>Final resource usage, or null if not found.</returns>
    public ToolResourceUsage? StopMonitoring(string correlationId);

    /// <summary>
    /// Gets all active monitoring sessions.
    /// </summary>
    /// <returns>A list of active monitoring sessions.</returns>
    public IReadOnlyList<IResourceMonitoringSession> GetActiveSessions();

    /// <summary>
    /// Event raised when resource limits are exceeded.
    /// </summary>
    public event EventHandler<ResourceLimitExceededEventArgs>? ResourceLimitExceeded;
}

/// <summary>
/// Interface for a resource monitoring session.
/// </summary>
public interface IResourceMonitoringSession : IDisposable
{
    /// <summary>
    /// Gets the correlation ID for this session.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Gets when the monitoring started.
    /// </summary>
    public DateTimeOffset StartTime { get; }

    /// <summary>
    /// Gets the resource limits for this session.
    /// </summary>
    public ToolResourceLimits Limits { get; }

    /// <summary>
    /// Gets the current resource usage.
    /// </summary>
    public ToolResourceUsage CurrentUsage { get; }

    /// <summary>
    /// Gets whether any resource limits have been exceeded.
    /// </summary>
    public bool HasExceededLimits { get; }

    /// <summary>
    /// Gets the exceeded limits.
    /// </summary>
    public IReadOnlyList<string> ExceededLimits { get; }

    /// <summary>
    /// Records file access for monitoring.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="accessType">The access type.</param>
    /// <param name="bytesRead">Bytes read (if applicable).</param>
    /// <param name="bytesWritten">Bytes written (if applicable).</param>
    public void RecordFileAccess(string filePath, FileAccessType accessType, long bytesRead = 0, long bytesWritten = 0);

    /// <summary>
    /// Records network access for monitoring.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="bytesSent">Bytes sent.</param>
    /// <param name="bytesReceived">Bytes received.</param>
    public void RecordNetworkAccess(string host, long bytesSent, long bytesReceived);

    /// <summary>
    /// Records process execution for monitoring.
    /// </summary>
    /// <param name="processName">The process name.</param>
    public void RecordProcessExecution(string processName);

    /// <summary>
    /// Updates memory usage.
    /// </summary>
    /// <param name="memoryBytes">Current memory usage in bytes.</param>
    public void UpdateMemoryUsage(long memoryBytes);
}

/// <summary>
/// Event arguments for resource limit exceeded events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ResourceLimitExceededEventArgs"/> class.
/// </remarks>
/// <param name="correlationId">The correlation ID.</param>
/// <param name="limitType">The limit type.</param>
/// <param name="currentValue">The current value.</param>
/// <param name="limitValue">The limit value.</param>
public class ResourceLimitExceededEventArgs(string correlationId, string limitType, long currentValue, long limitValue) : EventArgs
{
    /// <summary>
    /// Gets the correlation ID.
    /// </summary>
    public string CorrelationId { get; } = correlationId;

    /// <summary>
    /// Gets the limit that was exceeded.
    /// </summary>
    public string LimitType { get; } = limitType;

    /// <summary>
    /// Gets the current value.
    /// </summary>
    public long CurrentValue { get; } = currentValue;

    /// <summary>
    /// Gets the limit value.
    /// </summary>
    public long LimitValue { get; } = limitValue;
}
