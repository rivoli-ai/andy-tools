using System.Collections.Concurrent;
using System.Text.Json;
using Andy.MCP.Client;
using Andy.MCP.Configuration;
using Andy.MCP.Protocol;
using Andy.MCP.Server;
using Andy.Tools.Core;
using Andy.Tools.Mcp;
using Andy.Tools.Registry;
using Andy.Tools.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Andy.Tools.Mcp.Tests;

public class McpToolRegistrarTests
{
    [Fact]
    public async Task ReconcilesNotificationsDisconnectReconnectAndExecutesThroughRegistry()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var (transport, serverTransport) = InMemoryTransport.CreatePair();
        await using var server = new McpServer(serverTransport);
        server.AddTool("echo", "Echo", (JsonElement? _, CancellationToken _) => Task.FromResult(CallToolResult.Text("pong")));
        server.AddTool("second", "Second", (JsonElement? _, CancellationToken _) => Task.FromResult(CallToolResult.Text("second")));
        var showSecond = false;
        serverTransport.Filter = message => message is JsonRpcResponse { Result: { } result } response
            && result.TryGetProperty("tools", out var tools) && !Volatile.Read(ref showSecond)
            ? response with { Result = JsonSerializer.SerializeToElement(new { tools = tools.EnumerateArray().Take(1).ToArray() }) }
            : message;
        var running = server.RunAsync(timeout.Token);
        await using var client = await McpClient.ConnectAsync(transport, cancellationToken: timeout.Token);
        var clients = new ConcurrentDictionary<string, McpClient>();
        clients["srv"] = client;
        var manager = new Mock<IMcpConnectionManager>();
        manager.SetupGet(m => m.ConnectedServers).Returns(() => clients.Keys.ToArray());
        manager.Setup(m => m.GetClient(It.IsAny<string>())).Returns((string name) => clients.GetValueOrDefault(name));
        var registry = new ToolRegistry(new ToolValidator(), NullLogger<ToolRegistry>.Instance);
        using var registrar = new McpToolRegistrar(manager.Object, registry, NullLogger<McpToolRegistrar>.Instance,
            new McpToolDiscoveryOptions { RefreshInterval = TimeSpan.FromMilliseconds(25) });
        await registrar.StartAsync(timeout.Token);
        Assert.Single(registry.Tools);
        manager.Verify(m => m.ConnectAllAsync(It.IsAny<CancellationToken>()), Times.Never());
        var initial = registry.Tools[0];
        await registrar.RefreshToolsAsync(timeout.Token);
        Assert.Same(initial, registry.Tools[0]);
        using var services = new ServiceCollection().AddSingleton<IMcpToolInvoker>(new McpToolInvoker(manager.Object)).BuildServiceProvider();
        var tool = registry.CreateTool(initial.Metadata.Id, services)!;
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync([], new ToolExecutionContext { Permissions = new() { NetworkAccess = true } });
        Assert.Equal("pong", result.Data);
        Assert.Single(registry.SearchTools("echo"));
        Assert.Single(registry.GetTools(capabilities: ToolCapability.Network, tags: ["mcp"]));
        Volatile.Write(ref showSecond, true);
        await server.NotifyToolsChangedAsync();
        await UntilAsync(() => registry.Tools.Count == 2, timeout.Token);
        transport.SimulateDisconnect();
        await UntilAsync(() => registry.Tools.Count == 0, timeout.Token);
        clients.TryRemove("srv", out _);
        await registrar.RefreshToolsAsync(timeout.Token);
        var (replacementTransport, replacementServerTransport) = InMemoryTransport.CreatePair();
        await using var replacementServer = new McpServer(replacementServerTransport);
        replacementServer.AddTool("replacement", "Replacement", (JsonElement? _, CancellationToken _) => Task.FromResult(CallToolResult.Text("new")));
        var replacementRunning = replacementServer.RunAsync(timeout.Token);
        await using var replacement = await McpClient.ConnectAsync(replacementTransport, cancellationToken: timeout.Token);
        clients["srv"] = replacement;
        await UntilAsync(() => registry.GetTool("mcp__srv__replacement") is not null, timeout.Token);
        await registrar.StopAsync(timeout.Token);
        Assert.Empty(registry.Tools);
        await timeout.CancelAsync();
        try { await Task.WhenAll(running, replacementRunning); } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task DiRegistersOneConnectionHostAndOnePublicRegistrar()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IToolRegistry>(new ToolRegistry(new ToolValidator(), NullLogger<ToolRegistry>.Instance));
        services.AddMcpTools(_ => { });
        services.AddMcpTools(_ => { });
        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().ToArray();
        Assert.Equal(2, hosted.Length);
        Assert.Same(provider.GetRequiredService<McpToolRegistrar>(), Assert.Single(hosted.OfType<McpToolRegistrar>()));
    }

    [Fact]
    public async Task StandardExecutorValidatesSchemaTracksAndCancelsRemoteCalls()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var (transport, serverTransport) = InMemoryTransport.CreatePair();
        await using var server = new McpServer(serverTransport);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var schema = JsonDocument.Parse("""{"type":"object","properties":{"wait":{"type":"boolean"}},"required":["wait"],"additionalProperties":false}""").RootElement.Clone();
        server.AddTool("work", "Work", schema, async (JsonElement? args, CancellationToken ct) =>
        {
            if (args!.Value.GetProperty("wait").GetBoolean())
            {
                entered.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
                catch (OperationCanceledException) { cancelled.TrySetResult(); throw; }
            }
            await Task.Delay(20, ct);
            return CallToolResult.Text("done");
        });
        var running = server.RunAsync(timeout.Token);
        await using var client = await McpClient.ConnectAsync(transport, cancellationToken: timeout.Token);
        var manager = new Mock<IMcpConnectionManager>();
        manager.SetupGet(m => m.ConnectedServers).Returns(["srv"]);
        manager.Setup(m => m.GetClient("srv")).Returns(client);
        var services = new ServiceCollection().AddLogging();
        services.AddAndyTools(options => { options.RegisterBuiltInTools = false; options.EnableObservability = false; });
        services.AddSingleton<IMcpToolInvoker>(new McpToolInvoker(manager.Object));
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IToolRegistry>();
        using var registrar = new McpToolRegistrar(manager.Object, registry, NullLogger<McpToolRegistrar>.Instance);
        await registrar.StartAsync(timeout.Token);
        var executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionRequest Request(bool wait) => new()
        {
            ToolId = "mcp__srv__work",
            Parameters = JsonSerializer.Deserialize<Dictionary<string, object?>>($$"""{"wait":{{wait.ToString().ToLowerInvariant()}}}""")!,
            Context = new() { Permissions = new() { NetworkAccess = true, CustomPermissions = new() { ["allow_destructive"] = true } } },
            EnforceResourceLimits = true,
        };
        var invalid = Request(false);
        invalid.Parameters["extra"] = true;
        Assert.False((await executor.ExecuteAsync(invalid)).IsSuccessful);
        var success = await executor.ExecuteAsync(Request(false));
        Assert.True(success.IsSuccessful, success.ErrorMessage);
        Assert.Equal("done", success.Data);
        Assert.True(success.DurationMs >= 10);
        Assert.True(executor.GetStatistics().AverageExecutionTimeMs >= 10);
        Assert.Equal(1, executor.GetStatistics().SuccessfulExecutions);
        Assert.IsType<CallToolResult>(success.Metadata["mcp_result"]);
        var request = Request(true);
        request.Context.CorrelationId = "cancel-mcp";
        var pending = executor.ExecuteAsync(request);
        await entered.Task.WaitAsync(timeout.Token);
        Assert.Single(executor.GetRunningExecutions());
        Assert.Equal(1, await executor.CancelExecutionsAsync("cancel-mcp"));
        Assert.True((await pending.WaitAsync(timeout.Token)).WasCancelled);
        await cancelled.Task.WaitAsync(timeout.Token);
        Assert.Empty(executor.GetRunningExecutions());
        Assert.Equal(1, executor.GetStatistics().CancelledExecutions);
        var timed = Request(true);
        timed.TimeoutMs = 50;
        Assert.True((await executor.ExecuteAsync(timed)).WasCancelled);
        await registrar.StopAsync(timeout.Token);
        await timeout.CancelAsync();
        try { await running; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task MultipleServersRouteCollisionsAndIsolateDiscoveryFailureWithRegistryEvents()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var clients = new ConcurrentDictionary<string, McpClient>();
        var servers = new List<McpServer>();
        var runs = new List<Task>();
        var failFirst = false;
        foreach (var name in new[] { "first", "second" })
        {
            var (transport, serverTransport) = InMemoryTransport.CreatePair();
            if (name == "first") serverTransport.Filter = message => message is JsonRpcResponse { Result: { } result } response
                && result.TryGetProperty("tools", out _) && Volatile.Read(ref failFirst)
                ? new JsonRpcResponse { Id = response.Id, Error = new JsonRpcError { Code = -32603, Message = "discovery failed" } } : message;
            var server = new McpServer(serverTransport);
            server.AddTool("echo", "Echo", (_, _) => Task.FromResult(CallToolResult.Text(name)));
            servers.Add(server);
            runs.Add(server.RunAsync(timeout.Token));
            clients[name] = await McpClient.ConnectAsync(transport, cancellationToken: timeout.Token);
        }
        var manager = new Mock<IMcpConnectionManager>();
        manager.SetupGet(m => m.ConnectedServers).Returns(() => clients.Keys.ToArray());
        manager.Setup(m => m.GetClient(It.IsAny<string>())).Returns((string name) => clients.GetValueOrDefault(name));
        var registry = new ToolRegistry(new ToolValidator(), NullLogger<ToolRegistry>.Instance);
        var added = 0;
        var removed = 0;
        registry.ToolRegistered += (_, _) => Interlocked.Increment(ref added);
        registry.ToolUnregistered += (_, _) => Interlocked.Increment(ref removed);
        using var registrar = new McpToolRegistrar(manager.Object, registry, NullLogger<McpToolRegistrar>.Instance);
        await registrar.StartAsync(timeout.Token);
        Assert.Equal(2, added);
        using var services = new ServiceCollection().AddSingleton<IMcpToolInvoker>(new McpToolInvoker(manager.Object)).BuildServiceProvider();
        foreach (var name in clients.Keys)
        {
            var tool = registry.CreateTool($"mcp__{name}__echo", services)!;
            await tool.InitializeAsync();
            Assert.Equal(name, (await tool.ExecuteAsync([], new() { Permissions = new() { NetworkAccess = true } })).Data);
        }
        Volatile.Write(ref failFirst, true);
        await registrar.RefreshToolsAsync(timeout.Token);
        Assert.Equal(2, registry.Tools.Count);
        Assert.Equal(2, added);
        clients.TryRemove("first", out var removedClient);
        await registrar.RefreshToolsAsync(timeout.Token);
        Assert.Equal(1, removed);
        Assert.NotNull(registry.GetTool("mcp__second__echo"));
        await registrar.StopAsync(timeout.Token);
        Assert.Equal(2, removed);
        await removedClient!.DisposeAsync();
        foreach (var client in clients.Values) await client.DisposeAsync();
        await timeout.CancelAsync();
        foreach (var server in servers) await server.DisposeAsync();
        try { await Task.WhenAll(runs); } catch (OperationCanceledException) { }
    }

    private static async Task UntilAsync(Func<bool> condition, CancellationToken ct)
    {
        while (!condition()) await Task.Delay(10, ct);
    }
}
