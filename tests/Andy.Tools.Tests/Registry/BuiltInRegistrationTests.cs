using System.Threading;
using System.Threading.Tasks;
using Andy.Tools;
using Andy.Tools.Core;
using Andy.Tools.Framework;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Andy.Tools.Tests.Registry;

/// <summary>
/// Regression test for issue #71: <c>CopyFileTool</c> (<c>copy_file</c>),
/// <c>DeleteFileTool</c> (<c>delete_file</c>), and <c>TodoManagementTool</c>
/// (<c>todo_management</c>) must be registered by default via <c>AddAndyTools()</c>.
/// </summary>
public sealed class BuiltInRegistrationTests
{
    [Theory]
    [InlineData("copy_file")]
    [InlineData("delete_file")]
    [InlineData("todo_management")]
    public async Task AddAndyTools_RegistersTool_ByDefault(string toolId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAndyTools();

        using var provider = services.BuildServiceProvider();

        // The registry is populated by the lifecycle manager during framework startup.
        var lifecycle = provider.GetRequiredService<IToolLifecycleManager>();
        await lifecycle.InitializeAsync(CancellationToken.None);

        var registry = provider.GetRequiredService<IToolRegistry>();
        registry.GetTool(toolId).Should().NotBeNull(
            "tool '{0}' should be registered by default", toolId);
    }
}
