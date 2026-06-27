using Andy.Doc.Pdf.Content;
using Andy.Tools.Core;
using Andy.Tools.Library;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Pdf;

/// <summary>
/// Shared base for the <c>pdf_*</c> tools. Each tool reads a single PDF identified by a
/// <c>path</c> parameter; this base centralises safe-path resolution and opening the
/// fully-managed <see cref="PdfImporter"/> so the concrete tools stay declarative.
/// </summary>
/// <remarks>
/// All PDF tools are read-only and therefore require only
/// <see cref="ToolPermissionFlags.FileSystemRead"/>. They never execute code, fetch over the
/// network, or write to disk.
/// </remarks>
public abstract class PdfToolBase : ToolBase
{
    /// <summary>The shared <c>path</c> parameter every PDF tool accepts.</summary>
    protected static ToolParameter PathParameter => new()
    {
        Name = "path",
        Type = "string",
        Description = "Path to the PDF file to read (absolute, or relative to the working directory).",
        Required = true,
    };

    /// <summary>
    /// Resolves and validates the <c>path</c> parameter against the execution context's working
    /// directory and opens a <see cref="PdfImporter"/>. The caller owns the returned importer
    /// and must dispose it.
    /// </summary>
    /// <exception cref="FileNotFoundException">The resolved path does not exist.</exception>
    protected static PdfImporter OpenPdf(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var rawPath = GetParameter<string>(parameters, "path");
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new ArgumentException("The 'path' parameter is required.");
        }

        var safePath = ToolHelpers.GetSafePath(rawPath, context.WorkingDirectory);
        if (!File.Exists(safePath))
        {
            throw new FileNotFoundException($"PDF file not found: {safePath}", safePath);
        }

        // Open the file read-only and let the importer take ownership of the stream.
        var stream = new FileStream(safePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new PdfImporter(stream);
    }

    /// <summary>
    /// Reads an optional 0-based <c>page</c> parameter. Returns <c>null</c> when omitted,
    /// meaning "the whole document".
    /// </summary>
    protected static int? GetOptionalPage(Dictionary<string, object?> parameters)
    {
        // Use a sentinel rather than GetParameter&lt;int?&gt; (Convert.ChangeType cannot target Nullable&lt;T&gt;).
        var page = GetParameter<int>(parameters, "page", -1);
        return page < 0 ? null : page;
    }
}
