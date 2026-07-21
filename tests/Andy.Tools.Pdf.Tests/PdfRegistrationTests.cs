using Andy.Tools.Core;
using Andy.Tools.Framework;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Pdf.Tests;

/// <summary>
/// Verifies that <see cref="ServiceCollectionExtensions.AddAndyPdfTools(IServiceCollection)"/> wires
/// all six <c>pdf_*</c> tools into the registry with unique ids and LLM-ready metadata.
/// </summary>
public sealed class PdfRegistrationTests
{
    private static readonly string[] ExpectedToolIds =
    {
        "pdf_info", "pdf_extract_text", "pdf_reflow", "pdf_outline", "pdf_extract_tables", "pdf_search",
    };

    private static readonly HashSet<string> AllowedParamTypes =
        new(StringComparer.OrdinalIgnoreCase) { "string", "integer", "number", "boolean", "array", "object" };

    private static async Task<ServiceProvider> BuildAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAndyTools();
        services.AddAndyPdfTools();
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IToolLifecycleManager>().InitializeAsync();
        return provider;
    }

    private static IReadOnlyList<ToolRegistration> PdfTools(IServiceProvider provider) =>
        provider.GetRequiredService<IToolRegistry>().Tools
            .Where(t => t.Metadata.Id.StartsWith("pdf_", StringComparison.Ordinal))
            .ToList();

    [Fact]
    public async Task AddAndyPdfTools_registers_all_six_tools_with_unique_ids()
    {
        using var provider = await BuildAsync();

        var ids = PdfTools(provider).Select(t => t.Metadata.Id).ToList();

        ids.Should().BeEquivalentTo(ExpectedToolIds);
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Every_pdf_tool_requires_only_filesystem_read()
    {
        using var provider = await BuildAsync();

        foreach (var tool in PdfTools(provider))
        {
            tool.Metadata.RequiredPermissions.Should().Be(
                ToolPermissionFlags.FileSystemRead,
                because: "{0} is read-only and must not request write/network/process permissions",
                tool.Metadata.Id);
        }
    }

    [Fact]
    public async Task Every_pdf_tool_exposes_name_description_and_a_required_path_parameter()
    {
        using var provider = await BuildAsync();

        foreach (var tool in PdfTools(provider))
        {
            var meta = tool.Metadata;
            meta.Name.Should().NotBeNullOrWhiteSpace();
            meta.Description.Should().NotBeNullOrWhiteSpace();

            var pathParam = meta.Parameters.SingleOrDefault(p => p.Name == "path");
            pathParam.Should().NotBeNull(because: "{0} reads a file identified by 'path'", meta.Id);
            pathParam!.Required.Should().BeTrue();

            foreach (var param in meta.Parameters)
            {
                AllowedParamTypes.Should().Contain(param.Type,
                    because: "parameter '{0}' on {1} must use a schema-supported type", param.Name, meta.Id);
            }
        }
    }
}
