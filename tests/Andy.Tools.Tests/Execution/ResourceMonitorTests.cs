using System.Collections.Generic;
using System.Linq;
using Andy.Tools.Core;
using Andy.Tools.Execution;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Andy.Tools.Tests.Execution;

public class ResourceMonitorTests : IDisposable
{
    private readonly Mock<ILogger<ResourceMonitor>> _mockLogger;
    private readonly ResourceMonitor _resourceMonitor;
    private readonly ToolResourceLimits _testLimits;

    public ResourceMonitorTests()
    {
        _mockLogger = new Mock<ILogger<ResourceMonitor>>();
        _resourceMonitor = new ResourceMonitor(_mockLogger.Object);
        _testLimits = new ToolResourceLimits
        {
            MaxMemoryBytes = 1024 * 1024, // 1 MB
            MaxFileCount = 10,
            MaxFileSizeBytes = 1024 * 1024 // 1 MB
        };
    }

    [Fact]
    public void StartMonitoring_ValidInput_ShouldReturnSession()
    {
        // Arrange
        var correlationId = "test-correlation-id";

        // Act
        var session = _resourceMonitor.StartMonitoring(correlationId, _testLimits);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(correlationId, session.CorrelationId);
        Assert.Equal(_testLimits.MaxMemoryBytes, session.Limits.MaxMemoryBytes);
        Assert.Equal(_testLimits.MaxFileCount, session.Limits.MaxFileCount);
        Assert.Equal(_testLimits.MaxFileSizeBytes, session.Limits.MaxFileSizeBytes);
    }

    [Fact]
    public void GetCurrentUsage_ExistingSession_ShouldReturnUsage()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var session = _resourceMonitor.StartMonitoring(correlationId, _testLimits);

        // Act
        var usage = _resourceMonitor.GetCurrentUsage(correlationId);

