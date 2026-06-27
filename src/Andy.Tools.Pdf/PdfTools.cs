using Andy.Doc.Pdf.Content;
using Andy.Tools.Core;

namespace Andy.Tools.Pdf;

/// <summary>
/// <c>pdf_info</c> — document metadata (title, author, producer, dates) and page count.
/// Cheap; call it first to size a document before extracting large amounts of text.
/// </summary>
public sealed class PdfInfoTool : PdfToolBase
{
    public override ToolMetadata Metadata => new()
    {
        Id = "pdf_info",
        Name = "PDF Info",
        Description = "Returns a PDF's metadata (title, author, producer, dates) and its page count.",
        Category = ToolCategory.General,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
        Tags = new List<string> { "pdf", "document", "metadata" },
        Parameters = new List<ToolParameter> { PathParameter },
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        using var pdf = OpenPdf(parameters, context);
        var info = pdf.GetInfo();
        return Task.FromResult(ToolResult.Success(new
        {
            pageCount = pdf.PageCount,
            wasRecovered = pdf.WasRecovered,
            title = info.Title,
            author = info.Author,
            subject = info.Subject,
            keywords = info.Keywords,
            creator = info.Creator,
            producer = info.Producer,
            creationDate = info.CreationDate,
            modDate = info.ModDate,
        }));
    }
}

/// <summary>
/// <c>pdf_extract_text</c> — plain text in show order, for the whole document or one page.
/// Best for linear prose such as earnings-call transcripts.
/// </summary>
public sealed class PdfExtractTextTool : PdfToolBase
{
    public override ToolMetadata Metadata => new()
    {
        Id = "pdf_extract_text",
        Name = "PDF Extract Text",
        Description = "Extracts plain text from a PDF. Omit 'page' for the whole document, or pass a "
            + "0-based page index for a single page. Pages are separated by form feeds.",
        Category = ToolCategory.General,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
        Tags = new List<string> { "pdf", "text", "extraction" },
        Parameters = new List<ToolParameter>
        {
            PathParameter,
            new()
            {
                Name = "page",
                Type = "integer",
                Description = "0-based page index. Omit to extract the entire document.",
                Required = false,
                MinValue = 0,
            },
        },
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        using var pdf = OpenPdf(parameters, context);
        var page = GetOptionalPage(parameters);

        if (page is int p)
        {
            if (p >= pdf.PageCount)
            {
                return Task.FromResult(ToolResult.Failure(
                    $"Page {p} is out of range; the document has {pdf.PageCount} page(s)."));
            }

            return Task.FromResult(ToolResult.Success(new
            {
                pageCount = pdf.PageCount,
                page = p,
                text = pdf.ExtractText(p),
            }));
        }

        return Task.FromResult(ToolResult.Success(new
        {
            pageCount = pdf.PageCount,
            text = pdf.ExtractAllText(),
        }));
    }
}

/// <summary>
/// <c>pdf_reflow</c> — reading-order paragraphs for one page. Reconstructs columns and paragraph
/// breaks, so it is preferable to raw extraction for multi-column filings such as 10-Ks.
/// </summary>
public sealed class PdfReflowTool : PdfToolBase
{
    public override ToolMetadata Metadata => new()
    {
        Id = "pdf_reflow",
        Name = "PDF Reflow Page",
        Description = "Reconstructs a single page into reading-order paragraphs (handles multi-column "
            + "layouts). Returns the paragraphs and the joined plain text.",
        Category = ToolCategory.General,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
        Tags = new List<string> { "pdf", "text", "reflow", "layout" },
        Parameters = new List<ToolParameter>
        {
            PathParameter,
            new()
            {
                Name = "page",
                Type = "integer",
                Description = "0-based page index to reflow (default 0).",
                Required = false,
                DefaultValue = 0,
                MinValue = 0,
            },
        },
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        using var pdf = OpenPdf(parameters, context);
        var page = GetParameter<int>(parameters, "page", 0);
        if (page < 0 || page >= pdf.PageCount)
        {
            return Task.FromResult(ToolResult.Failure(
                $"Page {page} is out of range; the document has {pdf.PageCount} page(s)."));
        }

        var reflowed = pdf.ReflowPage(page);
        var paragraphs = reflowed.Paragraphs.Select(par => par.Text).ToList();
        return Task.FromResult(ToolResult.Success(new
        {
            pageCount = pdf.PageCount,
            page,
            paragraphCount = paragraphs.Count,
            paragraphs,
            text = reflowed.PlainText,
        }));
    }
}

