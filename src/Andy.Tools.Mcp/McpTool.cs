using Andy.Tools.Core;
using Andy.Tools.Library;

namespace Andy.Tools.Mcp;

/// <summary>
/// An Andy.Tools <see cref="ITool"/> that proxies execution to a tool on an external MCP server.
/// </summary>
public sealed class McpTool : ToolBase
{
    private readonly ToolMetadata _metadata;
    private readonly string _serverName;
    private readonly string _toolName;
    private readonly IMcpToolInvoker _invoker;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpTool"/> class.
    /// </summary>
    /// <param name="metadata">The prebuilt metadata for this tool.</param>
    /// <param name="serverName">The configured MCP server name.</param>
    /// <param name="toolName">The MCP tool name on the server.</param>
    /// <param name="invoker">The invoker used to call the MCP tool.</param>
    public McpTool(ToolMetadata metadata, string serverName, string toolName, IMcpToolInvoker invoker)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _serverName = serverName ?? throw new ArgumentNullException(nameof(serverName));
        _toolName = toolName ?? throw new ArgumentNullException(nameof(toolName));
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
    }

    /// <inheritdoc />
    public override ToolMetadata Metadata => _metadata;

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var result = await _invoker.InvokeAsync(
            _serverName,
            _toolName,
            parameters,
            context.CancellationToken).ConfigureAwait(false);

        return CallToolResultMapper.Map(result);
    }
}
