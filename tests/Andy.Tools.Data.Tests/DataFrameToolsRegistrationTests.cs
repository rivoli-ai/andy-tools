using Andy.Data;
using Andy.Data.Backend;
using Andy.Tools.Core;
using Andy.Tools.Data;
using Andy.Tools.Framework;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Data.Tests;

/// <summary>
/// Verifies <see cref="ServiceCollectionExtensions.AddAndyDataFrameTools(IServiceCollection)"/> fully
/// and correctly wires the dataframe_* tools: all 28 are registered, the DuckDB backend and dataset
/// catalog are available, every tool exposes LLM-ready metadata, the IPathPolicy filesystem gate is
/// honored, and structured (object/array) parameters marshal through the executor. Complements
/// <see cref="DataFrameToolsIntegrationTests"/> (which proves the basic executor flow).
/// </summary>
public sealed class DataFrameToolsRegistrationTests
{
    private static readonly string[] ExpectedToolIds =
    {
        "dataframe_load_csv", "dataframe_load_json", "dataframe_load_parquet", "dataframe_load_delta",
        "dataframe_schema", "dataframe_profile", "dataframe_preview", "dataframe_value_counts",
        "dataframe_assert", "dataframe_list", "dataframe_select", "dataframe_filter",
        "dataframe_with_column", "dataframe_rename", "dataframe_group_by", "dataframe_window",
        "dataframe_pivot", "dataframe_unpivot", "dataframe_unnest", "dataframe_join", "dataframe_sample",
        "dataframe_sort", "dataframe_distinct", "dataframe_union", "dataframe_fillna", "dataframe_dropna",
        "dataframe_export", "dataframe_drop",
    };

    // The closed set of parameter types the operation metadata declares (and that the LLM schema builder expects).
    private static readonly HashSet<string> AllowedParamTypes =
        new(StringComparer.OrdinalIgnoreCase) { "string", "integer", "number", "boolean", "array", "object" };

    private static async Task<ServiceProvider> BuildAsync(IPathPolicy? pathPolicy = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (pathPolicy is not null)
        {
            services.AddSingleton(pathPolicy);
        }

        services.AddAndyTools();
        services.AddAndyDataFrameTools();
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IToolLifecycleManager>().InitializeAsync();
        return provider;
    }

    private static ToolExecutionContext Ctx() => new() { Permissions = new ToolPermissions() };

    private static IDictionary<string, object?> Env(ToolResult r) => (IDictionary<string, object?>)r.Data!;

    private static IReadOnlyList<ToolRegistration> DataFrameTools(IServiceProvider provider) =>
        provider.GetRequiredService<IToolRegistry>().Tools
            .Where(t => t.Metadata.Id.StartsWith("dataframe_", StringComparison.Ordinal))
            .ToList();

    [Fact]
    public async Task AddAndyDataFrameTools_registers_all_28_tools()
    {
        using var provider = await BuildAsync();

        var ids = DataFrameTools(provider)
            .Select(t => t.Metadata.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        ids.Should().HaveCount(28);
        ids.Should().BeEquivalentTo(ExpectedToolIds);
    }

    [Fact]
    public async Task AddAndyDataFrameTools_registers_the_backend_and_catalog()
    {
        using var provider = await BuildAsync();

        provider.GetService<IDuckDbBackend>().Should().NotBeNull();
        provider.GetService<IDatasetCatalog>().Should().NotBeNull();
    }

    [Fact]
    public async Task Every_dataframe_tool_exposes_llm_ready_metadata()
    {
        using var provider = await BuildAsync();

        var tools = DataFrameTools(provider);
        tools.Should().HaveCount(28);

        foreach (var tool in tools)
        {
            var m = tool.Metadata;
            m.Name.Should().NotBeNullOrWhiteSpace(m.Id);
            m.Description.Should().NotBeNullOrWhiteSpace(m.Id);
            m.Description.Length.Should().BeGreaterThan(20, "tool '{0}' needs a model-facing description", m.Id);

            foreach (var p in m.Parameters)
            {
                p.Name.Should().NotBeNullOrWhiteSpace(m.Id);
                AllowedParamTypes.Contains(p.Type).Should()
                    .BeTrue("parameter '{0}.{1}' declares an unexpected type '{2}'", m.Id, p.Name, p.Type);
            }
        }
    }

    [Fact]
    public async Task Path_policy_denial_is_enforced_through_the_load_tool()
    {
        using var provider = await BuildAsync(new DenyAllPathPolicy());
        var executor = provider.GetRequiredService<IToolExecutor>();

        var path = Path.Combine(Path.GetTempPath(), $"atd_deny_{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, "a,b\n1,2\n");
        try
        {
            var result = await executor.ExecuteAsync("dataframe_load_csv",
                new Dictionary<string, object?> { ["path"] = path, ["dataset_id"] = "denied" }, Ctx());

            result.IsSuccessful.Should().BeFalse();
            Env(result)["error_code"].Should().Be("PERMISSION_DENIED");
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Structured_object_and_array_params_marshal_through_the_executor()
    {
        using var provider = await BuildAsync();
        var executor = provider.GetRequiredService<IToolExecutor>();

        var path = Path.Combine(Path.GetTempPath(), $"atd_sel_{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, "region,amount\nEMEA,100\nAPAC,40\nEMEA,250\n");
        try
        {
            (await executor.ExecuteAsync("dataframe_load_csv",
                new Dictionary<string, object?> { ["path"] = path, ["dataset_id"] = "sales" }, Ctx()))
                .IsSuccessful.Should().BeTrue();

            // 'predicate' is a nested object — exercises object-parameter marshaling.
            var filtered = await executor.ExecuteAsync("dataframe_filter", new Dictionary<string, object?>
            {
                ["dataset_id"] = "sales", ["into"] = "big",
                ["predicate"] = new Dictionary<string, object?>
                {
                    ["column"] = "amount", ["op"] = "gte", ["value"] = 100,
                },
            }, Ctx());
            filtered.IsSuccessful.Should().BeTrue(filtered.ErrorMessage);
            Env(filtered)["row_count"].Should().Be(2L);

            // 'columns' is an array mixing a bare name and a { column, as } rename object.
            var selected = await executor.ExecuteAsync("dataframe_select", new Dictionary<string, object?>
            {
                ["dataset_id"] = "big", ["into"] = "proj",
                ["columns"] = new object[]
                {
                    "region",
                    new Dictionary<string, object?> { ["column"] = "amount", ["as"] = "amt" },
                },
            }, Ctx());
            selected.IsSuccessful.Should().BeTrue(selected.ErrorMessage);

            var schema = (IEnumerable<object?>)Env(selected)["schema"]!;
            var names = schema.Cast<IDictionary<string, object?>>().Select(c => c["name"]?.ToString()).ToList();
            names.Should().Equal("region", "amt");
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    private sealed class DenyAllPathPolicy : IPathPolicy
    {
        public bool CanRead(string path) => false;
        public bool CanWrite(string path) => false;
    }
}