/// <summary>
/// <c>pdf_outline</c> — the document outline (bookmark) tree. For a 10-K this surfaces the
/// "Item 1A. Risk Factors", "Item 7. MD&amp;A", etc. structure for navigation.
/// </summary>
public sealed class PdfOutlineTool : PdfToolBase
{
    public override ToolMetadata Metadata => new()
    {
        Id = "pdf_outline",
        Name = "PDF Outline",
        Description = "Returns the PDF outline (bookmark) tree as nested titles. Empty when the "
            + "document has no bookmarks.",
        Category = ToolCategory.General,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
        Tags = new List<string> { "pdf", "outline", "bookmarks", "navigation" },
        Parameters = new List<ToolParameter> { PathParameter },
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        using var pdf = OpenPdf(parameters, context);
        var outline = pdf.GetOutline().Select(Map).ToList();
        return Task.FromResult(ToolResult.Success(new
        {
            itemCount = outline.Count,
            outline,
        }));
    }

    private static OutlineNode Map(PdfOutlineItem item) => new()
    {
        Title = item.Title,
        Children = item.Children.Select(Map).ToList(),
    };

    private sealed class OutlineNode
    {
        public string Title { get; init; } = string.Empty;
        public List<OutlineNode> Children { get; init; } = new();
    }
}

/// <summary>
/// <c>pdf_extract_tables</c> — tabular content reconstructed from the document, as rows of cell
/// text. Useful for pulling financial statements out of a 10-K.
/// </summary>
public sealed class PdfExtractTablesTool : PdfToolBase
{
    public override ToolMetadata Metadata => new()
    {
        Id = "pdf_extract_tables",
        Name = "PDF Extract Tables",
        Description = "Detects tables page by page and returns them as rows of cell text "
            + "(e.g. financial statements in a 10-K). Restrict the work — and memory — to a page "
            + "range with first_page / last_page.",
        Category = ToolCategory.General,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
        Tags = new List<string> { "pdf", "tables", "extraction", "financial" },
        Parameters = new List<ToolParameter>
        {
            PathParameter,
            new()
            {
                Name = "first_page",
                Type = "integer",
                Description = "0-based first page to scan (default 0).",
                Required = false,
                DefaultValue = 0,
                MinValue = 0,
            },
            new()
            {
                Name = "last_page",
                Type = "integer",
                Description = "0-based last page to scan, inclusive. Omit to scan to the end of the "
                    + "document. Scan a narrow range on large filings to bound memory and time.",
                Required = false,
                MinValue = 0,
            },
            new()
            {
                Name = "max_tables",
                Type = "integer",
                Description = "Maximum number of tables to return (default 50).",
                Required = false,
                DefaultValue = 50,
                MinValue = 1,
            },
            new()
            {
                Name = "include_layout_artifacts",
                Type = "boolean",
                Description = "Include low-quality tables that look like layout artifacts (rows of "
                    + "single characters from infographics). Default false.",
                Required = false,
                DefaultValue = false,
            },
        },
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        using var pdf = OpenPdf(parameters, context);
        var maxTables = GetParameter<int>(parameters, "max_tables", 50);
        var includeArtifacts = GetParameter<bool>(parameters, "include_layout_artifacts", false);

        var firstPage = GetParameter<int>(parameters, "first_page", 0);
        var lastPage = GetParameter<int>(parameters, "last_page", -1);
        if (lastPage < 0 || lastPage > pdf.PageCount - 1)
        {
            lastPage = pdf.PageCount - 1;
        }

        if (firstPage > lastPage)
        {
            return Task.FromResult(ToolResult.Failure(
                $"first_page ({firstPage}) is after last_page ({lastPage}); the document has "
                + $"{pdf.PageCount} page(s)."));
        }

        var candidates = new List<(int Page, List<List<string>> Rows, int Cols, long Score)>();
        var skippedArtifacts = 0;

        // Per-page detection over the positioned content. This avoids the whole-document DOM
        // reconstruction (ToDocument), so memory stays bounded to a single page — and to just the
        // requested range. The PageContent overload clusters X coordinates into columns, handling
        // both wide multi-space gaps (real filings) and single-space gaps (library-generated PDFs).
        for (var page = firstPage; page <= lastPage; page++)
        {
            var content = pdf.ImportPage(page);
            var structure = StructureInference.Infer(content);

            foreach (var table in structure.Tables)
            {
                var rows = table.Rows.Select(r => r.ToList()).ToList();
                var cols = rows.Count > 0 ? rows.Max(r => r.Count) : 0;

                // The inference over-detects "tables" on design-heavy pages: it shreds infographics
                // into single-character cells and prose paragraphs into wide rows of word-cells. Drop
                // both by default so callers get genuine tabular data.
                if (!includeArtifacts && IsLayoutArtifact(rows, cols))
                {
                    skippedArtifacts++;
                    continue;
                }

                // Rank by numeric density: a consolidated financial statement is many rows of figures,
                // so counting digit-bearing cells surfaces the statements above text/chart tables.
                var score = rows.Sum(r => r.Count(c => c.Any(char.IsDigit)));
                candidates.Add((page, rows, cols, score));
            }
        }

        // Most figure-dense tables first; then cap at max_tables.
        var ranked = candidates.OrderByDescending(c => c.Score).ToList();
        var tables = ranked
            .Take(maxTables)
            .Select(c => new
            {
                page = c.Page,
                rowCount = c.Rows.Count,
                columnCount = c.Cols,
                rows = c.Rows,
            })
            .ToList();

        return Task.FromResult(ToolResult.Success(new
        {
            pagesScanned = new { first = firstPage, last = lastPage },
            tableCount = ranked.Count,
            returned = tables.Count,
            truncated = ranked.Count > tables.Count,
            skippedArtifacts,
            note = "Tables are ranked by numeric density (the most figure-dense financial tables first).",
            tables,
        }));
    }

