using System.Collections.Immutable;
using Andy.CK.Language;
using Andy.CK.Numerics;
using Andy.CK.Presentation;
using Andy.CK.Statistics;
using Andy.CK.Units;
using Andy.Tools.Core;

namespace Andy.Tools.CK;

/// <summary>
/// <c>ck_ask</c> — answers a question in words, exactly, and says where the
/// numbers came from.
/// </summary>
/// <remarks>
/// <para>
/// This is the tool worth reaching for. The engine reads the question with a
/// deterministic grammar, plans the retrieval, computes over exact arithmetic,
/// and returns the sources it consulted. A question it cannot read is reported
/// as unread — it is never guessed at, and there is no model behind it inventing
/// a plausible answer.
/// </para>
/// <para>
/// Call <c>ck_capabilities</c> first if you do not know what this engine can be
/// asked. Its question forms are a fixed, listable set, so guessing is
/// unnecessary.
/// </para>
/// </remarks>
public sealed class CkAskTool : CkToolBase
{
    public override ToolMetadata Metadata => new()
    {
        Id = "ck_ask",
        Name = "Computable Knowledge Ask",
        Description =
            "Answers a factual or computational question in words using exact arithmetic, and returns "
            + "the sources the answer rests on. Refuses rather than guesses when the question is not "
            + "one it can read. Use ck_capabilities to see the question forms it understands.",
        Category = ToolCategory.Utility,
        RequiredPermissions = ToolPermissionFlags.None,
        Tags = ["knowledge", "exact", "symbolic", "provenance"],
        Parameters =
        [
            new ToolParameter
            {
                Name = "question",
                Type = "string",
                Description =
                    "The question, in words. For example 'area of France' or "
                    + "'population density of Germany'.",
                Required = true,
            },
        ],
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        if (!TryRequiredString(parameters, "question", out var question, out var failure))
        {
            return Task.FromResult(failure!);
        }

        var report = Engine.Ask(question);

        // An unread question is a successful call with a negative answer, not a
        // tool failure: the caller asked something well-formed and got a true
        // report that the engine could not read it. Reporting it as a failure
        // would invite a retry of the same question.
        return Task.FromResult(ToolResult.Success(new
        {
            question = report.Query,
            answered = report.IsComplete,
            answer = report.Primary?.DisplayText,
            pods = report.Pods.Select(pod => new
            {
                kind = pod.Kind.ToString(),
                text = pod.DisplayText,
                items = pod.Items.IsDefaultOrEmpty ? null : pod.Items.ToArray(),
            }).ToArray(),
            sources = report.Pods
                .Where(pod => pod.Kind == PodKind.Provenance)
                .SelectMany(pod => pod.Items)
                .ToArray(),
            diagnostics = report.Diagnostics.ToArray(),
            planHash = report.PlanHash,
            asOf = LoadedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
        }));
    }
}

/// <summary>
/// <c>ck_capabilities</c> — the question forms and vocabulary the engine
/// actually understands.
/// </summary>
/// <remarks>
/// Cheap, and worth calling before <c>ck_ask</c> on an unfamiliar deployment.
/// The grammar is a fixed set of forms over a fixed vocabulary, so what can be
/// asked is enumerable rather than a matter of trial and error — which is the
/// opposite of the situation with a natural-language model, and the reason this
/// tool exists at all.
/// </remarks>
public sealed class CkCapabilitiesTool : CkToolBase
{
    public override ToolMetadata Metadata => new()
    {
        Id = "ck_capabilities",
        Name = "Computable Knowledge Capabilities",
        Description =
            "Lists the question forms, vocabulary, loaded domain packages and data counts of the "
            + "computable-knowledge engine. Call this to find out what ck_ask can be asked, rather "
            + "than guessing at phrasings.",
        Category = ToolCategory.Utility,
        RequiredPermissions = ToolPermissionFlags.None,
        Tags = ["knowledge", "discovery", "schema"],
        Parameters = [],
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var vocabulary = Engine.Context.Lexicon.Entries
            .GroupBy(entry => entry.SymbolId)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new
            {
                symbol = group.Key,
                phrases = group.Select(item => item.Phrase).OrderBy(p => p, StringComparer.Ordinal).ToArray(),
            })
            .ToArray();

        return Task.FromResult(ToolResult.Success(new
        {
            questionForms = StandardPatterns.All.Select(pattern => pattern.Description).ToArray(),
            vocabulary,
            domains = Domains.Select(domain => new
            {
                id = domain.Manifest.Id,
                version = domain.Manifest.Version.ToString(),
            }).ToArray(),
            entities = Engine.Context.Entities.Count,
            properties = Engine.Context.Schema.Properties.Count,
            assertions = Engine.Context.Assertions.All().Length,
            asOf = LoadedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            dataNote =
                "The shipped domain data is a deliberately illustrative sample under clearly marked "
                + "sample sources. It exercises the engine end to end; it is not a dataset to answer "
                + "real questions from.",
        }));
    }
}

