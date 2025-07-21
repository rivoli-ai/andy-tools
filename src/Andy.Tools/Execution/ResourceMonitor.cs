using System.Collections.Concurrent;
using System.Diagnostics;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Execution;

/// <summary>
/// Implementation of the resource monitor for tracking tool resource usage.
/// </summary>
public class ResourceMonitor : IResourceMonitor
{
    private readonly ILogger<ResourceMonitor> _logger;
    private readonly ConcurrentDictionary<string, ResourceMonitoringSession> _sessions = new();
    private readonly Timer _monitoringTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceMonitor"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ResourceMonitor(ILogger<ResourceMonitor> logger)
    {
        _logger = logger;

        // Start monitoring timer to update resource usage periodically
        _monitoringTimer = new Timer(UpdateResourceUsage, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <inheritdoc />
    public event EventHandler<ResourceLimitExceededEventArgs>? ResourceLimitExceeded;

    /// <inheritdoc />
    public IResourceMonitoringSession StartMonitoring(string correlationId, ToolResourceLimits limits)
    {
        var session = new ResourceMonitoringSession(correlationId, limits, this, _logger);
        _sessions.TryAdd(correlationId, session);

        _logger.LogDebug("Started resource monitoring for correlation ID {CorrelationId}", correlationId);
        return session;
    }

    /// <inheritdoc />
    public ToolResourceUsage? GetCurrentUsage(string correlationId)
    {
        return _sessions.TryGetValue(correlationId, out var session) ? session.CurrentUsage : null;
    }

    /// <inheritdoc />
    public ToolResourceUsage? StopMonitoring(string correlationId)
    {
        if (_sessions.TryRemove(correlationId, out var session))
        {
            var finalUsage = session.CurrentUsage;
            session.Dispose();

            _logger.LogDebug("Stopped resource monitoring for correlation ID {CorrelationId}", correlationId);
            return finalUsage;
        }

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<IResourceMonitoringSession> GetActiveSessions()
    {
        return _sessions.Values.Cast<IResourceMonitoringSession>().ToList().AsReadOnly();
    }

    internal void OnResourceLimitExceeded(ResourceLimitExceededEventArgs args)
    {
        ResourceLimitExceeded?.Invoke(this, args);
    }

    private void UpdateResourceUsage(object? state)
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var currentMemory = currentProcess.WorkingSet64;

            foreach (var session in _sessions.Values)
            {
                session.UpdateMemoryUsage(currentMemory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update resource usage");
        }
    }

    /// <summary>
    /// Disposes the resource monitor.
    /// </summary>
    public void Dispose()
    {
        _monitoringTimer?.Dispose();

        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Implementation of a resource monitoring session.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ResourceMonitoringSession"/> class.
/// </remarks>
/// <param name="correlationId">The correlation ID.</param>
/// <param name="limits">The resource limits.</param>
/// <param name="monitor">The parent monitor.</param>
/// <param name="logger">The logger.</param>
internal class ResourceMonitoringSession(string correlationId, ToolResourceLimits limits, ResourceMonitor monitor, ILogger logger) : IResourceMonitoringSession
{
    private readonly ResourceMonitor _monitor = monitor;
    private readonly ILogger _logger = logger;
    private readonly object _lockObject = new();
    private readonly List<string> _exceededLimits = [];
    private readonly HashSet<string> _accessedFiles = [];
    private readonly HashSet<string> _accessedHosts = [];
    private readonly HashSet<string> _executedProcesses = [];
    private bool _disposed;

    /// <inheritdoc />
    public string CorrelationId { get; } = correlationId;

    /// <inheritdoc />
    public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public ToolResourceLimits Limits { get; } = limits;

    /// <inheritdoc />
    public ToolResourceUsage CurrentUsage { get; private set; } = new ToolResourceUsage();

    /// <inheritdoc />
    public bool HasExceededLimits => _exceededLimits.Count > 0;

    /// <inheritdoc />
    public IReadOnlyList<string> ExceededLimits => _exceededLimits.AsReadOnly();

    /// <inheritdoc />
    public void RecordFileAccess(string filePath, FileAccessType accessType, long bytesRead = 0, long bytesWritten = 0)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lockObject)
        {
            _accessedFiles.Add(filePath);
            CurrentUsage.FilesAccessed = _accessedFiles.Count;
            CurrentUsage.BytesRead += bytesRead;
            CurrentUsage.BytesWritten += bytesWritten;

            // Check file count limit
            if (CurrentUsage.FilesAccessed > Limits.MaxFileCount && !_exceededLimits.Contains("file_count"))
            {
                _exceededLimits.Add("file_count");
                var args = new ResourceLimitExceededEventArgs(CorrelationId, "file_count", CurrentUsage.FilesAccessed, Limits.MaxFileCount);
                _monitor.OnResourceLimitExceeded(args);

                _logger.LogWarning("File count limit exceeded for correlation {CorrelationId}: {Current} > {Limit}",
                    CorrelationId, CurrentUsage.FilesAccessed, Limits.MaxFileCount);
            }

            // Check file size limit
            var totalFileSize = CurrentUsage.BytesRead + CurrentUsage.BytesWritten;
            if (totalFileSize > Limits.MaxFileSizeBytes && !_exceededLimits.Contains("file_size"))
            {
                _exceededLimits.Add("file_size");
                var args = new ResourceLimitExceededEventArgs(CorrelationId, "file_size", totalFileSize, Limits.MaxFileSizeBytes);
                _monitor.OnResourceLimitExceeded(args);

                _logger.LogWarning("File size limit exceeded for correlation {CorrelationId}: {Current} > {Limit}",
                    CorrelationId, totalFileSize, Limits.MaxFileSizeBytes);
            }
        }
    }

    /// <inheritdoc />
    public void RecordNetworkAccess(string host, long bytesSent, long bytesReceived)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lockObject)
        {
            _accessedHosts.Add(host);
            CurrentUsage.NetworkRequests++;
            CurrentUsage.NetworkBytesSent += bytesSent;
            CurrentUsage.NetworkBytesReceived += bytesReceived;
        }
    }

    /// <inheritdoc />
    public void RecordProcessExecution(string processName)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lockObject)
        {
            _executedProcesses.Add(processName);
            CurrentUsage.ProcessesStarted = _executedProcesses.Count;
        }
    }

    /// <inheritdoc />
    public void UpdateMemoryUsage(long memoryBytes)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lockObject)
        {
            if (memoryBytes > CurrentUsage.PeakMemoryBytes)
            {
                CurrentUsage.PeakMemoryBytes = memoryBytes;
            }

            // Update average memory (simple running average)
            var samples = (DateTimeOffset.UtcNow - StartTime).TotalSeconds;
            CurrentUsage.AverageMemoryBytes = samples > 0 ? (long)(((CurrentUsage.AverageMemoryBytes * (samples - 1)) + memoryBytes) / samples) : memoryBytes;

            // Check memory limit
            if (CurrentUsage.PeakMemoryBytes > Limits.MaxMemoryBytes && !_exceededLimits.Contains("memory"))
            {
                _exceededLimits.Add("memory");
                var args = new ResourceLimitExceededEventArgs(CorrelationId, "memory", CurrentUsage.PeakMemoryBytes, Limits.MaxMemoryBytes);
                _monitor.OnResourceLimitExceeded(args);

                _logger.LogWarning("Memory limit exceeded for correlation {CorrelationId}: {Current} > {Limit}",
                    CorrelationId, CurrentUsage.PeakMemoryBytes, Limits.MaxMemoryBytes);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
