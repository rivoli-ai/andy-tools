using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.CK;

/// <summary>
/// Dependency-injection registration for the Andy.Tools computable-knowledge
/// tools (<c>ck_*</c>), which compute over the framework-independent
/// <c>Andy.CK</c> engine. Call after <c>AddAndyTools()</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the computable-knowledge tools (<c>ck_ask</c>,
    /// <c>ck_capabilities</c>, <c>ck_convert_unit</c>, <c>ck_constant</c>,
    /// <c>ck_statistics</c>) with the tool registry.
    ///
    /// <para>
    /// The tools are stateless and require no permissions: none reads a file,
    /// opens a socket, or executes anything a caller supplies. The engine
    /// evaluates symbolically over data compiled into its own packages, so a
    /// question cannot reach outside it.
    /// </para>
    /// </summary>
    public static IServiceCollection AddAndyCkTools(this IServiceCollection services)
    {
        services.AddTool<CkAskTool>();
        services.AddTool<CkCapabilitiesTool>();
        services.AddTool<CkConvertUnitTool>();
        services.AddTool<CkConstantTool>();
        services.AddTool<CkStatisticsTool>();
        return services;
    }
}
