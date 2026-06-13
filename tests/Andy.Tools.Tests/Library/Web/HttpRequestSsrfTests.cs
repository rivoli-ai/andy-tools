using System.Net;
using Andy.Tools.Core;
using Andy.Tools.Execution;
using Andy.Tools.Library.Common;
using Andy.Tools.Library.Web;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Andy.Tools.Tests.Library.Web;

/// <summary>
/// Regression tests for issue #12: SSRF protection in the HTTP tool and network classification.
/// </summary>
public class HttpRequestSsrfTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.5.5.5", true)]
    [InlineData("::1", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.255", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("169.254.169.254", true)] // cloud metadata endpoint
    [InlineData("100.64.0.1", true)]       // CGNAT
    [InlineData("0.0.0.0", true)]
    [InlineData("224.0.0.1", true)]        // multicast
    [InlineData("fc00::1", true)]          // unique local
    [InlineData("fe80::1", true)]          // link-local
    [InlineData("8.8.8.8", false)]
    [InlineData("1.1.1.1", false)]
    [InlineData("172.32.0.1", false)]      // just outside 172.16/12
    public void IsPrivateOrLocalAddress_ClassifiesRanges(string ip, bool expectedInternal)
    {
        ToolHelpers.IsPrivateOrLocalAddress(IPAddress.Parse(ip)).Should().Be(expectedInternal);
    }

    [Theory]
    [InlineData("http://127.0.0.1:1/")]
    [InlineData("http://localhost:1/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]:1/")]
    public async Task HttpRequest_ToInternalHost_IsBlockedByDefault(string url)
    {
        var tool = new HttpRequestTool();
        await tool.InitializeAsync();

        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["url"] = url },
            new ToolExecutionContext());

        result.IsSuccessful.Should().BeFalse();
        // Error surfaced from the preflight check.
        (result.ErrorMessage ?? string.Empty).Should().Contain("SSRF");
    }

    [Fact]
    public void SecurityManager_PrivateIp_NetworkAccessDenied()
    {
        var sm = new SecurityManager(new Mock<ILogger<SecurityManager>>().Object);
        var permissions = new ToolPermissions
        {
            NetworkAccess = true,
            CustomPermissions = new Dictionary<string, object?>()
        };

        sm.IsNetworkAccessAllowed("169.254.169.254", permissions).Should().BeFalse();
        sm.IsNetworkAccessAllowed("10.1.2.3", permissions).Should().BeFalse();
    }
}
