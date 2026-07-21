using Andy.Tools.Core;
using Andy.Tools.Framework;
using Andy.Tools.Library;
using Andy.Tools.Pdf.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Pdf.Tests;

/// <summary>
/// Document-wide <c>pdf_*</c> operations must observe the execution context's cancellation token,
/// both on explicit cancellation and when the executor cancels its linked token on a timeout.
/// </summary>
public sealed class PdfCancellationTests : PdfToolTestBase
{
    // A document large enough that whole-document work cannot complete before an immediate timeout.
    private static byte[] LargeDocument()
    {
        var pages = Enumerable.Range(0, 400)
            .Select(i => PdfBuilder.Lines(
                $"Page {i} needle heading", "Some filler prose for this page.", "Revenue and guidance."))
            .ToList();
        return PdfBuilder.Build(pages);
    }

    [Fact]
    public async Task ExtractText_whole_document_honors_a_pre_cancelled_token()
    {
        var path = WriteFixture(LargeDocument());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await ExecuteAsync(
            new PdfExtractTextTool(), new() { ["path"] = path }, Context(cancellationToken: cts.Token));

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    [Fact]
    public async Task Search_honors_a_pre_cancelled_token()
    {
        var path = WriteFixture(LargeDocument());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await ExecuteAsync(
            new PdfSearchTool(),
            new() { ["path"] = path, ["query"] = "needle" },
            Context(cancellationToken: cts.Token));

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    [Fact]
    public async Task ExtractTables_honors_a_pre_cancelled_token()
    {
        var path = WriteFixture(LargeDocument());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await ExecuteAsync(
            new PdfExtractTablesTool(), new() { ["path"] = path }, Context(cancellationToken: cts.Token));

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    [Fact]
    public async Task Single_page_extract_completes_even_with_a_live_token()
    {
        // A cheap single-page read against a non-cancelled token must still succeed — cancellation
        // support must not turn into spurious failures on the happy path.
        var path = WriteFixture(TestPdfs.ThreePagesMarked());
        using var cts = new CancellationTokenSource();

        var result = await ExecuteAsync(
            new PdfExtractTextTool(),
            new() { ["path"] = path, ["page"] = 0 },
            Context(cancellationToken: cts.Token));

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
    }

    [Fact]
    public async Task Short_executor_timeout_stops_document_wide_extraction()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAndyTools();
        services.AddAndyPdfTools();
        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IToolLifecycleManager>().InitializeAsync();
        var executor = provider.GetRequiredService<IToolExecutor>();

        var path = WriteFixture(LargeDocument());
        var request = new ToolExecutionRequest
        {
            ToolId = "pdf_extract_text",
            Parameters = new Dictionary<string, object?> { ["path"] = path },
            TimeoutMs = 1,
            Context = new ToolExecutionContext
            {
                WorkingDirectory = WorkingDirectory,
                Permissions = new ToolPermissions { AllowedPaths = new HashSet<string> { WorkingDirectory } },
            },
        };

        var result = await executor.ExecuteAsync(request);

        result.IsSuccessful.Should().BeFalse(
            "a 1 ms timeout cannot let a 400-page whole-document extraction finish");
    }
}
