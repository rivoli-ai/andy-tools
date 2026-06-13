using System.Text.Json;
using Andy.Tools.Core;
using Andy.Tools.Library.Web;
using FluentAssertions;

namespace Andy.Tools.Tests.Library.Web;

/// <summary>
/// Regression tests for issue #21: malformed CSV (unescaped delimiters/quotes), a shallow merge
/// mislabeled "deep_merge", and a diff that only ever reported a top-level type difference.
/// </summary>
public class JsonProcessorToolTests
{
    private static async Task<ToolResult> Run(Dictionary<string, object?> p)
    {
        var tool = new JsonProcessorTool();
        await tool.InitializeAsync();
        return await tool.ExecuteAsync(p, new ToolExecutionContext());
    }

    [Fact]
    public async Task ToCsv_QuotesFieldsContainingDelimiterOrQuote()
    {
        var json = """[{"name":"Doe, John","note":"say \"hi\""}]""";
        var result = await Run(new() { ["operation"] = "to_csv", ["json_input"] = json });

        result.IsSuccessful.Should().BeTrue();
        var csv = (string)result.Data!;
        // The comma-containing value and the quote-containing value must both be quoted/escaped.
        csv.Should().Contain("\"Doe, John\"");
        csv.Should().Contain("\"say \"\"hi\"\"\"");
    }

    [Fact]
    public async Task Merge_IsDeep_NestedObjectsAreMergedNotReplaced()
    {
        var target = """{"a":{"x":1,"y":2},"b":1}""";
        var source = """{"a":{"y":3,"z":4}}""";
        var result = await Run(new() { ["operation"] = "merge", ["json_input"] = target, ["merge_json"] = source });

        result.IsSuccessful.Should().BeTrue();
        using var doc = JsonDocument.Parse((string)result.Data!);
        var a = doc.RootElement.GetProperty("a");
        a.GetProperty("x").GetInt32().Should().Be(1, "deep merge must keep target-only nested keys");
        a.GetProperty("y").GetInt32().Should().Be(3, "source overrides overlapping keys");
        a.GetProperty("z").GetInt32().Should().Be(4, "source-only nested keys are added");
        doc.RootElement.GetProperty("b").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Diff_DetectsNestedValueDifferences()
    {
        var left = """{"a":1,"nested":{"k":"old"}}""";
        var right = """{"a":1,"nested":{"k":"new"}}""";
        var result = await Run(new() { ["operation"] = "diff", ["json_input"] = left, ["merge_json"] = right });

        result.IsSuccessful.Should().BeTrue();
        ((int)result.Metadata["differences_found"]!).Should().BeGreaterThan(0);
        ((string)result.Data!).Should().Contain("nested.k");
    }

    [Fact]
    public async Task Diff_IdenticalDocuments_ReportsNoDifferences()
    {
        var doc = """{"a":1,"b":[1,2,3]}""";
        var result = await Run(new() { ["operation"] = "diff", ["json_input"] = doc, ["merge_json"] = doc });

        result.IsSuccessful.Should().BeTrue();
        ((int)result.Metadata["differences_found"]!).Should().Be(0);
    }
}
