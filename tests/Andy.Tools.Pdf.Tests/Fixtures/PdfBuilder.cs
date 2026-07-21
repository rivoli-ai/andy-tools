using System.Text;

namespace Andy.Tools.Pdf.Tests.Fixtures;

/// <summary>
/// Builds small, deterministic PDF documents entirely in memory so the test suite needs no
/// binary fixtures, no network access, and no separate PDF writer dependency. The output is a
/// minimal but structurally valid PDF 1.4 file (single-byte Helvetica text, a cross-reference
/// table, and an optional outline) that the Andy.Doc <c>PdfImporter</c> reads without recovery.
/// </summary>
/// <remarks>
/// Text is positioned absolutely via a text matrix so callers can lay out grids that the table
/// inference detects as real tables. Everything is pure and side-effect free, so the same call
/// always produces byte-identical output.
/// </remarks>
public static class PdfBuilder
{
    /// <summary>A single absolutely-positioned text run in PDF user space (origin bottom-left).</summary>
    public readonly record struct Run(double X, double Y, string Text);

    /// <summary>An outline (bookmark) entry pointing at a 0-based page index.</summary>
    public readonly record struct OutlineEntry(string Title, int Page);

    /// <summary>
    /// Builds a PDF from the given pages. Each page is a list of positioned text runs. An optional
    /// flat outline adds top-level bookmarks.
    /// </summary>
    public static byte[] Build(
        IReadOnlyList<IReadOnlyList<Run>> pages,
        IReadOnlyList<OutlineEntry>? outline = null)
    {
        ArgumentNullException.ThrowIfNull(pages);

        const int catalog = 1, pagesNode = 2, font = 3;
        var next = 4;

        var pageNums = new List<int>();
        var contentNums = new List<int>();
        foreach (var _ in pages)
        {
            pageNums.Add(next++);
            contentNums.Add(next++);
        }

        var outlineRoot = 0;
        var itemNums = new List<int>();
        if (outline is { Count: > 0 })
        {
            outlineRoot = next++;
            foreach (var _ in outline)
            {
                itemNums.Add(next++);
            }
        }

        var body = new Dictionary<int, string>
        {
            [catalog] = $"<< /Type /Catalog /Pages {pagesNode} 0 R"
                + (outlineRoot > 0 ? $" /Outlines {outlineRoot} 0 R" : string.Empty) + " >>",
            [pagesNode] = $"<< /Type /Pages /Count {pages.Count} "
                + $"/Kids [{string.Join(" ", pageNums.Select(n => n + " 0 R"))}] >>",
            [font] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        for (var i = 0; i < pages.Count; i++)
        {
            body[pageNums[i]] =
                $"<< /Type /Page /Parent {pagesNode} 0 R /MediaBox [0 0 612 792] "
                + $"/Resources << /Font << /F1 {font} 0 R >> >> /Contents {contentNums[i]} 0 R >>";

            var content = new StringBuilder();
            foreach (var run in pages[i])
            {
                content.Append(
                    $"BT /F1 10 Tf 1 0 0 1 {run.X:0.##} {run.Y:0.##} Tm ({Escape(run.Text)}) Tj ET\n");
            }

            var stream = content.ToString();
            body[contentNums[i]] = $"<< /Length {stream.Length} >>\nstream\n{stream}endstream";
        }

        if (outlineRoot > 0)
        {
            body[outlineRoot] =
                $"<< /Type /Outlines /First {itemNums[0]} 0 R /Last {itemNums[^1]} 0 R "
                + $"/Count {outline!.Count} >>";
            for (var i = 0; i < outline!.Count; i++)
            {
                var prev = i > 0 ? $" /Prev {itemNums[i - 1]} 0 R" : string.Empty;
                var nextItem = i < outline.Count - 1 ? $" /Next {itemNums[i + 1]} 0 R" : string.Empty;
                body[itemNums[i]] =
                    $"<< /Title ({Escape(outline[i].Title)}) /Parent {outlineRoot} 0 R{prev}{nextItem} "
                    + $"/Dest [{pageNums[outline[i].Page]} 0 R /Fit] >>";
            }
        }

        var count = next - 1;
        var bytes = new List<byte>();
        void Write(string s) => bytes.AddRange(Encoding.ASCII.GetBytes(s));

        Write("%PDF-1.4\n%âãÏÓ\n");
        var offsets = new int[count + 1];
        for (var n = 1; n <= count; n++)
        {
            offsets[n] = bytes.Count;
            Write($"{n} 0 obj\n{body[n]}\nendobj\n");
        }

        var xref = bytes.Count;
        Write($"xref\n0 {count + 1}\n0000000000 65535 f \n");
        for (var n = 1; n <= count; n++)
        {
            Write($"{offsets[n]:D10} 00000 n \n");
        }

        Write($"trailer\n<< /Size {count + 1} /Root {catalog} 0 R >>\nstartxref\n{xref}\n%%EOF");
        return bytes.ToArray();
    }

    /// <summary>
    /// Lays out a grid of rows × columns as positioned runs, evenly spaced. The regular column
    /// positions let the table-inference pass recover the grid as a table.
    /// </summary>
    public static IReadOnlyList<Run> Grid(
        string[][] rows, double x0 = 72, double y0 = 700, double columnWidth = 120, double rowHeight = 20)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var runs = new List<Run>();
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                runs.Add(new Run(x0 + (c * columnWidth), y0 - (r * rowHeight), rows[r][c]));
            }
        }

        return runs;
    }

    /// <summary>Lays out a column of single-line paragraphs top-to-bottom (no tabular structure).</summary>
    public static IReadOnlyList<Run> Lines(params string[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var runs = new List<Run>();
        for (var i = 0; i < lines.Length; i++)
        {
            runs.Add(new Run(72, 700 - (i * 16), lines[i]));
        }

        return runs;
    }

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
