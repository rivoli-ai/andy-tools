using Andy.Tools.Core;
using System.Diagnostics;
using System.Text.Json;
using Andy.MCP.Server;
using Andy.Tools.Library;

namespace Andy.Tools.Mcp;

/// <summary>
/// An Andy.Tools <see cref="ITool"/> that proxies execution to a tool on an external MCP server.
/// </summary>
public sealed class McpTool : ToolBase, ITool
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

    /// <summary>Executes remotely and preserves cancellation for the executor's cancellation statistics.</summary>
    public new async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        var started = Stopwatch.GetTimestamp();
        var result = await base.ExecuteAsync(parameters, context).ConfigureAwait(false);
        result.DurationMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        context.CancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <inheritdoc />
    public override IList<string> ValidateParameters(Dictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (Metadata.AdditionalMetadata.TryGetValue("mcp_input_schema", out var schema)
            && schema is JsonElement element)
        {
            return JsonSchemaValidator.Validate(JsonSerializer.SerializeToElement(parameters), element).ToList();
        }
        return base.ValidateParameters(parameters);
    }

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
