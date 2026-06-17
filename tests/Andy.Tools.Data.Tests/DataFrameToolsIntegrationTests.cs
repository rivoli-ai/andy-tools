using Andy.Tools.Core;
using Andy.Tools.Data;
using Andy.Tools.Framework;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Data.Tests;

/// <summary>
/// Verifies the dataframe_* tools work end-to-end through the real Andy DI + registry + IToolExecutor
/// path, adapting the framework-independent Andy.Data operations. Exhaustive operation behavior is
/// covered by the Andy.Data engine tests; this proves the adapter layer wires up correctly.
/// </summary>
public sealed class DataFrameToolsIntegrationTests
{
    private static async Task<(IToolExecutor executor, ServiceProvider provider)> BuildAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAndyTools();
        services.AddAndyDataFrameTools();
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IToolLifecycleManager>().InitializeAsync();
        return (provider.GetRequiredService<IToolExecutor>(), provider);
    }

    private static ToolExecutionContext Ctx() => new() { Permissions = new ToolPermissions() };

    private static IDictionary<string, object?> Env(ToolResult r) => (IDictionary<string, object?>)r.Data!;

    [Fact]
    public async Task Load_group_by_and_assert_flow_through_the_executor()
    {
        var (executor, provider) = await BuildAsync();
        using var _ = provider;

        var path = Path.Combine(Path.GetTempPath(), $"atd_{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, "region,amount\nEMEA,100\nEMEA,40\nAPAC,200\n");
        try
        {
            var load = await executor.ExecuteAsync("dataframe_load_csv",
                new Dictionary<string, object?> { ["path"] = path, ["dataset_id"] = "sales" }, Ctx());
            load.IsSuccessful.Should().BeTrue(load.ErrorMessage);

            var grouped = await executor.ExecuteAsync("dataframe_group_by", new Dictionary<string, object?>
            {
                ["dataset_id"] = "sales", ["into"] = "by_region",
                ["group_by"] = new[] { "region" },
                ["aggregations"] = new object[]
                {
                    new Dictionary<string, object?> { ["column"] = "amount", ["function"] = "sum", ["alias"] = "total" },
                },
            }, Ctx());
            grouped.IsSuccessful.Should().BeTrue(grouped.ErrorMessage);
            Env(grouped)["row_count"].Should().Be(2L);

            var asserted = await executor.ExecuteAsync("dataframe_assert", new Dictionary<string, object?>
            {
                ["dataset_id"] = "sales",
                ["expectations"] = new object[]
                {
                    new Dictionary<string, object?> { ["type"] = "not_null", ["column"] = "region" },
                    new Dictionary<string, object?> { ["type"] = "row_count", ["min"] = 1 },
                },
            }, Ctx());
            asserted.IsSuccessful.Should().BeTrue(asserted.ErrorMessage);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Error_envelope_carries_error_code_through_the_executor()
    {
        var (executor, provider) = await BuildAsync();
        using var _ = provider;

        var missing = await executor.ExecuteAsync("dataframe_schema",
            new Dictionary<string, object?> { ["dataset_id"] = "nope" }, Ctx());
        missing.IsSuccessful.Should().BeFalse();
        Env(missing)["error_code"].Should().Be("DATASET_NOT_FOUND");
    }

    [Fact]
    public void Tool_metadata_is_derived_from_the_operation_metadata()
    {
        // The parameterless constructor (used by the registry) reads metadata from the operation.
        new FilterTool().Metadata.Id.Should().Be("dataframe_filter");

        var how = new JoinTool().Metadata.Parameters.Single(p => p.Name == "how");
        how.AllowedValues.Should().NotBeNull();
        how.AllowedValues!.Select(v => v?.ToString()).Should().Contain(new[] { "inner", "left", "asof" });

        // Capability flags are mapped per tool: in-memory operations need nothing, loaders read the
        // input path, the exporter writes the output path.
        new SchemaTool().Metadata.RequiredPermissions.Should().Be(ToolPermissionFlags.None);
        new FilterTool().Metadata.RequiredPermissions.Should().Be(ToolPermissionFlags.None);
        new GroupByTool().Metadata.RequiredPermissions.Should().Be(ToolPermissionFlags.None);
        new JoinTool().Metadata.RequiredPermissions.Should().Be(ToolPermissionFlags.None);
        new LoadCsvTool().Metadata.RequiredPermissions.Should().Be(ToolPermissionFlags.FileSystemRead);
        new ExportTool().Metadata.RequiredPermissions.Should().Be(ToolPermissionFlags.FileSystemWrite);
    }
}
