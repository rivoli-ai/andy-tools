using System.Net;
using System.Net.Sockets;
using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Web;
using FluentAssertions;

namespace Andy.Tools.Tests.Library.Web;

/// <summary>
/// Integration tests for issue #67 (web_fetch): HTML is reduced to readable text, max_length truncates,
/// and internal hosts are blocked by default (SSRF). Uses a loopback HttpListener; SSRF protection is
/// opted out via the allow_localhost permission where a successful fetch is expected.
/// </summary>
public sealed class WebFetchToolTests : IDisposable
{
    private readonly HttpListener? _listener;
    private readonly string _prefix;
    private Func<HttpListenerContext, Task>? _handler;

    public WebFetchToolTests()
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

    private static ToolExecutionContext DefaultContext() => new()
    {
        Permissions = new ToolPermissions { NetworkAccess = true }
    };

    private void RespondHtml(string html)
    {
        _handler = ctx =>
        {
            var bytes = Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
            return Task.CompletedTask;
        };
    }

    [Fact]
    public async Task Fetch_ReducesHtmlToReadableText()
    {
        if (_listener == null) { return; }

        RespondHtml(
            "<html><head><style>.x{color:red}</style>" +
            "<script>var a = 1 < 2;</script></head>" +
            "<body><h1>Title</h1><p>Hello &amp; welcome to R&amp;D</p></body></html>");

        var tool = new WebFetchTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["url"] = _prefix }, LocalContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var data = (Dictionary<string, object?>)result.Data!;
        var content = (string)data["content"]!;

        content.Should().Contain("Title");
        content.Should().Contain("Hello & welcome to R&D", "HTML entities must be decoded");
        content.Should().NotContain("color:red", "style blocks must be removed");
        content.Should().NotContain("var a", "script blocks must be removed");
        content.Should().NotContain("<", "HTML tags must be stripped");
        data["is_html"].Should().Be(true);
        data["truncated"].Should().Be(false);
    }

    [Fact]
    public async Task Fetch_TruncatesToMaxLength()
    {
        if (_listener == null) { return; }

        RespondHtml("<html><body><p>" + new string('a', 5000) + "</p></body></html>");

        var tool = new WebFetchTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["url"] = _prefix, ["max_length"] = 100 },
            LocalContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var data = (Dictionary<string, object?>)result.Data!;
        var content = (string)data["content"]!;

        content.Length.Should().Be(100);
        data["truncated"].Should().Be(true);
    }

    [Fact]
    public async Task Fetch_BlocksInternalHostByDefault()
    {
        if (_listener == null) { return; }

        RespondHtml("<html><body>secret</body></html>");

        var tool = new WebFetchTool();
        await tool.InitializeAsync();
        // No allow_localhost permission: the loopback target must be blocked by SSRF protection.
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["url"] = _prefix }, DefaultContext());

        result.IsSuccessful.Should().BeFalse();
        result.Metadata["error_code"].Should().Be("BLOCKED_INTERNAL_HOST");
    }

    [Fact]
    public async Task Fetch_ReturnsNonHtmlAsIs()
    {
        if (_listener == null) { return; }

        _handler = ctx =>
        {
            var bytes = Encoding.UTF8.GetBytes("plain text body with <not-a-tag>");
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
            return Task.CompletedTask;
        };

        var tool = new WebFetchTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["url"] = _prefix }, LocalContext());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var data = (Dictionary<string, object?>)result.Data!;
        ((string)data["content"]!).Should().Be("plain text body with <not-a-tag>");
        data["is_html"].Should().Be(false);
    }

    public void Dispose()
    {
        try { _listener?.Stop(); _listener?.Close(); } catch { }
    }
}
