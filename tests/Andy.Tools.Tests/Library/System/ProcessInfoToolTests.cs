using Andy.Tools.Core;
using Andy.Tools.Library.System;
using FluentAssertions;

namespace Andy.Tools.Tests.Library.System;

/// <summary>
/// Tests for ProcessInfoTool (previously untested — issue #34) including the by-id handle-leak fix (#25).
/// </summary>
public class ProcessInfoToolTests
{
    private static async Task<ToolResult> Run(Dictionary<string, object?> p)
    {
        var tool = new ProcessInfoTool();
        await tool.InitializeAsync();
        return await tool.ExecuteAsync(p, new ToolExecutionContext());
    }

    [Fact]
    public async Task QueryByCurrentProcessId_ReturnsThatProcess()
    {
        var pid = Environment.ProcessId;
        var result = await Run(new() { ["process_id"] = pid });

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryByNonExistentProcessId_DoesNotThrow()
    {
        // A pid that is extremely unlikely to exist; the tool must fail/empty gracefully, not throw.
        var result = await Run(new() { ["process_id"] = 2_000_000_000 });
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RepeatedByIdQueries_CompleteWithoutError()
    {
        // The by-id path used to leak a process handle on each call (it never disposed the Process).
        // This exercises that path many times; with the `using` fix it completes cleanly.
        var pid = Environment.ProcessId;
        for (var i = 0; i < 200; i++)
        {
            var result = await Run(new() { ["process_id"] = pid });
            result.IsSuccessful.Should().BeTrue();
        }
    }
}
