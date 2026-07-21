using System.Text;
using Andy.Doc.Pdf.Content;
using Andy.Tools.Core;
using Andy.Tools.Library;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Pdf;

/// <summary>
/// Shared base for the <c>pdf_*</c> tools. Each tool reads a single PDF identified by a
/// <c>path</c> parameter; this base centralises safe-path resolution, filesystem-permission
/// enforcement, and opening the fully-managed <see cref="PdfImporter"/> so the concrete tools
/// stay declarative.
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
    /// The outcome of <see cref="OpenPdf"/>: either an open <see cref="Importer"/> that the caller
    /// owns and must dispose, or a <see cref="Failure"/> result to return unchanged. Exactly one of
    /// the two is non-null.
    /// </summary>
    protected readonly struct PdfOpenResult
    {
        /// <summary>The open importer on success; <c>null</c> when <see cref="Failure"/> is set.</summary>
        public PdfImporter? Importer { get; private init; }

        /// <summary>The failure to return on error; <c>null</c> on success.</summary>
        public ToolResult? Failure { get; private init; }

        internal static PdfOpenResult Ok(PdfImporter importer) => new() { Importer = importer };

        internal static PdfOpenResult Fail(string message) => new() { Failure = ToolResult.Failure(message) };
    }

    /// <summary>
    /// Resolves and validates the <c>path</c> parameter, enforces the execution context's filesystem
    /// permissions, and opens a <see cref="PdfImporter"/>. On any failure the PDF is neither opened
    /// nor parsed and a stable, non-leaky failure result is returned in
    /// <see cref="PdfOpenResult.Failure"/>.
    /// </summary>
    /// <remarks>
    /// Enforcement order — done before the file is opened — is: the requested path must resolve inside
    /// the working directory, must lie within the caller's <see cref="ToolPermissions.AllowedPaths"/>
    /// (when any are configured), and must not lie within its <see cref="ToolPermissions.BlockedPaths"/>.
    /// Blocked paths take precedence over allowed paths. Checks use canonical, symlink-resolved paths
    /// and a directory-boundary-aware comparison so a symlink inside an allowed directory cannot escape it.
    /// </remarks>
    protected static PdfOpenResult OpenPdf(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var rawPath = GetParameter<string>(parameters, "path");
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return PdfOpenResult.Fail("The 'path' parameter is required.");
        }

        string safePath;
        try
        {
            // Confines the path to the working directory and resolves it (symlink-aware).
            safePath = ToolHelpers.GetSafePath(rawPath, context.WorkingDirectory);
        }
        catch (ArgumentException)
        {
            // A path that escapes the working directory is treated the same as any other denied path
            // so the error does not reveal the working-directory layout.
            return PdfOpenResult.Fail("Access to the requested path is denied.");
        }

        // Enforce the caller's allowed/blocked boundaries before touching the file, so a denied path
        // is never opened or parsed and its existence is not revealed.
        if (!ToolHelpers.IsPathWithinAllowedPaths(safePath, context.Permissions)
            || ToolHelpers.IsPathBlocked(safePath, context.Permissions))
        {
            return PdfOpenResult.Fail("Access to the requested path is denied.");
        }

        if (!File.Exists(safePath))
        {
            return PdfOpenResult.Fail($"PDF file not found: {rawPath}");
        }

        // Open the file read-only and let the importer take ownership of the stream (leaveOpen: false),
        // so disposing the importer closes the file handle.
        var stream = new FileStream(safePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            return PdfOpenResult.Ok(new PdfImporter(stream, leaveOpen: false));
        }
        catch
        {
            stream.Dispose();
            throw;
        }
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

    /// <summary>
    /// Cancellation-aware whole-document text extraction. Behaves exactly like
    /// <see cref="PdfImporter.ExtractAllText"/> (pages joined by a form feed) but yields to
    /// <paramref name="cancellationToken"/> between pages, so a large or adversarial document can be
    /// stopped promptly when the executor cancels on a timeout or resource limit.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled during extraction.</exception>
    protected static string ExtractAllText(PdfImporter pdf, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        for (var page = 0; page < pdf.PageCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (page > 0)
            {
                builder.Append('\f');
            }

            builder.Append(pdf.ExtractText(page));
        }

        return builder.ToString();
    }
}
