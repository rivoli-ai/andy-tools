using Andy.Tools.Core;
using Andy.Tools.Core.OutputLimiting;
using Andy.Tools.Execution;
using Andy.Tools.Library;
using Andy.Tools.Library.FileSystem;
using Andy.Tools.Registry;
using Andy.Tools.Validation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Andy.Tools.Tests.Integration;

/// <summary>
/// Integration tests for issue #34: exercise the real ToolExecutor pipeline (real SecurityManager,
/// ResourceMonitor, ToolValidator, OutputLimiter, real tools) to prove enforcement actually works,
/// rather than asserting against mocks.
/// </summary>
public sealed class EnforcementIntegrationTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IToolExecutor _executor;
    private readonly IToolRegistry _registry;
    private readonly string _root;

    public EnforcementIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"andy_enf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        var services = new ServiceCollection();
        services.AddSingleton<IToolValidator, ToolValidator>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<ISecurityManager, SecurityManager>();
        services.AddSingleton<IResourceMonitor, ResourceMonitor>();
        services.AddSingleton<IToolOutputLimiter, ToolOutputLimiter>();
        services.AddSingleton<IToolExecutor, ToolExecutor>();
        services.AddLogging();
        services.Configure<ToolOutputLimiterOptions>(_ => { });

        _provider = services.BuildServiceProvider();
        _executor = _provider.GetRequiredService<IToolExecutor>();
        _registry = _provider.GetRequiredService<IToolRegistry>();

        _registry.RegisterTool(typeof(WriteFileTool));
        _registry.RegisterTool(typeof(SlowTool));
    }

    [Fact]
    public async Task Write_OutsideAllowedPaths_IsBlockedByTheRealPipeline()
    {
        var allowed = Path.Combine(_root, "allowed");
        Directory.CreateDirectory(allowed);
        var outside = Path.Combine(_root, "outside.txt");

        var request = new ToolExecutionRequest
        {
            ToolId = "write_file",
            Parameters = new Dictionary<string, object?> { ["file_path"] = outside, ["content"] = "x" },
            Context = new ToolExecutionContext
            {
                WorkingDirectory = _root,
                Permissions = new ToolPermissions
                {
                    FileSystemAccess = true,
                    AllowedPaths = new HashSet<string> { allowed }
                }
            }
        };

        var result = await _executor.ExecuteAsync(request);

        result.IsSuccessful.Should().BeFalse();
        File.Exists(outside).Should().BeFalse("the write must be blocked before touching disk");
    }

    [Fact]
    public async Task Execution_ExceedingTimeout_IsCancelledByTheRealPipeline()
    {
        var request = new ToolExecutionRequest
        {
            ToolId = "slow_tool",
            Parameters = new Dictionary<string, object?>(),
            Context = new ToolExecutionContext(),
            TimeoutMs = 150
        };

        var result = await _executor.ExecuteAsync(request);

        // The tool sleeps 30s but the 150ms timeout must cut it short. ToolBase surfaces cancellation as
        // an unsuccessful result with a "cancelled" message, so assert the observable outcome.
        result.IsSuccessful.Should().BeFalse();
        (result.ErrorMessage ?? string.Empty).ToLowerInvariant().Should().Contain("cancel");
        (result.DurationMs ?? 0).Should().BeLessThan(10_000, "execution must be cut short, not run the full 30s");
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { Directory.Delete(_root, true); } catch { }
    }

    /// <summary>A tool that runs longer than the configured timeout and honors cancellation.</summary>
    private sealed class SlowTool : ToolBase
    {
        public override ToolMetadata Metadata { get; } = new()
        {
            Id = "slow_tool",
            Name = "Slow Tool",
            Description = "Sleeps to exercise timeout cancellation",
            Version = "1.0.0",
            Category = ToolCategory.Utility
        };

        protected override async Task<ToolResult> ExecuteInternalAsync(
            Dictionary<string, object?> parameters, ToolExecutionContext context)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), context.CancellationToken);
            return ToolResult.Success("done");
        }
    }
}
