using Andy.MCP.Configuration;
using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Mcp;

/// <summary>
/// Hosted service that connects to all configured MCP servers on startup, discovers their
/// tools, and registers each as an <see cref="ITool"/> in the Andy.Tools <see cref="IToolRegistry"/>.
/// </summary>
public sealed class McpToolRegistrar : IHostedService
{
    private readonly IMcpConnectionManager _connectionManager;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<McpToolRegistrar> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolRegistrar"/> class.
    /// </summary>
    /// <param name="connectionManager">The MCP connection manager.</param>
    /// <param name="toolRegistry">The Andy.Tools registry.</param>
    /// <param name="logger">The logger.</param>
    public McpToolRegistrar(
        IMcpConnectionManager connectionManager,
        IToolRegistry toolRegistry,
        ILogger<McpToolRegistrar> logger)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _connectionManager.ConnectAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MCP servers; no MCP tools will be registered.");
            return;
        }

        IReadOnlyList<(string serverName, Andy.MCP.Protocol.Tool tool)> tools;
        try
        {
            tools = await _connectionManager.ListAllToolsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list tools from MCP servers.");
            return;
        }

        foreach (var (serverName, tool) in tools)
        {
            try
            {
                var metadata = McpToolMetadataMapper.Map(serverName, tool);
                var toolName = tool.Name;

                _toolRegistry.RegisterTool(
                    metadata,
                    sp => new McpTool(metadata, serverName, toolName, sp.GetRequiredService<IMcpToolInvoker>()));

                _logger.LogInformation(
                    "Registered MCP tool '{ToolId}' from server '{Server}'.", metadata.Id, serverName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex, "Failed to register MCP tool '{Tool}' from server '{Server}'.", tool.Name, serverName);
            }
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
