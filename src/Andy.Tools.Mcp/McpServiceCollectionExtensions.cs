using Andy.MCP.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Andy.Tools.Mcp;

/// <summary>
/// DI registration extensions for consuming external MCP servers as Andy.Tools tools.
/// </summary>
public static class McpServiceCollectionExtensions
{
    /// <summary>
    /// Adds MCP client support to Andy.Tools. Connects to the configured MCP servers on startup,
    /// discovers their tools, and registers each as an <c>ITool</c> with id <c>mcp__{server}__{tool}</c>.
    /// Assumes <c>AddAndyTools()</c> has already been called so that <c>IToolRegistry</c> is available.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the MCP client options (servers, timeouts, etc.).</param>
    /// <param name="configureDiscovery">Optional discovery refresh settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpTools(
        this IServiceCollection services,
        Action<McpClientOptions> configure,
        Action<McpToolDiscoveryOptions>? configureDiscovery = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Registers IMcpConnectionManager + McpClientOptions (and Andy.MCP's own hosted service).
        services.AddMcpClient(configure);

        services.TryAddSingleton<IMcpToolInvoker, McpToolInvoker>();
        var discovery = new McpToolDiscoveryOptions();
        configureDiscovery?.Invoke(discovery);
        services.TryAddSingleton(discovery);
        services.TryAddSingleton<McpToolRegistrar>();
        services.AddHostedService(sp => sp.GetRequiredService<McpToolRegistrar>());

        return services;
    }
}
