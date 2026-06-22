using Andy.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Pdf;

/// <summary>
/// Dependency-injection registration for the Andy.Tools PDF tools (<c>pdf_*</c>), which read PDFs
/// via the fully-managed <c>Andy.Doc</c> engine. Call after <c>AddAndyTools()</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PDF tools (<c>pdf_info</c>, <c>pdf_extract_text</c>, <c>pdf_reflow</c>,
    /// <c>pdf_outline</c>, <c>pdf_extract_tables</c>, <c>pdf_search</c>) with the tool registry.
    /// The tools are stateless and require only filesystem-read permission.
    /// </summary>
    public static IServiceCollection AddAndyPdfTools(this IServiceCollection services)
    {
        services.AddTool<PdfInfoTool>();
        services.AddTool<PdfExtractTextTool>();
        services.AddTool<PdfReflowTool>();
        services.AddTool<PdfOutlineTool>();
        services.AddTool<PdfExtractTablesTool>();
        services.AddTool<PdfSearchTool>();
        return services;
    }
}
