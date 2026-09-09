using System.Text.Json;
using Andy.MCP.Protocol;
using Andy.Tools.Core;
using Andy.Tools.Mcp;
using Andy.Tools.Validation;
using Moq;

namespace Andy.Tools.Mcp.Tests;

public class SharedAdapterTests
{
    private static JsonElement Json(string value) => JsonDocument.Parse(value).RootElement.Clone();

    [Theory]
    [InlineData("srv", "tool.name")]
    [InlineData("srv", "Tool")]
    [InlineData("srv__a", "b")]
    [InlineData("srv", "tool/with spaces")]
    public void NamesAreValidAndDoNotAlias(string server, string tool)
    {
        var metadata = McpToolMetadataMapper.Map(server, new Tool { Name = tool, InputSchema = Json("{}") });
        Assert.True(new ToolValidator().ValidateMetadata(metadata).IsValid);
        Assert.NotEqual(McpToolMetadataMapper.BuildId(server, tool.ToLowerInvariant() + "_"), metadata.Id);
        Assert.Equal(metadata.Id, McpToolMetadataMapper.BuildId(server, tool));
        Assert.NotEqual(McpToolMetadataMapper.BuildId("a_", "b"), McpToolMetadataMapper.BuildId("a", "_b"));
        Assert.NotEqual(McpToolMetadataMapper.BuildId("A", "b"), McpToolMetadataMapper.BuildId("a", "b"));
        Assert.True(McpToolMetadataMapper.BuildId(new string('s', 150), tool).Length <= 100);
    }

    [Fact]
    public void MapsNestedSchemasEnumsDefaultsAndUntrustedHints()
    {
        var schema = Json("""{"type":"object","properties":{"items":{"type":"array","items":{"type":"object","properties":{"mode":{"enum":["fast","safe"],"default":"safe"}}}},"mode":{"type":"string","enum":["a","b"],"default":"a"}},"required":["items"]}""");
        var definition = new Tool { Name = "tool", InputSchema = schema, OutputSchema = Json("""{"type":"object"}"""), Annotations = new() { ReadOnlyHint = true, OpenWorldHint = false } };
        var metadata = McpToolMetadataMapper.Map("srv", definition);
        Assert.True(metadata.Parameters[0].Required);
        Assert.Equal("object", metadata.Parameters[0].ItemType!.Type);
        Assert.Equal(2, metadata.Parameters[1].AllowedValues!.Count);
        Assert.Equal("a", ((JsonElement)metadata.Parameters[1].DefaultValue!).GetString());
        Assert.Equal(schema.GetRawText(), ((JsonElement)metadata.AdditionalMetadata["mcp_input_schema"]!).GetRawText());
        Assert.NotNull(metadata.OutputSchema);
        Assert.True(metadata.RequiresConfirmation);
        Assert.True(metadata.RequiredCapabilities.HasFlag(ToolCapability.Network));
        Assert.Equal(ToolPermissionFlags.Network, metadata.RequiredPermissions);
    }

    [Theory]
    [InlineData("""{"options":{"count":0}}""", false)]
    [InlineData("""{"options":{"count":2},"extra":true}""", false)]
    [InlineData("""{"options":null}""", false)]
    [InlineData("""{"options":{"count":2}}""", true)]
    public async Task ValidatesFullSchemaBeforeRemoteExecution(string arguments, bool valid)
    {
        var schema = Json("""{"type":"object","$defs":{"options":{"type":"object","properties":{"count":{"type":"integer","minimum":1}},"required":["count"]}},"properties":{"options":{"$ref":"#/$defs/options"}},"required":["options"],"additionalProperties":false}""");
        var metadata = McpToolMetadataMapper.Map("srv", new Tool { Name = "tool", InputSchema = schema });
        var invoker = new Mock<IMcpToolInvoker>();
        invoker.Setup(x => x.InvokeAsync("srv", "tool", It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>())).ReturnsAsync(CallToolResult.Text("ok"));
        var tool = new McpTool(metadata, "srv", "tool", invoker.Object);
        await tool.InitializeAsync();
        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments)!;
        var result = await tool.ExecuteAsync(args, new ToolExecutionContext { Permissions = new() { NetworkAccess = true } });
        Assert.Equal(valid, result.IsSuccessful);
        invoker.Verify(x => x.InvokeAsync("srv", "tool", It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()), valid ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesFullResultIncludingErrorMediaAndStructuredContent(bool error)
    {
        var result = new CallToolResult { IsError = error, StructuredContent = Json("""{"status":"detail"}"""), Content = [new TextContent { Text = "detail" }, new ImageContent { Data = "eA==", MimeType = "image/png" }] };
        var mapped = CallToolResultMapper.Map(result);
        Assert.Equal(!error, mapped.IsSuccessful);
        Assert.Same(result, mapped.Metadata["mcp_result"]);
        Assert.True(mapped.Metadata.ContainsKey("media"));
    }
}
