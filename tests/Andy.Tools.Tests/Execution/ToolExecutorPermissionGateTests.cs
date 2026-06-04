using Andy.Tools.Core;
using Andy.Tools.Core.OutputLimiting;
using Andy.Tools.Execution;
using Andy.Tools.Validation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Andy.Tools.Tests.Execution;

public class ToolExecutorPermissionGateTests
{
    private sealed class FakeGate : IToolPermissionGate
    {
        private readonly ToolPermissionVerdict _verdict;
        public int Calls;
        public FakeGate(ToolPermissionVerdict verdict) => _verdict = verdict;
        public Task<ToolPermissionVerdict> CheckAsync(ToolPermissionGateRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(_verdict);
        }
    }

    private static (ToolExecutor Exec, Mock<ITool> Tool) Build(IToolPermissionGate? gate)
    {
        var registry = new Mock<IToolRegistry>();
        var validator = new Mock<IToolValidator>();
        var security = new Mock<ISecurityManager>();
        var monitor = new Mock<IResourceMonitor>();
        var limiter = new Mock<IToolOutputLimiter>();
        var sp = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<ToolExecutor>>();
        var tool = new Mock<ITool>();

        var registration = new ToolRegistration
        {
            Metadata = new ToolMetadata { Id = "demo", Name = "Demo" },
            IsEnabled = true,
        };
        registry.Setup(x => x.GetTool("demo")).Returns(registration);
        registry.Setup(x => x.CreateTool("demo", It.IsAny<IServiceProvider>())).Returns(tool.Object);

        tool.Setup(t => t.InitializeAsync(It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        tool.Setup(t => t.ExecuteAsync(It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(ToolResult.Success("ok"));
        limiter.Setup(x => x.NeedsLimiting(It.IsAny<object?>(), It.IsAny<OutputType>())).Returns(false);
        sp.Setup(x => x.GetService(typeof(IToolPermissionGate))).Returns(gate!);

        var exec = new ToolExecutor(registry.Object, validator.Object, security.Object, monitor.Object, limiter.Object, sp.Object, logger.Object);
        return (exec, tool);
    }

    private static ToolExecutionRequest Req() => new()
    {
        ToolId = "demo",
        Parameters = new Dictionary<string, object?>(),
        Context = new ToolExecutionContext(),
        ValidateParameters = false,
        EnforcePermissions = false,
        EnforceResourceLimits = false,
    };

    [Fact]
    public async Task Gate_deny_blocks_execution_and_reports_violation()
    {
        var gate = new FakeGate(ToolPermissionVerdict.Deny("blocked by policy"));
        var (exec, tool) = Build(gate);
        SecurityViolationEventArgs? violation = null;
        exec.SecurityViolation += (_, e) => violation = e;

        var result = await exec.ExecuteAsync(Req());

        Assert.False(result.IsSuccessful);
        Assert.Contains("blocked by policy", result.ErrorMessage);
        Assert.NotEmpty(result.SecurityViolations);
        Assert.Equal(1, gate.Calls);
        Assert.NotNull(violation);
        tool.Verify(t => t.ExecuteAsync(It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()), Times.Never);
    }

    [Fact]
    public async Task Gate_allow_runs_the_tool()
    {
        var gate = new FakeGate(ToolPermissionVerdict.Allow);
        var (exec, tool) = Build(gate);

        var result = await exec.ExecuteAsync(Req());

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        Assert.Equal(1, gate.Calls);
        tool.Verify(t => t.ExecuteAsync(It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()), Times.Once);
    }

    [Fact]
    public async Task No_gate_registered_is_a_no_op()
    {
        var (exec, tool) = Build(gate: null);

        var result = await exec.ExecuteAsync(Req());

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        tool.Verify(t => t.ExecuteAsync(It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()), Times.Once);
    }
}
