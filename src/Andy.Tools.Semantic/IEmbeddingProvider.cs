namespace Andy.Tools.Semantic;

/// <summary>
/// Produces dense vector embeddings for text. Implementations are pluggable so the library is
/// not locked into any specific embedding API; the caller registers a provider via
/// <see cref="SemanticServiceCollectionExtensions.AddSemanticSearch(Microsoft.Extensions.DependencyInjection.IServiceCollection, IEmbeddingProvider)"/>
/// (or supplies one to <see cref="SemanticSearchTool(IEmbeddingProvider)"/> directly).
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Gets the dimensionality of the vectors produced by this provider.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Embeds a batch of texts. The returned list aligns by index with <paramref name="texts"/>:
    /// the vector at index <c>i</c> is the embedding of <c>texts[i]</c>.
    /// </summary>
    /// <param name="texts">The texts to embed.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>One vector per input text, in input order.</returns>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct);
}
