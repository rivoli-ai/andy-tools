using Andy.MCP.Protocol;
using Andy.Tools.Core;
using Andy.Tools.Mcp;
using FluentAssertions;
using Moq;

namespace Andy.Tools.Mcp.Tests;

public class McpToolTests
{
    private static ToolMetadata BuildMetadata() => new()
    {
        Id = "mcp__srv__echo",
        Name = "echo",
        Description = "Echoes",
        Category = ToolCategory.General,
        RequiredPermissions = ToolPermissionFlags.Network,
    };

    private static ToolExecutionContext NetworkContext() => new()
    {
        Permissions = new ToolPermissions { NetworkAccess = true },
        CancellationToken = CancellationToken.None,
    };

    [Fact]
    public async Task ExecuteAsync_InvokesInvokerWithServerToolAndArgs_AndMapsResult()
    {
        var invoker = new Mock<IMcpToolInvoker>(MockBehavior.Strict);
        invoker
            .Setup(x => x.InvokeAsync(
                "srv",
                "echo",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallToolResult { Content = [new TextContent { Text = "pong" }] });

        var tool = new McpTool(BuildMetadata(), "srv", "echo", invoker.Object);
        await tool.InitializeAsync();

        var parameters = new Dictionary<string, object?> { ["message"] = "ping" };
        var result = await tool.ExecuteAsync(parameters, NetworkContext());

        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().Be("pong");

        invoker.Verify(
            x => x.InvokeAsync(
                "srv",
                "echo",
                It.Is<IReadOnlyDictionary<string, object?>>(d =>
                    d.ContainsKey("message") && (string)d["message"]! == "ping"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvokerReturnsError_MapsToFailure()
    {
        var invoker = new Mock<IMcpToolInvoker>();
        invoker
            .Setup(x => x.InvokeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallToolResult
            {
                Content = [new TextContent { Text = "nope" }],
                IsError = true,
            });

        var tool = new McpTool(BuildMetadata(), "srv", "echo", invoker.Object);
        await tool.InitializeAsync();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>(), NetworkContext());

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("nope");
    }

    [Fact]
    public void Metadata_ReturnsProvidedMetadata()
    {
        var metadata = BuildMetadata();
        var tool = new McpTool(metadata, "srv", "echo", Mock.Of<IMcpToolInvoker>());

        tool.Metadata.Should().BeSameAs(metadata);
    }
}
