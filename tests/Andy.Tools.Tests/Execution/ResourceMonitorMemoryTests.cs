using Andy.Tools.Core;
using Andy.Tools.Execution;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Andy.Tools.Tests.Execution;

/// <summary>
/// Regression tests for issue #26: memory was reported as the whole-process working set (so a fresh
/// session immediately showed huge usage and tripped tiny limits), and IResourceMonitor was not
/// IDisposable so the container never disposed the polling timer.
/// </summary>
public class ResourceMonitorMemoryTests
{
    [Fact]
    public void NewSession_ReportsBaselineRelativeMemory_NotWholeProcess()
    {
        using var monitor = new ResourceMonitor(NullLogger<ResourceMonitor>.Instance);
        var session = monitor.StartMonitoring("c1", new ToolResourceLimits { MaxMemoryBytes = 64L * 1024 * 1024 });

        // A just-started session that has allocated nothing must not report the entire process working
        // set (hundreds of MB). Feed the current process memory as the sampler would.
        using var proc = global::System.Diagnostics.Process.GetCurrentProcess();
        proc.Refresh();
        session.UpdateMemoryUsage(proc.WorkingSet64);

        // Delta from baseline should be small — definitely far below the full working set.
        session.CurrentUsage.PeakMemoryBytes.Should().BeLessThan(proc.WorkingSet64 / 2);
    }

    [Fact]
    public void ResourceMonitor_IsDisposableThroughInterface()
    {
        IResourceMonitor monitor = new ResourceMonitor(NullLogger<ResourceMonitor>.Instance);
        // IResourceMonitor now extends IDisposable, so the DI container can dispose the timer.
        monitor.Should().BeAssignableTo<IDisposable>();
        monitor.Dispose();
    }
}
