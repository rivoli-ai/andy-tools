using System.Net;
using System.Text.RegularExpressions;

namespace Andy.Tools.Library.Web;

/// <summary>
/// A key-free <see cref="IWebSearchProvider"/> that scrapes DuckDuckGo's HTML endpoint
/// (<c>https://html.duckduckgo.com/html/</c>). This needs no API key and works out of the box,
/// but it is best-effort HTML scraping: if DuckDuckGo changes its markup, parsing degrades
/// gracefully (returns fewer/no results, never throws). For production use, register a custom
/// <see cref="IWebSearchProvider"/> backed by a stable search API.
/// </summary>
public sealed class DuckDuckGoSearchProvider : IWebSearchProvider
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    private static readonly HttpClient HttpClient = CreateClient();

    // Matches each result anchor: class="result__a" ... href="..." ...>TITLE</a>
    // (attribute order varies, so match class and href independently within the same tag).
    private static readonly Regex ResultAnchorRegex = new(
        "<a\\b[^>]*\\bclass=\"[^\"]*\\bresult__a\\b[^\"]*\"[^>]*>(?<title>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex HrefRegex = new(
        "href=\"(?<href>[^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches a snippet block: an element carrying class="result__snippet", capturing its inner text up
    // to that element's closing tag. Inner inline tags (e.g. <b> query highlights) are kept here and
    // stripped later by CleanText, so the snippet isn't truncated at the first nested "</".
    private static readonly Regex SnippetRegex = new(
        "<(?<tag>[a-zA-Z][a-zA-Z0-9]*)\\b[^>]*\\bclass=\"[^\"]*\\bresult__snippet\\b[^\"]*\"[^>]*>(?<snippet>.*?)</\\k<tag>>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Strips any residual HTML tags from extracted text (e.g. <b> highlights in titles/snippets).
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Singleline | RegexOptions.Compiled);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        return client;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct)
    {
        var url = "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query);
        var html = await HttpClient.GetStringAsync(url, ct);
        return ParseResults(html, maxResults);
    }

    /// <summary>
    /// Extracts search results from DuckDuckGo HTML. Public and static so it can be unit-tested
    /// without performing any network I/O. Never throws: returns an empty list if the expected
    /// structure is not found.
    /// </summary>
    /// <param name="html">The raw HTML returned by the DuckDuckGo HTML endpoint.</param>
    /// <param name="maxResults">The maximum number of results to return.</param>
    /// <returns>The extracted results (possibly empty).</returns>
    public static IReadOnlyList<WebSearchResult> ParseResults(string html, int maxResults)
    {
        var results = new List<WebSearchResult>();

        if (string.IsNullOrEmpty(html) || maxResults <= 0)
        {
            return results;
        }

        try
        {
            var snippetMatches = SnippetRegex.Matches(html);
            var snippetIndex = 0;

            foreach (Match anchor in ResultAnchorRegex.Matches(html))
            {
                if (results.Count >= maxResults)
                {
                    break;
                }

                var title = CleanText(anchor.Groups["title"].Value);

                var hrefMatch = HrefRegex.Match(anchor.Value);
                var rawHref = hrefMatch.Success ? hrefMatch.Groups["href"].Value : string.Empty;
                var url = ResolveUrl(WebUtility.HtmlDecode(rawHref) ?? string.Empty);

                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var snippet = snippetIndex < snippetMatches.Count
                    ? CleanText(snippetMatches[snippetIndex].Groups["snippet"].Value)
                    : string.Empty;
                snippetIndex++;

                results.Add(new WebSearchResult(title, url, snippet));
            }
        }
        catch
        {
            // Be defensive: never throw from parsing. Return whatever was collected so far.
        }

        return results;
    }

    /// <summary>
    /// Resolves a DuckDuckGo result href to a real URL. DuckDuckGo wraps result links as a
    /// redirect of the form <c>//duckduckgo.com/l/?uddg=&lt;percent-encoded real URL&gt;&amp;...</c>;
    /// when present, the <c>uddg</c> parameter is extracted and unescaped. Otherwise the raw href
    /// is returned as-is.
    /// </summary>
    private static string ResolveUrl(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return string.Empty;
        }

        var uddgIndex = href.IndexOf("uddg=", StringComparison.OrdinalIgnoreCase);
        if (uddgIndex >= 0)
        {
            var value = href[(uddgIndex + "uddg=".Length)..];
            var ampIndex = value.IndexOf('&');
            if (ampIndex >= 0)
            {
                value = value[..ampIndex];
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                return Uri.UnescapeDataString(value);
            }
        }

        // Protocol-relative URLs (//host/path) are returned with an explicit scheme.
        if (href.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + href;
        }

        return href;
    }

    private static string CleanText(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var stripped = TagRegex.Replace(raw, string.Empty);
        return (WebUtility.HtmlDecode(stripped) ?? string.Empty).Trim();
    }
}
