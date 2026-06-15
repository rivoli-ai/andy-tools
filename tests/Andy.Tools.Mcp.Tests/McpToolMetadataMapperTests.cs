using System.Text.Json;
using Andy.Tools.Core;
using Andy.Tools.Mcp;
using FluentAssertions;
using McpToolDef = Andy.MCP.Protocol.Tool;

namespace Andy.Tools.Mcp.Tests;

public class McpToolMetadataMapperTests
{
    private static JsonElement Schema(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Map_WithTwoProperties_OneRequired_ProducesExpectedMetadata()
    {
        var schema = Schema(
            """
            {
              "type": "object",
              "properties": {
                "path": { "type": "string", "description": "File path" },
                "count": { "type": "integer", "description": "How many" }
              },
              "required": ["path"]
            }
            """);

        var tool = new McpToolDef
        {
            Name = "read_file",
            Title = "Read File",
            Description = "Reads a file",
            InputSchema = schema,
        };

        var metadata = McpToolMetadataMapper.Map("srv", tool);

        metadata.Id.Should().Be("mcp__srv__read_file");
        metadata.Name.Should().Be("Read File");
        metadata.Description.Should().Be("Reads a file");
        metadata.Category.Should().Be(ToolCategory.General);
        metadata.RequiredPermissions.Should().Be(ToolPermissionFlags.Network);

        metadata.Parameters.Should().HaveCount(2);

        var path = metadata.Parameters.Single(p => p.Name == "path");
        path.Type.Should().Be("string");
        path.Required.Should().BeTrue();
        path.Description.Should().Be("File path");

        var count = metadata.Parameters.Single(p => p.Name == "count");
        count.Type.Should().Be("integer");
        count.Required.Should().BeFalse();
    }

    [Fact]
    public void Map_UsesNameWhenTitleMissing_AndEmptyDescription()
    {
        var tool = new McpToolDef
        {
            Name = "tool_x",
            InputSchema = Schema("{}"),
        };

        var metadata = McpToolMetadataMapper.Map("s", tool);

        metadata.Id.Should().Be("mcp__s__tool_x");
        metadata.Name.Should().Be("tool_x");
        metadata.Description.Should().Be("");
    }

    [Fact]
    public void Map_WithEmptySchema_ProducesNoParameters()
    {
        var tool = new McpToolDef { Name = "n", InputSchema = Schema("{}") };

        var metadata = McpToolMetadataMapper.Map("s", tool);

        metadata.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Map_WithNonObjectSchema_ProducesNoParameters()
    {
        var tool = new McpToolDef { Name = "n", InputSchema = Schema("\"not-an-object\"") };

        var metadata = McpToolMetadataMapper.Map("s", tool);

        metadata.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Map_WithPropertiesNotObject_ProducesNoParameters()
    {
        var tool = new McpToolDef { Name = "n", InputSchema = Schema("""{ "properties": [] }""") };

        var metadata = McpToolMetadataMapper.Map("s", tool);

        metadata.Parameters.Should().BeEmpty();
    }

    [Theory]
    [InlineData("string", "string")]
    [InlineData("number", "number")]
    [InlineData("integer", "integer")]
    [InlineData("boolean", "boolean")]
    [InlineData("array", "array")]
    [InlineData("object", "object")]
    [InlineData("weird-unknown", "string")]
    public void ParseParameters_MapsJsonSchemaTypes(string jsonType, string expected)
    {
        var schema = Schema($$"""{ "type": "object", "properties": { "p": { "type": "{{jsonType}}" } } }""");

        var parameters = McpToolMetadataMapper.ParseParameters(schema);

        parameters.Should().ContainSingle();
        parameters[0].Type.Should().Be(expected);
    }
}
