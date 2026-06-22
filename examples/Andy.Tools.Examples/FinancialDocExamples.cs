using System.Text.Json;
using Andy.Doc;
using Andy.Doc.Model;
using Andy.Doc.Pdf;
using Andy.Doc.Styling;
using Andy.Tools.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Examples;

/// <summary>
/// Demonstrates the Andy.Tools.Pdf tools (<c>pdf_*</c>) by driving them through the same
/// <see cref="IToolExecutor"/> the engine and CLI use. The scenario mirrors "understand a
/// company 10-K or earnings-call transcript": locate a topic, read the relevant prose, and
/// pull the financial tables out as structured data.
///
/// By default it downloads Chevron's real 2023 annual report (Form 10-K, ~11 MB, 114 pages) to a
/// temp cache and runs the tools against it. Pass a PDF path as the second CLI argument
/// (e.g. <c>dotnet run pdf /path/to/AAPL-10K.pdf</c>) to use a different filing. If the download
/// fails (e.g. offline), it falls back to a small generated demo 10-K so the example still runs.
/// </summary>
public static class FinancialDocExamples
{
    // Chevron Corporation — 2023 Annual Report (Form 10-K), as published on annualreports.com.
    private const string Chevron10KUrl =
        "https://www.annualreports.com/HostedData/AnnualReportArchive/c/NYSE_CVX_2023.pdf";

    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Financial Document (PDF) Examples ===\n");

        var executor = serviceProvider.GetRequiredService<IToolExecutor>();

        // Use a real filing: an explicit path argument, otherwise the cached Chevron 10-K download,
        // otherwise a small generated stand-in so the example always runs.
        var cmdArgs = Environment.GetCommandLineArgs();
        var pdfPath = cmdArgs.Length > 2 && File.Exists(cmdArgs[2])
            ? cmdArgs[2]
            : await TryDownloadChevron10K() ?? GenerateDemoTenK();

        Console.WriteLine($"Document: {pdfPath}\n");

        // 1. Metadata + size. Always cheap; good first call before extracting a large filing.
        var info = await RunTool(executor, "pdf_info", new() { ["path"] = pdfPath });
        var pageCount = info?.TryGetProperty("pageCount", out var pc) == true ? pc.GetInt32() : 0;

        // 2. Outline (bookmark) tree — for a real 10-K this lists the sections so the model can
        //    jump straight to one (e.g. "Financial and operating highlights").
        await RunTool(executor, "pdf_outline", new() { ["path"] = pdfPath });

        // 3. Find where a topic is discussed before reading it.
        var search = await RunTool(executor, "pdf_search", new()
        {
            ["path"] = pdfPath,
            ["query"] = "revenue",
            ["max_results"] = 5,
        });

        // 4. Read the prose on the page where the topic first appears (reading-order reflow handles
        //    the multi-column layout of a real filing). Fall back to page 0.
        var hitPage = FirstMatchPage(search) ?? 0;
        await RunTool(executor, "pdf_reflow", new() { ["path"] = pdfPath, ["page"] = hitPage });

        // 5. Pull the financial statements out as rows of cells — ready to hand to a model or to the
        //    dataframe tools for YoY math. On a large filing, bound the work (and memory) to the
        //    statements section with first_page / last_page instead of scanning all 100+ pages.
        if (pageCount > 40)
        {
            // Heuristic: the consolidated statements sit in the back third of a 10-K.
            await RunTool(executor, "pdf_extract_tables", new()
            {
                ["path"] = pdfPath,
                ["first_page"] = pageCount * 2 / 3,
                ["last_page"] = pageCount - 1,
                ["max_tables"] = 5,
            });
        }
        else
        {
            await RunTool(executor, "pdf_extract_tables", new() { ["path"] = pdfPath, ["max_tables"] = 5 });
        }

