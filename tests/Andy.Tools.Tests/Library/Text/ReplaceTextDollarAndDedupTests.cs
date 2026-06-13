using Andy.Tools.Core;
using Andy.Tools.Library.Text;
using FluentAssertions;

namespace Andy.Tools.Tests.Library.Text;

/// <summary>
/// Regression tests for issue #20: literal replacements mis-handled '$' (treated as a regex substitution
/// token), and overlapping file_patterns processed the same file twice (double-applying replacements).
/// </summary>
public sealed class ReplaceTextDollarAndDedupTests : IDisposable
{
    private readonly string _dir;

    public ReplaceTextDollarAndDedupTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "andy_repl_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public async Task LiteralReplacement_WithDollarSign_IsNotTreatedAsSubstitution()
    {
        var file = Path.Combine(_dir, "price.txt");
        await File.WriteAllTextAsync(file, "PRICE here");

        var tool = new ReplaceTextTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["target_path"] = file,
            ["search_pattern"] = "PRICE",
            ["replacement_text"] = "$100",  // literal '$' must survive
            ["search_type"] = "contains",
            ["create_backup"] = false
        }, new ToolExecutionContext { WorkingDirectory = _dir });

        result.IsSuccessful.Should().BeTrue();
        (await File.ReadAllTextAsync(file)).Should().Be("$100 here");
    }

    [Fact]
    public async Task OverlappingFilePatterns_DoNotApplyReplacementTwice()
    {
        var file = Path.Combine(_dir, "doc.txt");
        await File.WriteAllTextAsync(file, "aa");

        var tool = new ReplaceTextTool();
        await tool.InitializeAsync();
        // Both patterns match doc.txt; without de-dup the replacement would run twice ("bb" then "cc"...
        // actually "a"->"b" twice yields "bb" once but counted twice; use a pattern that compounds).
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["target_path"] = _dir,
            ["search_pattern"] = "a",
            ["replacement_text"] = "aa", // if applied twice, "aa"->"aaaa"->"aaaaaaaa"
            ["search_type"] = "contains",
            ["file_patterns"] = new List<string> { "*.txt", "doc.*" },
            ["create_backup"] = false
        }, new ToolExecutionContext { WorkingDirectory = _dir });

        result.IsSuccessful.Should().BeTrue();
        // Each 'a' -> 'aa' exactly once: "aa" (2 a's) -> "aaaa" (4 a's). Double-processing would give 8.
        (await File.ReadAllTextAsync(file)).Should().Be("aaaa");
    }
}
