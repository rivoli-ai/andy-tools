using System.Collections.Generic;
using Andy.Tools.Core.OutputLimiting;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Andy.Tools.Tests.Core.OutputLimiting;

/// <summary>
/// Regression tests for issue #30: strongly-typed lists were not recognized for item-count limiting
/// (only IList&lt;object&gt; was), and truncation could throw at a tiny MaxCharacters.
/// </summary>
public class OutputLimiterTypedListAndJsonTests
{
    private readonly ToolOutputLimiter _limiter = new(Options.Create(new ToolOutputLimiterOptions
    {
        MaxOutputCharacters = 1000,
        MaxFileListEntries = 10,
        MaxFileListCharacters = 100_000
    }));

    [Fact]
    public void NeedsLimiting_TypedListExceedingItemCount_ReturnsTrue()
    {
        // List<string> is NOT IList<object>; previously this slipped past the item-count check.
        var list = new List<string>();
        for (var i = 0; i < 25; i++) { list.Add($"item-{i}"); }

        _limiter.NeedsLimiting(list, OutputType.FileList).Should().BeTrue();
    }

    [Fact]
    public void LimitOutput_TinyMaxCharacters_DoesNotThrow()
    {
        // maxChars - 20 was negative for small caps, throwing ArgumentOutOfRangeException in Substring.
        var text = new string('x', 5000);
        var context = new OutputLimitContext { MaxCharacters = 5 };

        var act = () => _limiter.LimitOutput(text, OutputType.Text, context);

        act.Should().NotThrow();
    }

    [Fact]
    public void LimitOutput_StructuredDataWithEscapedQuotes_DoesNotThrow()
    {
        // A string value containing an escaped backslash followed by a quote exercised the broken
        // single-char escape detection in the JSON truncation path.
        var data = new Dictionary<string, object?>
        {
            ["note"] = new string('a', 2000) + "\\\" trailing",
            ["more"] = new string('b', 2000)
        };
        var context = new OutputLimitContext { MaxCharacters = 100 };

        var act = () => _limiter.LimitOutput(data, OutputType.Text, context);
        act.Should().NotThrow();
    }
}
