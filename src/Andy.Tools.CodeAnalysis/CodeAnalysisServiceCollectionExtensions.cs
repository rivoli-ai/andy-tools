using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.CodeAnalysis;

/// <summary>
/// DI registration extensions for the Roslyn-powered code-analysis tools.
/// </summary>
public static class CodeAnalysisServiceCollectionExtensions
{
    /// <summary>
    /// Registers the code-analysis tools (currently <see cref="ListDefinitionsTool"/>).
    /// This is opt-in and is intentionally NOT called by the core <c>AddAndyTools()</c>, so the
    /// core package stays free of the Roslyn dependency. Assumes <c>AddAndyTools()</c> has already
    /// been called so the tool registry is available.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCodeAnalysisTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTool<ListDefinitionsTool>();

        return services;
    }
}
