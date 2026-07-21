namespace Andy.Tools.Pdf.Tests.Fixtures;

/// <summary>
/// Named, deterministic PDF fixtures shared across the test suite. Each property returns freshly
/// built bytes so tests can never mutate a shared instance.
/// </summary>
public static class TestPdfs
{
    /// <summary>
    /// A two-page prose document with an outline. Page 0 mentions "Revenue" once; page 1 mentions
    /// "revenue" (lower case) once and "guidance" once — used for search / case-sensitivity tests.
    /// </summary>
    public static byte[] TwoPageWithOutline() => PdfBuilder.Build(
        new[]
        {
            PdfBuilder.Lines("Page one heading", "Revenue grew this year", "Segment overview"),
            PdfBuilder.Lines("Page two heading", "Full-year revenue and guidance", "Outlook remains stable"),
        },
        new[]
        {
            new PdfBuilder.OutlineEntry("Item 1 Business", 0),
            new PdfBuilder.OutlineEntry("Item 7 MD and A", 1),
        });

    /// <summary>A single-page document containing one clean 4×3 financial table.</summary>
    public static byte[] SingleTable() => PdfBuilder.Build(
        new[]
        {
            PdfBuilder.Grid(new[]
            {
                new[] { "Item", "2023", "2022" },
                new[] { "Revenue", "1000", "900" },
                new[] { "Cost of sales", "400", "350" },
                new[] { "Net income", "600", "550" },
            }),
        });

    /// <summary>A single-page prose document with no outline and no tabular content.</summary>
    public static byte[] PlainNoOutlineNoTable() => PdfBuilder.Build(
        new[]
        {
            PdfBuilder.Lines(
                "This document is plain narrative text.",
                "It has no bookmarks and no tables.",
                "Just a few ordinary sentences."),
        });

    /// <summary>A document whose pages each carry a distinct marker, for page-boundary tests.</summary>
    public static byte[] ThreePagesMarked() => PdfBuilder.Build(
        new[]
        {
            PdfBuilder.Lines("ALPHA page zero"),
            PdfBuilder.Lines("BRAVO page one"),
            PdfBuilder.Lines("CHARLIE page two"),
        });

    /// <summary>A page containing the same token many times, for match-truncation tests.</summary>
    public static byte[] RepeatedTerm(int occurrences)
    {
        var line = string.Join(" ", Enumerable.Repeat("needle", occurrences));
        return PdfBuilder.Build(new[] { PdfBuilder.Lines(line) });
    }

    /// <summary>Bytes that are not a PDF at all, for malformed-input tests.</summary>
    public static byte[] NotAPdf() =>
        System.Text.Encoding.ASCII.GetBytes("This is definitely not a PDF file.");
}