    /// <summary>
    /// Heuristic: a reconstructed "table" is a layout artifact rather than real tabular data when it
    /// has only one row, an implausibly wide column count (a paragraph shredded into word-cells), or
    /// when most of its non-empty cells are a single character (an infographic shredded into columns).
    /// </summary>
    private static bool IsLayoutArtifact(List<List<string>> rows, int cols)
    {
        if (rows.Count < 2 || cols > 12)
        {
            return true;
        }

        var nonEmpty = 0;
        var shortCells = 0;
        foreach (var row in rows)
        {
            foreach (var cell in row)
            {
                var text = cell.Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                nonEmpty++;
                if (text.Length <= 1)
                {
                    shortCells++;
                }
            }
        }

        // Need a few cells to judge; below that, keep the table rather than risk dropping a real one.
        return nonEmpty >= 6 && shortCells > nonEmpty * 0.5;
    }
}

/// <summary>
/// <c>pdf_search</c> — full-text search across the document, returning each match's page and a
/// surrounding snippet. Good for locating a topic (e.g. "guidance", "revenue") before extracting.
/// </summary>
public sealed class PdfSearchTool : PdfToolBase
{
    public override ToolMetadata Metadata => new()
    {
        Id = "pdf_search",
        Name = "PDF Search",
        Description = "Searches a PDF for a phrase and returns each match's page number and text. "
            + "Use it to find where a topic is discussed before extracting that page.",
        Category = ToolCategory.General,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
        Tags = new List<string> { "pdf", "search", "find" },
        Parameters = new List<ToolParameter>
        {
            PathParameter,
            new()
            {
                Name = "query",
                Type = "string",
                Description = "The phrase to search for.",
                Required = true,
            },
            new()
            {
                Name = "case_sensitive",
                Type = "boolean",
                Description = "Whether the search is case-sensitive (default false).",
                Required = false,
                DefaultValue = false,
            },
            new()
            {
                Name = "max_results",
                Type = "integer",
                Description = "Maximum number of matches to return (default 50).",
                Required = false,
                DefaultValue = 50,
                MinValue = 1,
            },
        },
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var query = GetParameter<string>(parameters, "query");
        if (string.IsNullOrEmpty(query))
        {
            return Task.FromResult(ToolResult.Failure("The 'query' parameter is required."));
        }

        var caseSensitive = GetParameter<bool>(parameters, "case_sensitive", false);
        var maxResults = GetParameter<int>(parameters, "max_results", 50);
        var comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        using var pdf = OpenPdf(parameters, context);

        // Search the importer's extracted text page by page. The lower-level PdfTextSearcher relies
        // on a text path that fails on many real-world font encodings (e.g. it returns zero matches
        // on a Chevron 10-K), whereas the importer's content interpreter decodes them correctly.
        var matches = new List<object>();
        var total = 0;

        for (var page = 0; page < pdf.PageCount; page++)
        {
            var text = pdf.ExtractText(page);
            var from = 0;
            while (true)
            {
                var idx = text.IndexOf(query, from, comparison);
                if (idx < 0)
                {
                    break;
                }

                total++;
                if (matches.Count < maxResults)
                {
                    matches.Add(new { page, snippet = Snippet(text, idx, query.Length) });
                }

                from = idx + query.Length;
            }
        }

        return Task.FromResult(ToolResult.Success(new
        {
            query,
            matchCount = total,
            returned = matches.Count,
            truncated = total > matches.Count,
            matches,
        }));
    }

    /// <summary>Returns the match plus a little surrounding context, with whitespace collapsed.</summary>
    private static string Snippet(string text, int matchIndex, int matchLength)
    {
        const int pad = 60;
        var start = Math.Max(0, matchIndex - pad);
        var end = Math.Min(text.Length, matchIndex + matchLength + pad);
        var slice = text[start..end].Replace('\n', ' ').Replace('\r', ' ');
        slice = string.Join(' ', slice.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var prefix = start > 0 ? "…" : string.Empty;
        var suffix = end < text.Length ? "…" : string.Empty;
        return prefix + slice + suffix;
    }
}
