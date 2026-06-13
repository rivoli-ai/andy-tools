using System.Collections.Generic;
using System.Text.Json;
using Andy.Tools.Core;
using Andy.Tools.Validation;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Validation;

/// <summary>
/// Regression tests for issue #28: JSON-sourced parameter values (JsonElement) were rejected by the
/// type checks, and AllowedValues used boxed-type equality so an int allowed-value never matched a
/// long/double/JsonElement value.
/// </summary>
public class ToolValidatorJsonCoercionTests
{
    private readonly ToolValidator _validator = new();

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    [Fact]
    public void BooleanFromJsonElement_IsAccepted()
    {
        var defs = new List<ToolParameter> { new() { Name = "flag", Type = "boolean", Required = true } };
        var parameters = new Dictionary<string, object?> { ["flag"] = Json("true") };

        _validator.ValidateParameters(parameters, defs).IsValid.Should().BeTrue();
    }

    [Fact]
    public void NumberFromJsonElement_RespectsIntegerAndRange()
    {
        var defs = new List<ToolParameter>
        {
            new() { Name = "count", Type = "integer", Required = true, MinValue = 1, MaxValue = 10 }
        };

        _validator.ValidateParameters(new Dictionary<string, object?> { ["count"] = Json("5") }, defs)
            .IsValid.Should().BeTrue();

        // 5.5 is not an integer
        _validator.ValidateParameters(new Dictionary<string, object?> { ["count"] = Json("5.5") }, defs)
            .IsValid.Should().BeFalse();

        // 99 is out of range
        _validator.ValidateParameters(new Dictionary<string, object?> { ["count"] = Json("99") }, defs)
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void StringFromJsonElement_IsAccepted()
    {
        var defs = new List<ToolParameter> { new() { Name = "name", Type = "string", Required = true } };
        var parameters = new Dictionary<string, object?> { ["name"] = Json("\"hello\"") };

        _validator.ValidateParameters(parameters, defs).IsValid.Should().BeTrue();
    }

    [Fact]
    public void AllowedValues_MatchAcrossNumericTypes()
    {
        var defs = new List<ToolParameter>
        {
            new() { Name = "n", Type = "integer", Required = true, AllowedValues = new object[] { 1, 2, 3 } }
        };

        // value arrives as a long (JsonElement number) but allowed-values are ints — must still match.
        _validator.ValidateParameters(new Dictionary<string, object?> { ["n"] = Json("2") }, defs)
            .IsValid.Should().BeTrue();

        _validator.ValidateParameters(new Dictionary<string, object?> { ["n"] = Json("4") }, defs)
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void AllowedValues_MatchStringEnum()
    {
        var defs = new List<ToolParameter>
        {
            new() { Name = "method", Type = "string", Required = true, AllowedValues = new object[] { "GET", "POST" } }
        };

        _validator.ValidateParameters(new Dictionary<string, object?> { ["method"] = Json("\"POST\"") }, defs)
            .IsValid.Should().BeTrue();
    }
}
