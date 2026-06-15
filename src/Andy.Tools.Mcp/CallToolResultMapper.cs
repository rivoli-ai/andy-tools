using System.Text.Json;
using Andy.MCP.Protocol;
using Andy.Tools.Core;

namespace Andy.Tools.Mcp;

/// <summary>
/// Maps an MCP <see cref="CallToolResult"/> into an Andy.Tools <see cref="ToolResult"/>.
/// </summary>
public static class CallToolResultMapper
{
    /// <summary>
    /// Maps the result of an MCP tool call to an Andy.Tools result.
    /// </summary>
    /// <param name="result">The MCP call result.</param>
    /// <returns>The mapped <see cref="ToolResult"/>.</returns>
    public static ToolResult Map(CallToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsError == true)
        {
            var errorText = JoinText(result.Content);
            return ToolResult.Failure(
                string.IsNullOrEmpty(errorText) ? "MCP tool error" : errorText);
        }

        var metadata = ExtractBinaryContent(result.Content);

        // Prefer structured content as the data payload when present.
        if (result.StructuredContent is { } structured)
        {
            return ToolResult.Success(DeserializeStructured(structured), metadata);
        }

        return ToolResult.Success(JoinText(result.Content), metadata);
    }

    private static string JoinText(IReadOnlyList<Content>? content)
    {
        if (content is null || content.Count == 0)
        {
            return "";
        }

        var texts = content.OfType<TextContent>().Select(static c => c.Text);
        return string.Join("\n", texts);
    }

    private static Dictionary<string, object?> ExtractBinaryContent(IReadOnlyList<Content>? content)
    {
        var metadata = new Dictionary<string, object?>();
        if (content is null || content.Count == 0)
        {
            return metadata;
        }

        var media = new List<Dictionary<string, object?>>();
        foreach (var block in content)
        {
            switch (block)
            {
                case ImageContent image:
                    media.Add(new Dictionary<string, object?>
                    {
                        ["type"] = "image",
                        ["data"] = image.Data,
                        ["mimeType"] = image.MimeType,
                    });
                    break;
                case AudioContent audio:
                    media.Add(new Dictionary<string, object?>
                    {
                        ["type"] = "audio",
                        ["data"] = audio.Data,
                        ["mimeType"] = audio.MimeType,
                    });
                    break;
            }
        }

        if (media.Count > 0)
        {
            metadata["media"] = media;
        }

        return metadata;
    }

    private static object? DeserializeStructured(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            _ => JsonSerializer.Deserialize<object>(element.GetRawText()),
        };
    }
}
