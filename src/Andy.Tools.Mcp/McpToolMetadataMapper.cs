using System.Text.Json;
using Andy.Tools.Core;
using McpToolDef = Andy.MCP.Protocol.Tool;

namespace Andy.Tools.Mcp;

/// <summary>
/// Maps an MCP <see cref="McpToolDef"/> definition (discovered from a server)
/// into Andy.Tools <see cref="ToolMetadata"/>.
/// </summary>
public static class McpToolMetadataMapper
{
    /// <summary>
    /// Builds the deterministic Andy.Tools tool id for an MCP tool.
    /// Format: <c>mcp__{serverName}__{toolName}</c>.
    /// </summary>
    /// <param name="serverName">The configured MCP server name.</param>
    /// <param name="toolName">The MCP tool name.</param>
    /// <returns>The Andy.Tools tool id.</returns>
    public static string BuildId(string serverName, string toolName)
        => $"mcp__{serverName}__{toolName}";

    /// <summary>
    /// Maps an MCP tool to Andy.Tools metadata.
    /// </summary>
    /// <param name="serverName">The configured MCP server name.</param>
    /// <param name="tool">The MCP tool definition.</param>
    /// <returns>The mapped <see cref="ToolMetadata"/>.</returns>
    public static ToolMetadata Map(string serverName, McpToolDef tool)
    {
        ArgumentNullException.ThrowIfNull(serverName);
        ArgumentNullException.ThrowIfNull(tool);

        return new ToolMetadata
        {
            Id = BuildId(serverName, tool.Name),
            Name = tool.Title ?? tool.Name,
            Description = tool.Description ?? "",
            Category = ToolCategory.General,
            RequiredPermissions = ToolPermissionFlags.Network,
            Parameters = ParseParameters(tool.InputSchema),
            Tags = ["mcp", serverName],
            AdditionalMetadata =
            {
                ["mcp_server"] = serverName,
                ["mcp_tool"] = tool.Name,
            },
        };
    }

    /// <summary>
    /// Parses a JSON-Schema object (the MCP tool input schema) into Andy.Tools parameters.
    /// Defensive: if the schema is missing <c>properties</c> or is not an object, returns an empty list.
    /// </summary>
    /// <param name="inputSchema">The MCP tool input schema element.</param>
    /// <returns>The parsed parameters.</returns>
    public static IList<ToolParameter> ParseParameters(JsonElement inputSchema)
    {
        var parameters = new List<ToolParameter>();

        if (inputSchema.ValueKind != JsonValueKind.Object)
        {
            return parameters;
        }

        if (!inputSchema.TryGetProperty("properties", out var properties)
            || properties.ValueKind != JsonValueKind.Object)
        {
            return parameters;
        }

        var required = new HashSet<string>(StringComparer.Ordinal);
        if (inputSchema.TryGetProperty("required", out var requiredElement)
            && requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in requiredElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var name = item.GetString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        required.Add(name);
                    }
                }
            }
        }

        foreach (var property in properties.EnumerateObject())
        {
            var schema = property.Value;
            var parameter = new ToolParameter
            {
                Name = property.Name,
                Type = MapType(schema),
                Required = required.Contains(property.Name),
            };

            if (schema.ValueKind == JsonValueKind.Object
                && schema.TryGetProperty("description", out var description)
                && description.ValueKind == JsonValueKind.String)
            {
                parameter.Description = description.GetString() ?? "";
            }

            parameters.Add(parameter);
        }

        return parameters;
    }

    private static string MapType(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("type", out var typeElement)
            || typeElement.ValueKind != JsonValueKind.String)
        {
            return "string";
        }

        return typeElement.GetString() switch
        {
            "string" => "string",
            "number" => "number",
            "integer" => "integer",
            "boolean" => "boolean",
            "array" => "array",
            "object" => "object",
            _ => "string",
        };
    }
}
