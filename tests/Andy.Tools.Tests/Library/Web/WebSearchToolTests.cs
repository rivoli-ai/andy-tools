using Andy.Tools.Core;
using Andy.Tools.Library.Web;
using FluentAssertions;
using Moq;

namespace Andy.Tools.Tests.Library.Web;

/// <summary>
/// Tests for the web_search tool (issue #66). The provider is mocked so no network I/O occurs;
/// these verify result mapping, empty-query handling, and that the parameterless constructor
/// (used by the registry's metadata probe) works.
/// </summary>
public sealed class WebSearchToolTests
{
    private static ToolExecutionContext Context() => new()
    {
        Permissions = new ToolPermissions { NetworkAccess = true }
    };

    [Fact]
    public async Task Search_ReturnsMappedResultsFromProvider()
    {
        var provider = new Mock<IWebSearchProvider>();
        provider
            .Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WebSearchResult>
            {
                new("First Result", "https://example.com/first", "The first snippet"),
                new("Second Result", "https://example.com/second", "The second snippet")
            });

        var tool = new WebSearchTool(provider.Object);
        await tool.InitializeAsync();

        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["query"] = "andy tools" }, Context());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);

        var items = (List<Dictionary<string, object?>>)result.Data!;
        items.Should().HaveCount(2);

        items[0]["title"].Should().Be("First Result");
        items[0]["url"].Should().Be("https://example.com/first");
        items[0]["snippet"].Should().Be("The first snippet");
        items[1]["title"].Should().Be("Second Result");
        items[1]["url"].Should().Be("https://example.com/second");
    }

    [Fact]
    public async Task Search_WithMissingQuery_IsNotSuccessful()
    {
        var provider = new Mock<IWebSearchProvider>();
        var tool = new WebSearchTool(provider.Object);
        await tool.InitializeAsync();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>(), Context());

        result.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task Search_WithEmptyQuery_IsNotSuccessful()
    {
        var provider = new Mock<IWebSearchProvider>();
        var tool = new WebSearchTool(provider.Object);
        await tool.InitializeAsync();

        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["query"] = "   " }, Context());

        result.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public void ParameterlessConstructor_Constructs()
    {
        // The registry probes tool metadata via Activator.CreateInstance(type), which requires a
        // working parameterless constructor; this guards that registration path.
        var tool = new WebSearchTool();

        tool.Metadata.Id.Should().Be("web_search");
        tool.Metadata.RequiredPermissions.Should().Be(ToolPermissionFlags.Network);
    }
}
