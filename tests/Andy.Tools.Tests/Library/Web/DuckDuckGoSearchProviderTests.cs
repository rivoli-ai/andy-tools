using Andy.Tools.Library.Web;
using FluentAssertions;

namespace Andy.Tools.Tests.Library.Web;

/// <summary>
/// Unit tests for <see cref="DuckDuckGoSearchProvider.ParseResults"/> (issue #66). These exercise the
/// HTML extraction against hand-crafted markup, with no network I/O: title extraction, decoding the
/// <c>uddg</c> redirect URL, direct hrefs, and respecting maxResults.
/// </summary>
public sealed class DuckDuckGoSearchProviderTests
{
    // Two results: the first uses DuckDuckGo's uddg redirect href, the second a direct href.
    private const string SampleHtml = """
        <div class="result">
          <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fpage%3Fa%3D1&amp;rut=abc">Example &amp; Co</a>
          <a class="result__snippet" href="#">This is the <b>first</b> snippet about example.</a>
        </div>
        <div class="result">
          <a class="result__a" href="https://direct.example.org/path">Direct Result</a>
          <a class="result__snippet" href="#">Second snippet here.</a>
        </div>
        """;

    [Fact]
    public void ParseResults_ExtractsTitlesSnippetsAndDecodesUddgUrl()
    {
        var results = DuckDuckGoSearchProvider.ParseResults(SampleHtml, 5);

        results.Should().HaveCount(2);

        results[0].Title.Should().Be("Example & Co");
        results[0].Url.Should().Be("https://example.com/page?a=1", "the uddg redirect must be decoded");
        results[0].Snippet.Should().Be("This is the first snippet about example.");

        results[1].Title.Should().Be("Direct Result");
        results[1].Url.Should().Be("https://direct.example.org/path", "direct hrefs should pass through");
        results[1].Snippet.Should().Be("Second snippet here.");
    }

    [Fact]
    public void ParseResults_RespectsMaxResults()
    {
        var results = DuckDuckGoSearchProvider.ParseResults(SampleHtml, 1);

        results.Should().HaveCount(1);
        results[0].Title.Should().Be("Example & Co");
    }

    [Fact]
    public void ParseResults_ReturnsEmptyForUnrecognizedHtml()
    {
        var results = DuckDuckGoSearchProvider.ParseResults("<html><body>no results</body></html>", 5);

        results.Should().BeEmpty();
    }

    [Fact]
    public void ParseResults_ReturnsEmptyForEmptyInput()
    {
        DuckDuckGoSearchProvider.ParseResults(string.Empty, 5).Should().BeEmpty();
    }
}
