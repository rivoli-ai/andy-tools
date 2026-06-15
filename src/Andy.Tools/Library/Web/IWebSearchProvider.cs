namespace Andy.Tools.Library.Web;

/// <summary>
/// A single web search result.
/// </summary>
/// <param name="Title">The result title.</param>
/// <param name="Url">The result URL.</param>
/// <param name="Snippet">A short snippet/description of the result.</param>
public record WebSearchResult(string Title, string Url, string Snippet);

/// <summary>
/// Provides web search results for a query. Implementations may use any backend
/// (a key-free HTML endpoint, a paid search API, etc.). Register a custom implementation
/// in DI before calling <c>AddAndyTools()</c> to override the default provider used by the
/// <c>web_search</c> tool.
/// </summary>
public interface IWebSearchProvider
{
    /// <summary>
    /// Searches for the given query and returns up to <paramref name="maxResults"/> results.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="maxResults">The maximum number of results to return.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The search results (possibly empty).</returns>
    Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct);
}