/// <summary>
/// <c>ck_convert_unit</c> — an exact unit conversion, or a refusal explaining
/// why the two units cannot be compared.
/// </summary>
/// <remarks>
/// The conversion is exact: a mile is a rational number of metres, and this
/// returns that rational rather than a rounded decimal. A conversion between
/// unrelated dimensions is refused with the dimensions named, rather than
/// returning a number that would be meaningless.
/// </remarks>
public sealed class CkConvertUnitTool : CkToolBase
{
    public override ToolMetadata Metadata => new()
    {
        Id = "ck_convert_unit",
        Name = "Computable Knowledge Convert Unit",
        Description =
            "Converts a quantity between units exactly, returning the exact value and its decimal "
            + "rendering. Refuses, naming both dimensions, when the units measure different things.",
        Category = ToolCategory.Utility,
        RequiredPermissions = ToolPermissionFlags.None,
        Tags = ["units", "conversion", "exact"],
        Parameters =
        [
            new ToolParameter
            {
                Name = "value",
                Type = "number",
                Description = "The magnitude to convert.",
                Required = true,
            },
            new ToolParameter
            {
                Name = "from",
                Type = "string",
                Description = "The unit it is in, such as 'Miles', 'Celsius' or 'Kilometers/Hours'.",
                Required = true,
            },
            new ToolParameter
            {
                Name = "to",
                Type = "string",
                Description = "The unit to convert to.",
                Required = true,
            },
        ],
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        if (!TryNumberList(parameters, "value", out var magnitudes, out var failure)
            || !TryRequiredString(parameters, "from", out var from, out failure)
            || !TryRequiredString(parameters, "to", out var to, out failure))
        {
            return Task.FromResult(failure!);
        }

        var catalog = UnitCatalog.Standard;
        if (!catalog.TryResolve(from, out var source))
        {
            return Task.FromResult(ToolResult.Failure($"'{from}' is not a unit this catalog knows."));
        }

        if (!catalog.TryResolve(to, out var target))
        {
            return Task.FromResult(ToolResult.Failure($"'{to}' is not a unit this catalog knows."));
        }

        var math = new QuantityMath(catalog: catalog);
        var quantity = new Quantity(Exactly(magnitudes[0]), UnitTerm.Simple(source));

        if (!math.TryConvert(quantity, UnitTerm.Simple(target), out var converted))
        {
            var reason = math.Diagnostics.Count > 0
                ? string.Join("; ", math.Diagnostics.Select(d => d.Message))
                : $"'{from}' measures {source.Dimension} and '{to}' measures {target.Dimension}.";

            return Task.FromResult(ToolResult.Failure($"Cannot convert {from} to {to}. {reason}"));
        }

        var numbers = new NumericContext();
        return Task.FromResult(ToolResult.Success(new
        {
            value = converted.Magnitude.ToString(),
            unit = converted.Unit.ToString(),
            isExact = converted.Magnitude.IsExact,
            asDecimal = numbers.ToInexact(converted.Magnitude, NumberKind.BigReal, 17).ToDouble(),
            dimension = converted.Unit.Dimension.ToString(),
        }));
    }
}

/// <summary>
/// <c>ck_constant</c> — a physical constant, and whether its value is exact.
/// </summary>
/// <remarks>
/// The distinction is the point of the tool. Since the 2019 SI redefinition the
/// speed of light and the Planck constant have exact decimal values, while the
/// gravitational constant is a measurement with an uncertainty. Reporting both
/// as though they were the same kind of number would misrepresent one of them,
/// and a result computed from the second cannot be quoted to more figures than
/// the second has.
/// </remarks>
public sealed class CkConstantTool : CkToolBase
{
    public override ToolMetadata Metadata => new()
    {
        Id = "ck_constant",
        Name = "Computable Knowledge Physical Constant",
        Description =
            "Returns a physical constant with its unit, and says whether the value is exact by SI "
            + "definition or a measurement carrying an uncertainty. Resolves by symbol, id or name — "
            + "'c', 'SpeedOfLight', 'speed of light in vacuum'. Omit the name to list them all.",
        Category = ToolCategory.Utility,
        RequiredPermissions = ToolPermissionFlags.None,
        Tags = ["physics", "constants", "exact", "uncertainty"],
        Parameters =
        [
            new ToolParameter
            {
                Name = "name",
                Type = "string",
                Description = "The constant's symbol, id or name. Omit to list every constant.",
                Required = false,
            },
        ],
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var numbers = new NumericContext();

        object Describe(PhysicalConstant constant) => new
        {
            id = constant.Id,
            symbol = constant.Symbol,
            name = constant.Name,
            value = constant.Value.Magnitude.ToString(),
            asDecimal = numbers.ToInexact(constant.Value.Magnitude, NumberKind.BigReal, 17).ToDouble(),
            unit = constant.Value.Unit.ToString(),
            exact = constant.IsExact,
            standardUncertainty = constant.StandardUncertainty?.ToString(),
            note = constant.Note,
        };

        if (!parameters.TryGetValue("name", out var raw) || raw is null
            || string.IsNullOrWhiteSpace(raw.ToString()))
        {
            return Task.FromResult(ToolResult.Success(new
            {
                constants = PhysicalConstants.All.Select(Describe).ToArray(),
                source = PhysicalConstants.Source,
            }));
        }

        var requested = raw.ToString()!.Trim();
        if (!PhysicalConstants.TryResolve(requested, out var found))
        {
            return Task.FromResult(ToolResult.Failure(
                $"'{requested}' is not a constant this engine knows. Call ck_constant with no name to list them. "
                + "Note that symbols are case-sensitive, because 'c' and 'C' are different things."));
        }

        return Task.FromResult(ToolResult.Success(new
        {
            constant = Describe(found),
            source = PhysicalConstants.Source,
        }));
    }
}

