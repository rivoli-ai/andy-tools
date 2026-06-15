using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.Web;

/// <summary>
/// Tool that performs a web search via a pluggable <see cref="IWebSearchProvider"/>.
/// </summary>
/// <remarks>
/// The tool has two constructors by design:
/// <list type="bullet">
/// <item>The parameterless constructor (used by the registry's <c>Activator.CreateInstance</c>
/// metadata probe) defaults to the key-free <see cref="DuckDuckGoSearchProvider"/>, so the tool
/// works out of the box with no configuration.</item>
/// <item>The <see cref="WebSearchTool(IWebSearchProvider)"/> constructor (selected by
/// <c>ActivatorUtilities.CreateInstance</c> at execution time when an <see cref="IWebSearchProvider"/>
/// is registered in DI) enables pluggability: register your own provider before <c>AddAndyTools()</c>
/// to override the default.</item>
/// </list>
/// No default <see cref="IWebSearchProvider"/> is registered in DI, keeping the DuckDuckGo default internal.
/// </remarks>
public class WebSearchTool : ToolBase
{
    private const int DefaultMaxResults = 5;

    private readonly IWebSearchProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSearchTool"/> class using the default,
    /// key-free DuckDuckGo provider.
    /// </summary>
    public WebSearchTool()
    {
        _provider = new DuckDuckGoSearchProvider();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSearchTool"/> class using the supplied provider.
    /// </summary>
    /// <param name="provider">The web search provider to use.</param>
    public WebSearchTool(IWebSearchProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "web_search",
        Name = "Web Search",
        Description = "Searches the web for a query and returns a list of result titles, URLs, and snippets",
        Version = "1.0.0",
        Category = ToolCategory.Web,
        RequiredPermissions = ToolPermissionFlags.Network,
        Parameters =
        [
            new()
            {
                Name = "query",
                Description = "The search query",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "max_results",
                Description = "Maximum number of results to return (default: 5)",
                Type = "integer",
                Required = false,
                DefaultValue = DefaultMaxResults,
                MinValue = 1,
                MaxValue = 20
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var query = GetParameter<string>(parameters, "query");
        var maxResults = GetParameter(parameters, "max_results", DefaultMaxResults);

        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResults.InvalidParameter("query", query, "Query cannot be empty");
        }

        if (maxResults < 1)
        {
            maxResults = DefaultMaxResults;
        }

        try
        {
            ReportProgress(context, $"Searching the web for \"{query}\"...", 30);

            var results = await _provider.SearchAsync(query, maxResults, context.CancellationToken);

            var items = results
                .Select(r => new Dictionary<string, object?>
                {
                    ["title"] = r.Title,
                    ["url"] = r.Url,
                    ["snippet"] = r.Snippet
                })
                .ToList();

            ReportProgress(context, "Search completed", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["query"] = query,
                ["max_results"] = maxResults,
                ["result_count"] = items.Count
            };

            return ToolResults.Success(items, $"Found {items.Count} results", metadata);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Web search failed: {ex.Message}", "WEB_SEARCH_ERROR", details: ex);
        }
    }
}