        // Assert
        Assert.NotNull(usage);
        Assert.Equal(0, usage.FilesAccessed);
        Assert.Equal(0, usage.BytesRead);
        Assert.Equal(0, usage.BytesWritten);
    }

    [Fact]
    public void GetCurrentUsage_NonExistentSession_ShouldReturnNull()
    {
        // Act
        var usage = _resourceMonitor.GetCurrentUsage("non-existent");

        // Assert
        Assert.Null(usage);
    }

    [Fact]
    public void StopMonitoring_ExistingSession_ShouldReturnFinalUsage()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var session = _resourceMonitor.StartMonitoring(correlationId, _testLimits);

        // Record some activity
        session.RecordFileAccess("/test/file.txt", FileAccessType.Read, 100, 0);

        // Act
        var finalUsage = _resourceMonitor.StopMonitoring(correlationId);

        // Assert
        Assert.NotNull(finalUsage);
        Assert.Equal(1, finalUsage.FilesAccessed);
        Assert.Equal(100, finalUsage.BytesRead);

        // Verify session is removed
        var currentUsage = _resourceMonitor.GetCurrentUsage(correlationId);
        Assert.Null(currentUsage);
    }

    [Fact]
    public void StopMonitoring_NonExistentSession_ShouldReturnNull()
    {
        // Act
        var finalUsage = _resourceMonitor.StopMonitoring("non-existent");

        // Assert
        Assert.Null(finalUsage);
    }

    [Fact]
    public void GetActiveSessions_WithMultipleSessions_ShouldReturnAllSessions()
    {
        // Arrange
        var session1 = _resourceMonitor.StartMonitoring("correlation-1", _testLimits);
        var session2 = _resourceMonitor.StartMonitoring("correlation-2", _testLimits);

        // Act
        var activeSessions = _resourceMonitor.GetActiveSessions();

        // Assert
        Assert.Equal(2, activeSessions.Count);
        Assert.Contains(activeSessions, s => s.CorrelationId == "correlation-1");
        Assert.Contains(activeSessions, s => s.CorrelationId == "correlation-2");
    }

    [Fact]
    public void ResourceMonitoringSession_RecordFileAccess_ShouldUpdateUsage()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var session = _resourceMonitor.StartMonitoring(correlationId, _testLimits);

        // Act
        session.RecordFileAccess("/test/file1.txt", FileAccessType.Read, 100, 0);
        session.RecordFileAccess("/test/file2.txt", FileAccessType.Write, 0, 200);

        // Assert
        var usage = session.CurrentUsage;
        Assert.Equal(2, usage.FilesAccessed);
        Assert.Equal(100, usage.BytesRead);
        Assert.Equal(200, usage.BytesWritten);
    }

    [Fact]
    public void ResourceMonitoringSession_RecordNetworkAccess_ShouldUpdateUsage()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var session = _resourceMonitor.StartMonitoring(correlationId, _testLimits);

        // Act
        session.RecordNetworkAccess("example.com", 100, 200);
        session.RecordNetworkAccess("api.test.com", 50, 75);

        // Assert
        var usage = session.CurrentUsage;
        Assert.Equal(2, usage.NetworkRequests);
        Assert.Equal(150, usage.NetworkBytesSent);
        Assert.Equal(275, usage.NetworkBytesReceived);
    }

    [Fact]
    public void ResourceMonitoringSession_RecordProcessExecution_ShouldUpdateUsage()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var session = _resourceMonitor.StartMonitoring(correlationId, _testLimits);

        // Act
        session.RecordProcessExecution("notepad.exe");
        session.RecordProcessExecution("calc.exe");

        // Assert
        var usage = session.CurrentUsage;
        Assert.Equal(2, usage.ProcessesStarted);
    }

    [Fact]
    public void ResourceMonitoringSession_UpdateMemoryUsage_ShouldUpdatePeakAndAverage()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var session = _resourceMonitor.StartMonitoring(correlationId, _testLimits);

        // Act
        session.UpdateMemoryUsage(100);
        session.UpdateMemoryUsage(200);
        session.UpdateMemoryUsage(150);

        // Assert
        var usage = session.CurrentUsage;
        Assert.Equal(200, usage.PeakMemoryBytes); // Peak should be 200
        Assert.True(usage.AverageMemoryBytes > 0); // Average should be calculated
    }

    [Fact]
    public void ResourceMonitoringSession_ExceedFileCountLimit_ShouldTriggerEvent()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var limitExceededEventFired = false;
        ResourceLimitExceededEventArgs? eventArgs = null;

        _resourceMonitor.ResourceLimitExceeded += (sender, args) =>
        {
            limitExceededEventFired = true;
            eventArgs = args;
        };

        var session = _resourceMonitor.StartMonitoring(correlationId, _testLimits);

        // Act - exceed file count limit (limit is 10)
        for (int i = 0; i < 12; i++)
        {
            session.RecordFileAccess($"/test/file{i}.txt", FileAccessType.Read);
        }

        // Assert
        Assert.True(limitExceededEventFired);
        Assert.NotNull(eventArgs);
        Assert.Equal(correlationId, eventArgs.CorrelationId);
        Assert.Equal("file_count", eventArgs.LimitType);
        Assert.True(eventArgs.CurrentValue > eventArgs.LimitValue);
        Assert.True(session.HasExceededLimits);
        Assert.Contains("file_count", session.ExceededLimits);
    }

    [Fact]
    public void ResourceMonitoringSession_ExceedMemoryLimit_ShouldTriggerEvent()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var limitExceededEventFired = false;
        ResourceLimitExceededEventArgs? eventArgs = null;

        _resourceMonitor.ResourceLimitExceeded += (sender, args) =>
        {
            limitExceededEventFired = true;
            eventArgs = args;
        };

        var session = _resourceMonitor.StartMonitoring(correlationId, _testLimits);

        // Act - exceed memory limit (limit is 1MB = 1,048,576 bytes)
        session.UpdateMemoryUsage(2 * 1024 * 1024); // 2MB

        // Assert
        Assert.True(limitExceededEventFired);
        Assert.NotNull(eventArgs);
        Assert.Equal(correlationId, eventArgs.CorrelationId);
        Assert.Equal("memory", eventArgs.LimitType);
        Assert.True(eventArgs.CurrentValue > eventArgs.LimitValue);
        Assert.True(session.HasExceededLimits);
        Assert.Contains("memory", session.ExceededLimits);
    }

    [Fact]
    public void ResourceMonitoringSession_ExceedFileSizeLimit_ShouldTriggerEvent()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var limitExceededEventFired = false;
        ResourceLimitExceededEventArgs? eventArgs = null;

        _resourceMonitor.ResourceLimitExceeded += (sender, args) =>
        {
            limitExceededEventFired = true;
            eventArgs = args;
        };

        var session = _resourceMonitor.StartMonitoring(correlationId, _testLimits);

        // Act - exceed file size limit (limit is 1MB = 1,048,576 bytes)
        session.RecordFileAccess("/test/largefile.txt", FileAccessType.Write, 0, 2 * 1024 * 1024); // 2MB write

        // Assert
        Assert.True(limitExceededEventFired);
        Assert.NotNull(eventArgs);
        Assert.Equal(correlationId, eventArgs.CorrelationId);
        Assert.Equal("file_size", eventArgs.LimitType);
        Assert.True(eventArgs.CurrentValue > eventArgs.LimitValue);
        Assert.True(session.HasExceededLimits);
        Assert.Contains("file_size", session.ExceededLimits);
    }

    [Fact]
    public void ResourceMonitoringSession_DuplicateFileAccess_ShouldNotDuplicateCount()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var session = _resourceMonitor.StartMonitoring(correlationId, _testLimits);

        // Act - access the same file multiple times
        session.RecordFileAccess("/test/file1.txt", FileAccessType.Read, 100, 0);
        session.RecordFileAccess("/test/file1.txt", FileAccessType.Write, 0, 50);
        session.RecordFileAccess("/test/file2.txt", FileAccessType.Read, 200, 0);

        // Assert
        var usage = session.CurrentUsage;
        Assert.Equal(2, usage.FilesAccessed); // Should count unique files only
        Assert.Equal(300, usage.BytesRead); // But bytes should accumulate
        Assert.Equal(50, usage.BytesWritten);
    }

    [Fact]
    public void ResourceMonitoringSession_Dispose_ShouldPreventFurtherUpdates()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var session = _resourceMonitor.StartMonitoring(correlationId, _testLimits);

        // Act
        session.Dispose();
        session.RecordFileAccess("/test/file.txt", FileAccessType.Read, 100, 0);

        // Assert
        var usage = session.CurrentUsage;
        Assert.Equal(0, usage.FilesAccessed); // Should not update after disposal
        Assert.Equal(0, usage.BytesRead);
    }

    public void Dispose()
    {
        _resourceMonitor.Dispose();
    }
}
