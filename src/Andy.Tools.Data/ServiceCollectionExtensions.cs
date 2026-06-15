using Andy.Tools;
using Andy.Data;
using Andy.Data.Backend;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Andy.Tools.Data;

/// <summary>
/// Dependency-injection registration for the Andy.Tools dataframe tools (<c>dataframe_*</c>), which
/// adapt the framework-independent <c>Andy.Data</c> operations. Call after <c>AddAndyTools()</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the dataframe tools and their supporting services as a process-wide singleton
    /// backend and catalog (every consumer sees the same datasets). To scope by filesystem path,
    /// register an <see cref="IPathPolicy"/> (<c>services.AddSingleton&lt;IPathPolicy, MyPolicy&gt;()</c>);
    /// the path-bearing tools resolve it via DI.
    /// </summary>
    public static IServiceCollection AddAndyDataFrameTools(this IServiceCollection services) =>
        services.AddAndyDataFrameTools(ServiceLifetime.Singleton);

    /// <summary>
    /// Registers the dataframe tools with an explicit lifetime for the backend and catalog.
    /// <see cref="ServiceLifetime.Singleton"/> shares one DuckDB engine and catalog process-wide;
    /// <see cref="ServiceLifetime.Scoped"/> gives each DI scope its own (real per-session isolation
    /// requires resolving the executor from a per-session scope).
    /// </summary>
    public static IServiceCollection AddAndyDataFrameTools(
        this IServiceCollection services, ServiceLifetime lifetime)
    {
        // One embedded in-memory DuckDB connection (used under a lock) and one catalog per lifetime unit.
        services.TryAdd(new ServiceDescriptor(typeof(IDuckDbBackend), typeof(DuckDbBackend), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IDatasetCatalog), typeof(InMemoryDatasetCatalog), lifetime));

        services.AddTool<LoadCsvTool>();
        services.AddTool<LoadJsonTool>();
        services.AddTool<LoadParquetTool>();
        services.AddTool<LoadDeltaTool>();
        services.AddTool<SchemaTool>();
        services.AddTool<ProfileTool>();
        services.AddTool<PreviewTool>();
        services.AddTool<ValueCountsTool>();
        services.AddTool<SelectTool>();
        services.AddTool<RenameTool>();
        services.AddTool<FilterTool>();
        services.AddTool<WithColumnTool>();
        services.AddTool<GroupByTool>();
        services.AddTool<WindowTool>();
        services.AddTool<PivotTool>();
        services.AddTool<UnpivotTool>();
        services.AddTool<UnnestTool>();
        services.AddTool<JoinTool>();
        services.AddTool<SampleTool>();
        services.AddTool<SortTool>();
        services.AddTool<DistinctTool>();
        services.AddTool<UnionTool>();
        services.AddTool<ListTool>();
        services.AddTool<DropTool>();
        services.AddTool<ExportTool>();
        services.AddTool<FillnaTool>();
        services.AddTool<DropnaTool>();
        services.AddTool<AssertTool>();

        // All 28 dataframe tools registered.
        return services;
    }
}
