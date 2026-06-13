using System.Net;
using System.Net.Sockets;
using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Web;
using FluentAssertions;

namespace Andy.Tools.Tests.Library.Web;

/// <summary>
/// Integration tests for issue #24: charset-aware decoding and a streaming response-size cap. Uses a
/// loopback HttpListener; SSRF protection is opted out via the allow_localhost permission.
/// </summary>
public sealed class HttpRequestToolTests : IDisposable
{
    private readonly HttpListener? _listener;
    private readonly string _prefix;
    private Func<HttpListenerContext, Task>? _handler;

    public HttpRequestToolTests()
    {
        var port = GetFreeLoopbackPort();
        _prefix = $"http://127.0.0.1:{port}/";
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(_prefix);
            _listener.Start();
            _ = AcceptLoopAsync(_listener);
        }
        catch
        {
            _listener = null; // environment disallows binding; tests below no-op
        }
    }

    private async Task AcceptLoopAsync(HttpListener listener)
    {
        while (listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await listener.GetContextAsync(); }
            catch { return; }
            if (_handler != null) { try { await _handler(ctx); } catch { } }
        }
    }

    private static int GetFreeLoopbackPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static ToolExecutionContext LocalContext() => new()
    {
        Permissions = new ToolPermissions
        {
            NetworkAccess = true,
            CustomPermissions = new Dictionary<string, object?> { ["allow_localhost"] = true }
        }
    };

    [Fact]
    public async Task Get_HonorsResponseCharset()
    {
        if (_listener == null) { return; }

        _handler = ctx =>
        {
            var bytes = Encoding.GetEncoding("iso-8859-1").GetBytes("héllo");
            ctx.Response.ContentType = "text/plain; charset=iso-8859-1";
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
            return Task.CompletedTask;
        };

        var tool = new HttpRequestTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["url"] = _prefix }, LocalContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var data = (Dictionary<string, object?>)result.Data!;
        data["content"].Should().Be("héllo", "the iso-8859-1 charset must be honored, not assumed UTF-8");
    }

    [Fact]
    public async Task Get_AbortsWhenResponseExceedsSizeCap()
    {
        if (_listener == null) { return; }

        _handler = ctx =>
        {
            // 2 MB body, no Content-Length (chunked) so the streaming cap is what stops it.
            ctx.Response.SendChunked = true;
            var chunk = new byte[64 * 1024];
            try
            {
                for (var i = 0; i < 32; i++) { ctx.Response.OutputStream.Write(chunk, 0, chunk.Length); }
                ctx.Response.Close();
            }
            catch { /* client aborted once the cap was hit */ }
            return Task.CompletedTask;
        };

        var tool = new HttpRequestTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["url"] = _prefix, ["max_response_size_mb"] = 1.0 },
            LocalContext());

        result.IsSuccessful.Should().BeFalse();
        (result.ErrorMessage ?? "").Should().Contain("too large");
    }

    public void Dispose()
    {
        try { _listener?.Stop(); _listener?.Close(); } catch { }
    }
}
