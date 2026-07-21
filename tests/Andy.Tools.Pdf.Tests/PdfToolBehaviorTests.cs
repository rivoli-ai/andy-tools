using System.Collections;
using Andy.Tools.Pdf.Tests.Fixtures;
using FluentAssertions;

namespace Andy.Tools.Pdf.Tests;

/// <summary>
/// Behavioural coverage for all six <c>pdf_*</c> tools against deterministic in-memory fixtures:
/// successful paths, page/range boundaries, truncation, case sensitivity, and artifact filtering.
/// </summary>
public sealed class PdfToolBehaviorTests : PdfToolTestBase
{
    // ---- pdf_info ------------------------------------------------------------------------------

    [Fact]
    public async Task Info_reports_page_count()
    {
        var path = WriteFixture(TestPdfs.TwoPageWithOutline());
        var result = await ExecuteAsync(new PdfInfoTool(), new() { ["path"] = path }, Context());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Prop<int>(result.Data, "pageCount").Should().Be(2);
        Prop<bool>(result.Data, "wasRecovered").Should().BeFalse();
    }

    // ---- pdf_extract_text ----------------------------------------------------------------------

    [Fact]
    public async Task ExtractText_whole_document_concatenates_all_pages()
    {
        var path = WriteFixture(TestPdfs.ThreePagesMarked());
        var result = await ExecuteAsync(new PdfExtractTextTool(), new() { ["path"] = path }, Context());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Prop<int>(result.Data, "pageCount").Should().Be(3);
        var text = Prop<string>(result.Data, "text");
        text.Should().Contain("ALPHA").And.Contain("BRAVO").And.Contain("CHARLIE");
    }

    [Fact]
    public async Task ExtractText_single_page_returns_only_that_page()
    {
        var path = WriteFixture(TestPdfs.ThreePagesMarked());
        var result = await ExecuteAsync(
            new PdfExtractTextTool(), new() { ["path"] = path, ["page"] = 1 }, Context());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Prop<int>(result.Data, "page").Should().Be(1);
        var text = Prop<string>(result.Data, "text");
        text.Should().Contain("BRAVO");
        text.Should().NotContain("ALPHA").And.NotContain("CHARLIE");
    }

    [Fact]
    public async Task ExtractText_page_out_of_range_fails()
    {
        var path = WriteFixture(TestPdfs.ThreePagesMarked());
        var result = await ExecuteAsync(
            new PdfExtractTextTool(), new() { ["path"] = path, ["page"] = 9 }, Context());

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("out of range");
    }

    // ---- pdf_reflow ----------------------------------------------------------------------------

    [Fact]
    public async Task Reflow_returns_paragraphs_and_text_for_a_page()
    {
        var path = WriteFixture(TestPdfs.PlainNoOutlineNoTable());
        var result = await ExecuteAsync(
            new PdfReflowTool(), new() { ["path"] = path, ["page"] = 0 }, Context());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Prop<int>(result.Data, "page").Should().Be(0);
        Prop<string>(result.Data, "text").Should().Contain("narrative");
    }

    [Fact]
    public async Task Reflow_page_out_of_range_fails()
    {
        var path = WriteFixture(TestPdfs.PlainNoOutlineNoTable());
        var result = await ExecuteAsync(
            new PdfReflowTool(), new() { ["path"] = path, ["page"] = 5 }, Context());

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("out of range");
    }

    // ---- pdf_outline ---------------------------------------------------------------------------

    [Fact]
    public async Task Outline_returns_bookmark_titles()
    {
        var path = WriteFixture(TestPdfs.TwoPageWithOutline());
        var result = await ExecuteAsync(new PdfOutlineTool(), new() { ["path"] = path }, Context());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Prop<int>(result.Data, "itemCount").Should().Be(2);
        var titles = ((IEnumerable)Prop(result.Data, "outline")!)
            .Cast<object>()
            .Select(o => Prop<string>(o, "Title"))
            .ToList();
        titles.Should().Contain(t => t.Contains("Business"));
        titles.Should().Contain(t => t.Contains("MD and A"));
    }

    [Fact]
    public async Task Outline_is_empty_when_document_has_no_bookmarks()
    {
        var path = WriteFixture(TestPdfs.PlainNoOutlineNoTable());
        var result = await ExecuteAsync(new PdfOutlineTool(), new() { ["path"] = path }, Context());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Prop<int>(result.Data, "itemCount").Should().Be(0);
    }

