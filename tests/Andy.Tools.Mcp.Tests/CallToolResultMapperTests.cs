using System.Text.Json;
using Andy.MCP.Protocol;
using Andy.Tools.Mcp;
using FluentAssertions;

namespace Andy.Tools.Mcp.Tests;

public class CallToolResultMapperTests
{
    private static JsonElement Element(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Map_TextResult_ReturnsSuccessWithJoinedText()
    {
        var result = new CallToolResult
        {
            Content = [new TextContent { Text = "hello" }, new TextContent { Text = "world" }],
        };

        var mapped = CallToolResultMapper.Map(result);

        mapped.IsSuccessful.Should().BeTrue();
        mapped.Data.Should().Be("hello\nworld");
    }

    [Fact]
    public void Map_IsError_ReturnsFailureWithText()
    {
        var result = new CallToolResult
        {
            Content = [new TextContent { Text = "boom" }],
            IsError = true,
        };

        var mapped = CallToolResultMapper.Map(result);

        mapped.IsSuccessful.Should().BeFalse();
        mapped.ErrorMessage.Should().Be("boom");
    }

    [Fact]
    public void Map_IsError_NoText_ReturnsDefaultErrorMessage()
    {
        var result = new CallToolResult { Content = [], IsError = true };

        var mapped = CallToolResultMapper.Map(result);

        mapped.IsSuccessful.Should().BeFalse();
        mapped.ErrorMessage.Should().Be("MCP tool error");
    }

    [Fact]
    public void Map_StructuredContent_PreferredAsData()
    {
        var result = new CallToolResult
        {
            Content = [new TextContent { Text = "ignored fallback" }],
            StructuredContent = Element("""{ "answer": 42 }"""),
        };

        var mapped = CallToolResultMapper.Map(result);

        mapped.IsSuccessful.Should().BeTrue();
        mapped.Data.Should().NotBeNull();
        mapped.Data!.GetType().Name.Should().NotBe("String");
    }

    [Fact]
    public void Map_StructuredScalar_DeserializedToValue()
    {
        var result = new CallToolResult { StructuredContent = Element("\"just-a-string\"") };

        var mapped = CallToolResultMapper.Map(result);

        mapped.Data.Should().Be("just-a-string");
    }

    [Fact]
    public void Map_ImageContent_SurfacedInMetadata()
    {
        var result = new CallToolResult
        {
            Content =
            [
                new TextContent { Text = "see image" },
                new ImageContent { Data = "QUJD", MimeType = "image/png" },
            ],
        };

        var mapped = CallToolResultMapper.Map(result);

        mapped.IsSuccessful.Should().BeTrue();
        mapped.Data.Should().Be("see image");
        mapped.Metadata.Should().ContainKey("media");

        var media = (List<Dictionary<string, object?>>)mapped.Metadata["media"]!;
        media.Should().ContainSingle();
        media[0]["type"].Should().Be("image");
        media[0]["data"].Should().Be("QUJD");
        media[0]["mimeType"].Should().Be("image/png");
    }
}
