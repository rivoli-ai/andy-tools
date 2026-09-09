using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
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
    {
        ArgumentNullException.ThrowIfNull(serverName);
        ArgumentNullException.ThrowIfNull(toolName);
        var id = $"mcp__{serverName}__{toolName}";
        // Reserve a separate prefix for hashed names; delimiters inside either component
        // would otherwise alias a different server/tool pair.
        if (id.Length <= 100 && serverName.Length > 0 && toolName.Length > 0
            && !serverName.Contains("__", StringComparison.Ordinal)
            && !toolName.Contains("__", StringComparison.Ordinal)
            && !serverName.StartsWith('_') && !serverName.EndsWith('_')
            && !toolName.StartsWith('_') && !toolName.EndsWith('_')
            && id.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'z' or '_' or '-'))
        {
            return id;
        }

        return "mcp_encoded_" + Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { serverName, toolName }))));
    }

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
            Description = string.IsNullOrWhiteSpace(tool.Description) ? $"MCP tool {tool.Name} on {serverName}" : tool.Description,
            Category = ToolCategory.General,
            RequiredPermissions = ToolPermissionFlags.Network,
            RequiredCapabilities = ToolCapability.Network
                | (tool.Annotations?.DestructiveHint != false ? ToolCapability.Destructive : ToolCapability.None),
            RequiresConfirmation = tool.Annotations?.DestructiveHint != false,
            OutputSchema = tool.OutputSchema?.Clone(),
            Parameters = ParseParameters(tool.InputSchema),
            ParameterValidator = args => Andy.MCP.Server.JsonSchemaValidator.Validate(JsonSerializer.SerializeToElement(args), tool.InputSchema).ToList(),
            Tags = ["mcp", serverName],
            AdditionalMetadata =
            {
                ["mcp_server"] = serverName,
                ["mcp_tool"] = tool.Name,
                ["mcp_input_schema"] = tool.InputSchema.Clone(),
                ["mcp_annotations"] = tool.Annotations,
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
            var parameter = ParseParameter(property.Name, property.Value, required.Contains(property.Name));

            parameters.Add(parameter);
        }

        return parameters;
    }

    private static ToolParameter ParseParameter(string name, JsonElement schema, bool required = false)
    {
        var parameter = new ToolParameter
        {
            Name = name,
            Type = MapType(schema),
            Required = required,
            Schema = schema.Clone(),
        };
        if (schema.ValueKind != JsonValueKind.Object) return parameter;
        if (schema.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String)
            parameter.Description = description.GetString() ?? "";
        if (schema.TryGetProperty("default", out var defaultValue)) parameter.DefaultValue = defaultValue.Clone();
        if (schema.TryGetProperty("enum", out var values) && values.ValueKind == JsonValueKind.Array)
            parameter.AllowedValues = values.EnumerateArray().Select(v => (object)v.Clone()).ToList();
        if (schema.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.String)
            parameter.Format = format.GetString();
        if (schema.TryGetProperty("items", out var items)) parameter.ItemType = ParseParameter("item", items);
        return parameter;
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
