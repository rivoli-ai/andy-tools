using Andy.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Andy.Tools.Semantic;

/// <summary>
/// DI registration extensions for the opt-in semantic search package.
/// </summary>
public static class SemanticServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="SemanticSearchTool"/> with Andy.Tools. This is opt-in and keeps the
    /// core dependency-free. The caller MUST also register an <see cref="IEmbeddingProvider"/> in DI
    /// (otherwise the tool returns a <c>NO_EMBEDDING_PROVIDER</c> failure at execution time).
    /// Use <see cref="AddSemanticSearch(IServiceCollection, IEmbeddingProvider)"/> to register both at once.
    /// Assumes <c>AddAndyTools()</c> has been (or will be) called.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSemanticSearch(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTool<SemanticSearchTool>();

        return services;
    }

    /// <summary>
    /// Registers the supplied <see cref="IEmbeddingProvider"/> as a singleton and the
    /// <see cref="SemanticSearchTool"/> with Andy.Tools.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="provider">The embedding provider to use.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSemanticSearch(this IServiceCollection services, IEmbeddingProvider provider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(provider);

        services.TryAddSingleton(provider);

        return services.AddSemanticSearch();
    }
}
