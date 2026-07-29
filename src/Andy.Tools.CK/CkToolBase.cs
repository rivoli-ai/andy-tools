using Andy.CK.Domains.Abstractions;
using Andy.CK.Domains.Demographics;
using Andy.CK.Domains.Economics;
using Andy.CK.Domains.Geography;
using Andy.CK.Engine;
using Andy.Tools.Core;
using Andy.Tools.Library;

namespace Andy.Tools.CK;

/// <summary>
/// Shared base for the <c>ck_*</c> tools: builds the computable-knowledge engine
/// once and hands it to the concrete tools.
/// </summary>
/// <remarks>
/// <para>
/// Every tool here is a pure computation. None reads a file, opens a socket, or
/// executes anything a caller supplies, so none requires a permission beyond
/// <see cref="ToolPermissionFlags.None"/>. That is worth stating rather than
/// leaving to inference: the engine is a symbolic evaluator over data compiled
/// into its own packages, and a question cannot reach outside it.
/// </para>
/// <para>
/// The engine is loaded at a fixed instant, not at the current time. Its domain
/// packages record their seed data against that instant, and a bitemporal query
/// can ask what was believed at a past one — so reading the clock here would
/// make the same question answerable differently on two days for no reason the
/// caller could see. This mirrors what the <c>andy-ck</c> CLI does, and follows
/// ADR-003 in the engine repository.
/// </para>
/// </remarks>
public abstract class CkToolBase : ToolBase
{
    /// <summary>
    /// The instant the engine's domain data is loaded at. Fixed so that an
    /// answer is a function of the question rather than of the day it was asked.
    /// </summary>
    internal static readonly DateTimeOffset LoadedAt =
        new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Lazy<ComputableKnowledgeEngine> Shared = new(Build, isThreadSafe: true);

    /// <summary>
    /// The engine, built once for the process. Construction loads three domain
    /// packages and validates their schemas, which is measured in hundreds of
    /// microseconds but is pure waste to repeat per call.
    /// </summary>
    protected static ComputableKnowledgeEngine Engine => Shared.Value;

    private static ComputableKnowledgeEngine Build() =>
        ComputableKnowledgeEngine.Create(
            LoadedAt,
            new GeographyDomain(),
            new DemographicsDomain(),
            new EconomicsDomain());

    /// <summary>
    /// The domain packages loaded, for a tool that reports on capability.
    /// </summary>
    protected static IReadOnlyList<IComputationalDomain> Domains => Engine.Domains;

    /// <summary>
    /// Reads a required string parameter, or produces the failure to return.
    /// </summary>
    protected static bool TryRequiredString(
        Dictionary<string, object?> parameters,
        string name,
        out string value,
        out ToolResult? failure)
    {
        value = string.Empty;
        failure = null;

        if (!parameters.TryGetValue(name, out var raw) || raw is null)
        {
            failure = ToolResult.Failure($"'{name}' is required.");
            return false;
        }

        var text = raw as string ?? raw.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            failure = ToolResult.Failure($"'{name}' may not be empty.");
            return false;
        }

        value = text.Trim();
        return true;
    }

    /// <summary>
    /// Reads a required list of numbers.
    /// </summary>
    /// <remarks>
    /// A parameter declared as <c>array</c> is validated by the framework as
    /// <c>IEnumerable and not string</c> before this runs, so a comma-separated
    /// string never reaches here — it is refused earlier with the expected type
    /// named, which is the more useful message anyway.
    /// </remarks>
    protected static bool TryNumberList(
        Dictionary<string, object?> parameters,
        string name,
        out IReadOnlyList<double> values,
        out ToolResult? failure)
    {
        values = [];
        failure = null;

        if (!parameters.TryGetValue(name, out var raw) || raw is null)
        {
            failure = ToolResult.Failure($"'{name}' is required.");
            return false;
        }

        var parsed = new List<double>();

        switch (raw)
        {
            case System.Collections.IEnumerable sequence and not string:
                foreach (var item in sequence)
                {
                    if (!TryNumber(item, out var element))
                    {
                        failure = ToolResult.Failure($"'{item}' in '{name}' is not a number.");
                        return false;
                    }

                    parsed.Add(element);
                }

                break;

            default:
                if (!TryNumber(raw, out var single))
                {
                    failure = ToolResult.Failure($"'{name}' is not a number or a list of numbers.");
                    return false;
                }

                parsed.Add(single);
                break;
        }

        if (parsed.Count == 0)
        {
            failure = ToolResult.Failure($"'{name}' contained no numbers.");
            return false;
        }

        values = parsed;
        return true;
    }

    /// <summary>
    /// Converts an incoming number to a tower number, keeping a whole number
    /// whole.
    ///
    /// <para>
    /// This matters more than it looks. Every one of these tools advertises
    /// exact arithmetic, and a magnitude that arrives as the double 1.0 and is
    /// handed to the engine as an inexact machine real throws that away at the
    /// door: one mile then converts to an inexact 1609.344 rather than to the
    /// exact rational it is. The transport format should not decide whether the
    /// answer is exact.
    /// </para>
    /// </summary>
    protected static Andy.CK.Numerics.Number Exactly(double value) =>
        value == Math.Floor(value) && Math.Abs(value) < 9.007199254740992e15
            ? Andy.CK.Numerics.Number.FromInteger(new System.Numerics.BigInteger(value))
            : Andy.CK.Numerics.Number.FromDouble(value);

    private static bool TryNumber(object? raw, out double value)
    {
        switch (raw)
        {
            case double d:
                value = d;
                return true;
            case float f:
                value = f;
                return true;
            case int i:
                value = i;
                return true;
            case long l:
                value = l;
                return true;
            case decimal m:
                value = (double)m;
                return true;
            default:
                return double.TryParse(
                    raw?.ToString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out value);
        }
    }
}