/// <summary>
/// <c>ck_statistics</c> — exact descriptive statistics over a list of numbers.
/// </summary>
/// <remarks>
/// Exact where exactness is possible: the mean of 1, 2, 3, 4 is 5/2 rather than
/// 2.5, and a variance is a rational rather than a rounded decimal. A statistic
/// whose value is irrational — a standard deviation, usually — is returned as
/// the exact variance under a square root together with its decimal, so the
/// caller can see both what the value is and what it rounds to.
/// </remarks>
public sealed class CkStatisticsTool : CkToolBase
{
    public override ToolMetadata Metadata => new()
    {
        Id = "ck_statistics",
        Name = "Computable Knowledge Statistics",
        Description =
            "Computes exact descriptive statistics over a list of numbers: total, mean, median, mode, "
            + "variance, standard deviation, minimum and maximum. Results are exact rationals where "
            + "exactness is possible, with decimal renderings alongside.",
        Category = ToolCategory.Utility,
        RequiredPermissions = ToolPermissionFlags.None,
        Tags = ["statistics", "exact", "mathematics"],
        Parameters =
        [
            new ToolParameter
            {
                Name = "values",
                Type = "array",
                Description = "The numbers, as a JSON array or a comma-separated string.",
                Required = true,
            },
        ],
    };

    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        if (!TryNumberList(parameters, "values", out var raw, out var failure))
        {
            return Task.FromResult(failure!);
        }

        var numbers = new NumericContext();
        var sample = ImmutableArray.CreateRange(raw.Select(Exactly));

        // Each statistic either has a value or does not -- a variance needs two
        // observations, a median needs an ordering. Absent is reported as null
        // rather than as zero, because a mean of nothing is not nought.
        string? Exact(Number value, bool present) => present ? value.ToString() : null;
        double? Rendered(Number value, bool present) =>
            present ? numbers.ToInexact(value, NumberKind.BigReal, 17).ToDouble() : null;

        var haveMean = DescriptiveStatistics.TryMean(numbers, sample, out var mean);
        var haveMedian = DescriptiveStatistics.TryMedian(numbers, sample, out var median);
        var haveVariance = DescriptiveStatistics.TryVariance(numbers, sample, out var variance);
        var haveMin = DescriptiveStatistics.TryMin(sample, out var min);
        var haveMax = DescriptiveStatistics.TryMax(sample, out var max);

        return Task.FromResult(ToolResult.Success(new
        {
            count = sample.Length,
            total = DescriptiveStatistics.Total(numbers, sample).ToString(),
            mean = Exact(mean, haveMean),
            meanAsDecimal = Rendered(mean, haveMean),
            median = Exact(median, haveMedian),
            mode = DescriptiveStatistics.Modes(sample).Select(m => m.ToString()).ToArray(),
            variance = Exact(variance, haveVariance),
            varianceAsDecimal = Rendered(variance, haveVariance),
            standardDeviationAsDecimal = haveVariance
                ? Math.Sqrt(numbers.ToInexact(variance, NumberKind.BigReal, 17).ToDouble())
                : (double?)null,
            minimum = Exact(min, haveMin),
            maximum = Exact(max, haveMax),
            notes = new
            {
                variance = "Sample variance, dividing by n - 1.",
                standardDeviation =
                    "The square root of the variance. Usually irrational, so only its decimal is "
                    + "given; the variance above is exact.",
            },
        }));
    }
}
