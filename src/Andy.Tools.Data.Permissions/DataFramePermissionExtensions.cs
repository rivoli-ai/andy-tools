using Andy.Permissions.Authorization;
using Andy.Permissions.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Data.Permissions;

/// <summary>
/// Integration glue between the dataframe tools and the Andy.Permissions consent gate. Kept in a
/// separate assembly so the core <c>Andy.Tools.Data</c> package does not depend on Andy.Permissions.
/// </summary>
public static class DataFramePermissionExtensions
{
    /// <summary>
    /// Teaches the Andy.Permissions action resolver which dataframe parameters carry filesystem
    /// paths, so allow/ask/deny rules can scope them (e.g. <c>dataframe_export(/data/exports/**)</c>).
    /// Call once at host startup, after <c>AddAndyPermissions()</c> and after the provider is built.
    /// Operations that only reference datasets by <c>dataset_id</c> govern no external resource and
    /// need no mapping.
    /// </summary>
    public static IServiceProvider UseAndyDataFramePermissions(this IServiceProvider provider)
    {
        if (provider.GetService<IToolActionResolver>() is DefaultToolActionResolver resolver)
        {
            resolver.Register("dataframe_load_csv", ("path", ResourceKind.Path));
            resolver.Register("dataframe_load_parquet", ("path", ResourceKind.Path));
            resolver.Register("dataframe_load_json", ("path", ResourceKind.Path));
            resolver.Register("dataframe_load_delta", ("path", ResourceKind.Path));
            resolver.Register("dataframe_export", ("path", ResourceKind.Path));
        }

        return provider;
    }
}