        Console.WriteLine("\nTypical agent flow: pdf_info → pdf_outline → pdf_search(\"guidance\") "
            + "→ pdf_reflow(hit page) → pdf_extract_tables → ask the model to summarize / compute.");
    }

    /// <summary>Runs a tool, prints a truncated JSON view, and returns the parsed result data.</summary>
    private static async Task<JsonElement?> RunTool(
        IToolExecutor executor, string toolId, Dictionary<string, object?> parameters)
    {
        Console.WriteLine($"--- {toolId} ---");
        var result = await executor.ExecuteAsync(toolId, parameters);
        if (!result.IsSuccessful)
        {
            Console.WriteLine($"  failed: {result.ErrorMessage}\n");
            return null;
        }

        var json = JsonSerializer.Serialize(result.Data, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(Truncate(json, 1600));
        Console.WriteLine();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    /// <summary>Reads <c>matches[0].page</c> from a pdf_search result, if any.</summary>
    private static int? FirstMatchPage(JsonElement? searchResult)
    {
        if (searchResult is { } data
            && data.TryGetProperty("matches", out var matches)
            && matches.ValueKind == JsonValueKind.Array
            && matches.GetArrayLength() > 0
            && matches[0].TryGetProperty("page", out var page))
        {
            return page.GetInt32();
        }

        return null;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + $"\n  … ({s.Length - max} more chars)";

    /// <summary>
    /// Downloads Chevron's 2023 Form 10-K to a temp cache (skipping the download if already cached).
    /// Returns the path, or <c>null</c> when offline / the fetch fails.
    /// </summary>
    private static async Task<string?> TryDownloadChevron10K()
    {
        var cache = Path.Combine(Path.GetTempPath(), "chevron-2023-10k.pdf");
        if (File.Exists(cache) && new FileInfo(cache).Length > 1_000_000)
        {
            return cache;
        }

        try
        {
            Console.WriteLine("Downloading Chevron 2023 10-K (~11 MB)…");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("rivoli-andy-tools-example");
            var bytes = await http.GetByteArrayAsync(Chevron10KUrl);
            await File.WriteAllBytesAsync(cache, bytes);
            return cache;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  (could not download the Chevron 10-K: {ex.Message}; using a generated demo)");
            return null;
        }
    }

    /// <summary>
    /// Builds a tiny, deterministic PDF shaped like a 10-K excerpt: an MD&amp;A heading, a
    /// revenue paragraph, and a condensed income-statement table. Used as an offline fallback.
    /// </summary>
    private static string GenerateDemoTenK()
    {
        var doc = Document.Create("Demo 10-K Excerpt", "Andy.Tools Examples");
        doc.DefaultPageSize = PageSize.A4;
        doc.DefaultMargin = Margin.Standard;

        var section = doc.AddSection();

        section.Add(new Paragraph("Item 7. Management's Discussion and Analysis")
        {
            Style = new Style { FontSize = 16, FontWeight = FontWeight.Bold, SpaceAfter = 10 },
        });

        section.Add(new Paragraph(
            "Total net revenue increased 8% to $394.3 billion in fiscal 2024, driven by growth in "
            + "Services and continued demand for the Company's products. Gross margin expanded on a "
            + "favorable product mix, and operating cash flow remained strong, supporting continued "
            + "investment and capital returns."));

        section.Add(new Paragraph("Condensed Consolidated Statements of Operations (in millions):")
        {
            Style = new Style { FontWeight = FontWeight.Bold, SpaceBefore = 8, SpaceAfter = 6 },
        });

        var table = new Table();
        table.AddHeaderRow("Metric", "FY2024", "FY2023");
        table.AddRow(new TableRow().AddCells("Net revenue", "394,328", "365,817"));
        table.AddRow(new TableRow().AddCells("Cost of sales", "210,352", "199,022"));
        table.AddRow(new TableRow().AddCells("Gross margin", "183,976", "166,795"));
        table.AddRow(new TableRow().AddCells("Operating income", "123,216", "108,949"));
        table.AddRow(new TableRow().AddCells("Net income", "101,233", "96,995"));
        section.Add(table);

        var outPath = Path.Combine(Path.GetTempPath(), "andy-tools-demo-10k.pdf");
        doc.SavePdf(outPath);
        return outPath;
    }
}