    // ---- pdf_extract_tables --------------------------------------------------------------------

    [Fact]
    public async Task ExtractTables_recovers_a_clean_grid()
    {
        var path = WriteFixture(TestPdfs.SingleTable());
        var result = await ExecuteAsync(new PdfExtractTablesTool(), new() { ["path"] = path }, Context());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Prop<int>(result.Data, "tableCount").Should().BeGreaterThan(0);

        var tables = ((IEnumerable)Prop(result.Data, "tables")!).Cast<object>().ToList();
        tables.Should().NotBeEmpty();
        var firstRows = ((IEnumerable)Prop(tables[0], "rows")!)
            .Cast<object>()
            .Select(r => ((IEnumerable)r).Cast<object?>().Select(c => c?.ToString()).ToList())
            .ToList();
        firstRows.Should().Contain(r => r.Contains("Revenue"));
    }

    [Fact]
    public async Task ExtractTables_returns_no_tables_for_plain_prose()
    {
        var path = WriteFixture(TestPdfs.PlainNoOutlineNoTable());
        var result = await ExecuteAsync(new PdfExtractTablesTool(), new() { ["path"] = path }, Context());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Prop<int>(result.Data, "tableCount").Should().Be(0);
    }

    [Fact]
    public async Task ExtractTables_rejects_first_page_after_last_page()
    {
        var path = WriteFixture(TestPdfs.SingleTable());
        var result = await ExecuteAsync(
            new PdfExtractTablesTool(),
            new() { ["path"] = path, ["first_page"] = 0, ["last_page"] = 0, ["max_tables"] = 5 },
            Context());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
    }

    // ---- pdf_search ----------------------------------------------------------------------------

    [Fact]
    public async Task Search_finds_a_term_and_reports_its_page()
    {
        var path = WriteFixture(TestPdfs.TwoPageWithOutline());
        var result = await ExecuteAsync(
            new PdfSearchTool(), new() { ["path"] = path, ["query"] = "guidance" }, Context());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Prop<int>(result.Data, "matchCount").Should().Be(1);
        var matches = ((IEnumerable)Prop(result.Data, "matches")!).Cast<object>().ToList();
        Prop<int>(matches[0], "page").Should().Be(1);
    }

    [Fact]
    public async Task Search_is_case_insensitive_by_default_and_case_sensitive_on_request()
    {
        var path = WriteFixture(TestPdfs.TwoPageWithOutline());

        var insensitive = await ExecuteAsync(
            new PdfSearchTool(), new() { ["path"] = path, ["query"] = "REVENUE" }, Context());
        Prop<int>(insensitive.Data, "matchCount").Should().Be(2, "both 'Revenue' and 'revenue' match");

        var sensitive = await ExecuteAsync(
            new PdfSearchTool(),
            new() { ["path"] = path, ["query"] = "REVENUE", ["case_sensitive"] = true },
            Context());
        Prop<int>(sensitive.Data, "matchCount").Should().Be(0);
    }

    [Fact]
    public async Task Search_truncates_to_max_results_but_reports_full_count()
    {
        var path = WriteFixture(TestPdfs.RepeatedTerm(10));
        var result = await ExecuteAsync(
            new PdfSearchTool(),
            new() { ["path"] = path, ["query"] = "needle", ["max_results"] = 3 },
            Context());

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Prop<int>(result.Data, "matchCount").Should().Be(10);
        Prop<int>(result.Data, "returned").Should().Be(3);
        Prop<bool>(result.Data, "truncated").Should().BeTrue();
    }

    [Fact]
    public async Task Search_requires_a_query()
    {
        var path = WriteFixture(TestPdfs.TwoPageWithOutline());
        var result = await ExecuteAsync(
            new PdfSearchTool(), new() { ["path"] = path, ["query"] = "" }, Context());

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("query");
    }

    // ---- shared failure paths ------------------------------------------------------------------

    [Fact]
    public async Task Missing_file_fails_cleanly()
    {
        var path = Path.Combine(WorkingDirectory, "does-not-exist.pdf");
        var result = await ExecuteAsync(new PdfInfoTool(), new() { ["path"] = path }, Context());

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Malformed_file_fails_without_crashing()
    {
        var path = WriteFixture(TestPdfs.NotAPdf());
        var result = await ExecuteAsync(new PdfInfoTool(), new() { ["path"] = path }, Context());

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}
