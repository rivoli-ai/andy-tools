using System.Text.Json;
using Andy.Tools.Core;
using Andy.Tools.Library.Utilities;
using FluentAssertions;

namespace Andy.Tools.Tests.Library.Utilities;

/// <summary>
/// Regression tests for issue #22: timezone conversion mishandled DateTimeKind (throwing or silently
/// off-by-offset), and url_decode translated '+' to space asymmetrically with url_encode.
/// </summary>
public class DateTimeAndUrlFixTests
{
    private static async Task<ToolResult> RunDate(Dictionary<string, object?> p)
    {
        var tool = new DateTimeTool();
        await tool.InitializeAsync();
        return await tool.ExecuteAsync(p, new ToolExecutionContext());
    }

    private static async Task<string> RunEncoding(Dictionary<string, object?> p)
    {
        var tool = new EncodingTool();
        await tool.InitializeAsync();
        var r = await tool.ExecuteAsync(p, new ToolExecutionContext());
        r.IsSuccessful.Should().BeTrue(r.ErrorMessage);
        return JsonSerializer.Serialize(r.Data);
    }

    [Fact]
    public async Task ConvertTimezone_UtcInputWithUtcSource_DoesNotThrow()
    {
        // A "Z" input parses as Kind=Utc; ConvertTimeToUtc(dt, ...) previously threw for that Kind.
        var result = await RunDate(new()
        {
            ["operation"] = "convert_timezone",
            ["date_input"] = "2024-01-15T12:00:00Z",
            ["timezone"] = "UTC",
            ["target_timezone"] = "UTC"
        });

        // The regression is that a Kind=Utc input no longer throws during conversion.
        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
    }

    [Fact]
    public async Task ConvertTimezone_UnknownTimezone_FailsCleanly()
    {
        var result = await RunDate(new()
        {
            ["operation"] = "convert_timezone",
            ["date_input"] = "2024-01-15T12:00:00",
            ["target_timezone"] = "Mars/Olympus_Mons"
        });

        result.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task UrlEncodeDecode_RoundTripsLiteralPlus()
    {
        var encoded = await RunEncoding(new() { ["operation"] = "url_encode", ["input_text"] = "a+b c" });
        var encodedValue = JsonDocument.Parse(encoded).RootElement.GetProperty("encoded").GetString()!;

        var decoded = await RunEncoding(new() { ["operation"] = "url_decode", ["input_text"] = encodedValue });
        JsonDocument.Parse(decoded).RootElement.GetProperty("decoded").GetString()
            .Should().Be("a+b c", "encode/decode must be symmetric for a literal '+'");
    }
}
