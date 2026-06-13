using System.Diagnostics;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;
using Andy.Tools.Library.Text;
using FluentAssertions;

namespace Andy.Tools.Tests.Library.Common;

/// <summary>
/// Regression tests for issue #14: user regexes ran with no timeout (ReDoS) and glob-to-regex
/// translation left metacharacters active (`.` matched any char; invalid patterns threw).
/// </summary>
public class GlobAndRegexSafetyTests
{
    [Theory]
    [InlineData("file.log", "file.log", true)]
    [InlineData("fileXlog", "file.log", false)] // literal dot must NOT match any char
    [InlineData("notes.txt", "*.txt", true)]
    [InlineData("notes.md", "*.txt", false)]
    [InlineData("a1c", "a?c", true)]
    [InlineData("ac", "a?c", false)]
    public void IsGlobMatch_TreatsNonWildcardsLiterally(string name, string pattern, bool expected)
    {
        ToolHelpers.IsGlobMatch(name, pattern).Should().Be(expected);
    }

    [Fact]
    public void IsGlobMatch_RegexMetacharacterPattern_DoesNotThrow()
    {
        // A pattern with regex metacharacters used to throw RegexParseException; now it is matched literally.
        Action act = () => ToolHelpers.IsGlobMatch("a(b[c", "a(b[c");
        act.Should().NotThrow();
        ToolHelpers.IsGlobMatch("a(b[c", "a(b[c").Should().BeTrue();
    }

    [Fact]
    public async Task SearchText_CatastrophicRegex_TimesOutInsteadOfHanging()
    {
        var tool = new SearchTextTool();
        await tool.InitializeAsync();

        // Classic catastrophic-backtracking pattern against a non-matching input.
        var parameters = new Dictionary<string, object?>
        {
            ["text"] = new string('a', 40) + "!",
            ["search_pattern"] = "(a+)+$",
            ["search_type"] = "regex"
        };

        var sw = Stopwatch.StartNew();
        var result = await tool.ExecuteAsync(parameters, new ToolExecutionContext());
        sw.Stop();

        // Must not hang: a 2s match timeout bounds it (well under this generous ceiling).
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
        // Either it completed quickly with no match, or it surfaced a clean failure — never a hang.
        result.Should().NotBeNull();
    }
}
