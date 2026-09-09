using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Andy.MCP.Client;
using Andy.MCP.Configuration;
using Andy.MCP.Protocol;
using Andy.MCP.Transport;
using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Mcp;

/// <summary>Controls discovery of newly connected servers and retries after discovery failures.</summary>
public sealed class McpToolDiscoveryOptions
{
    /// <summary>Gets or sets the fallback refresh interval. Notifications trigger immediate refresh.</summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>Reconciles MCP tools with the shared registry throughout the host lifetime.</summary>
public sealed class McpToolRegistrar : IHostedService, IDisposable
{
    private readonly IMcpConnectionManager _manager;
    private readonly IToolRegistry _registry;
    private readonly ILogger<McpToolRegistrar> _logger;
    private readonly McpToolDiscoveryOptions _options;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<bool> _changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
    });
    private readonly Dictionary<string, McpClient> _clients = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<McpClient, byte> _disconnected = new();
    private readonly Dictionary<string, (ToolRegistration Registration, string Definition)> _owned = new(StringComparer.Ordinal);
    private Task? _worker;
    private int _disposed;

    /// <summary>Creates a registrar. Connection startup is owned by Andy.MCP's hosted service.</summary>
    public McpToolRegistrar(IMcpConnectionManager connectionManager, IToolRegistry toolRegistry,
        ILogger<McpToolRegistrar> logger, McpToolDiscoveryOptions? options = null)
    {
        _manager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _registry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new();
        if (_options.RefreshInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RefreshToolsAsync(cancellationToken).ConfigureAwait(false);
        _worker = RunAsync(_stop.Token);
    }

    /// <summary>Refreshes each connected server independently and removes stale registrations.</summary>
    public async Task RefreshToolsAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        var ct = linked.Token;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var servers = _manager.ConnectedServers.ToHashSet(StringComparer.Ordinal);
            foreach (var (name, client) in _clients.ToArray())
            {
                if (!servers.Contains(name) || !ReferenceEquals(_manager.GetClient(name), client))
                {
                    Unsubscribe(client);
                    _clients.Remove(name);
                    RemoveServerTools(name);
                }
            }

            foreach (var name in servers)
            {
                ct.ThrowIfCancellationRequested();
                var client = _manager.GetClient(name);
                if (client is null) continue;
                if (!_clients.ContainsKey(name))
                {
                    _clients.Add(name, client);
                    client.ToolsChanged += OnToolsChanged;
                    client.Disconnected += OnDisconnected;
                }
                if (_disconnected.ContainsKey(client) || client.Session.State != McpSessionState.Ready)
                {
                    RemoveServerTools(name);
                    continue;
                }
                try
                {
                    var tools = client.Session.ServerCapabilities?.Tools is null
                        ? [] : await client.ListToolsAsync(ct).ConfigureAwait(false);
                    // A disconnect or replacement during discovery must not resurrect old tools.
                    if (_disconnected.ContainsKey(client) || !ReferenceEquals(_manager.GetClient(name), client))
                    {
                        RemoveServerTools(name);
                        continue;
                    }
                    var current = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var tool in tools)
                    {
                        var metadata = McpToolMetadataMapper.Map(name, tool);
                        current.Add(metadata.Id);
                        var definition = JsonSerializer.Serialize(tool);
                        if (_owned.TryGetValue(metadata.Id, out var previous))
                        {
                            if (previous.Definition == definition && ReferenceEquals(_registry.GetTool(metadata.Id), previous.Registration)) continue;
                            RemoveTool(metadata.Id);
                        }
                        // Never replace a registration owned by another provider.
                        if (_registry.GetTool(metadata.Id) is not null) continue;
                        var toolName = tool.Name;
                        var registration = _registry.RegisterTool(metadata,
                            sp => new McpTool(metadata, name, toolName, sp.GetRequiredService<IMcpToolInvoker>()));
                        registration.Source = "mcp";
                        _owned.Add(metadata.Id, (registration, definition));
                    }
                    foreach (var id in ServerToolIds(name).Where(id => !current.Contains(id)).ToArray()) RemoveTool(id);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to refresh MCP tools from {Server}; retaining the last successful discovery.", name);
                }
            }
        }
        finally { _gate.Release(); }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var wake = CancellationTokenSource.CreateLinkedTokenSource(ct);
                wake.CancelAfter(_options.RefreshInterval);
                try { await _changes.Reader.ReadAsync(wake.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
                await RefreshToolsAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private void OnToolsChanged(object? sender, EventArgs args) => _changes.Writer.TryWrite(true);

    private void OnDisconnected(object? sender, TransportDisconnectedEventArgs args)
    {
        if (sender is McpClient client) _disconnected.TryAdd(client, 0);
        _changes.Writer.TryWrite(true);
    }

    private IEnumerable<string> ServerToolIds(string name) => _owned
        .Where(pair => Equals(pair.Value.Registration.Metadata.AdditionalMetadata["mcp_server"], name))
        .Select(pair => pair.Key);

    private void RemoveServerTools(string name)
    {
        foreach (var id in ServerToolIds(name).ToArray()) RemoveTool(id);
    }

    private void RemoveTool(string id)
    {
        if (_owned.Remove(id, out var owned) && ReferenceEquals(_registry.GetTool(id), owned.Registration))
            _registry.UnregisterTool(id);
    }

    private void Unsubscribe(McpClient client)
    {
        client.ToolsChanged -= OnToolsChanged;
        client.Disconnected -= OnDisconnected;
        _disconnected.TryRemove(client, out _);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        if (_worker is not null) await _worker.WaitAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var client in _clients.Values) Unsubscribe(client);
            _clients.Clear();
            foreach (var id in _owned.Keys.ToArray()) RemoveTool(id);
        }
        finally { _gate.Release(); }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _stop.Dispose();
        _gate.Dispose();
    }
}
