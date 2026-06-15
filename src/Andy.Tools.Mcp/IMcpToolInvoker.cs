using Andy.MCP.Configuration;
using Andy.MCP.Protocol;

namespace Andy.Tools.Mcp;

/// <summary>
/// Abstraction over invoking a tool on an MCP server. This is the test seam for
/// <see cref="McpTool"/> because <c>Andy.MCP.Client.McpClient</c> is sealed and cannot be mocked.
/// </summary>
public interface IMcpToolInvoker
{
    /// <summary>
    /// Invokes a tool on the named MCP server.
    /// </summary>
    /// <param name="serverName">The configured MCP server name.</param>
    /// <param name="toolName">The MCP tool name.</param>
    /// <param name="arguments">The tool arguments, keyed by parameter name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The MCP call result.</returns>
    Task<CallToolResult> InvokeAsync(
        string serverName,
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IMcpToolInvoker"/> that resolves the connected client from the
/// <see cref="IMcpConnectionManager"/> and calls the tool.
/// </summary>
public sealed class McpToolInvoker : IMcpToolInvoker
{
    private readonly IMcpConnectionManager _connectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolInvoker"/> class.
    /// </summary>
    /// <param name="connectionManager">The MCP connection manager.</param>
    public McpToolInvoker(IMcpConnectionManager connectionManager)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    /// <inheritdoc />
    public Task<CallToolResult> InvokeAsync(
        string serverName,
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default)
    {
        var client = _connectionManager.GetClient(serverName)
            ?? throw new InvalidOperationException(
                $"MCP server '{serverName}' is not connected. Cannot invoke tool '{toolName}'.");

        // McpClient.CallToolAsync serializes arguments via System.Text.Json; a plain
        // dictionary serializes to the expected JSON object of arguments.
        object? args = arguments is null
            ? null
            : arguments.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value);

        return client.CallToolAsync(toolName, args, cancellationToken);
    }
}
