using Andy.Tools.Core;
using Andy.Tools.Library;
using Andy.Data;
using Andy.Data.Operations;

namespace Andy.Tools.Data;

/// <summary>
/// Base adapter that exposes a framework-independent <see cref="DataFrameOperationBase"/> as an Andy
/// <see cref="ITool"/>. It derives the tool metadata from the operation's
/// <see cref="OperationMetadata"/>, maps the <see cref="ToolExecutionContext"/> to the engine's
/// <see cref="DataFrameExecuteOptions"/>, and converts the <see cref="DataFrameResponse"/> envelope to
/// a <see cref="ToolResult"/>. All dataframe behavior lives in <c>Andy.Data</c>; this layer is glue.
/// </summary>
public abstract class DataFrameToolAdapter : ToolBase
{
    /// <summary>
    /// The framework's generic 100 MB <c>MaxMemoryBytes</c> default is not a host decision and is far
    /// below DuckDB's reader working set, so it is treated as "unset" — the engine cap applies only
    /// when a host configures any other value.
    /// </summary>
    private static readonly long FrameworkDefaultMaxMemoryBytes = new ToolResourceLimits().MaxMemoryBytes;

    private ToolMetadata? _metadata;

    /// <summary>The wrapped framework-independent operation.</summary>
    protected DataFrameOperationBase Operation { get; }

    protected DataFrameToolAdapter(DataFrameOperationBase operation) => Operation = operation;

    /// <summary>
    /// Capabilities this tool requires. Defaults to <see cref="ToolPermissionFlags.None"/>: most
    /// dataframe operations work purely on in-memory, already-loaded datasets and touch no files, so
    /// they need no filesystem permission. Only the loaders (override with
    /// <see cref="ToolPermissionFlags.FileSystemRead"/>, scoped to the input path) and the exporter
    /// (<see cref="ToolPermissionFlags.FileSystemWrite"/>, scoped to the output path) touch disk.
    /// </summary>
    protected virtual ToolPermissionFlags RequiredPermissions => ToolPermissionFlags.None;

    /// <inheritdoc />
    public override ToolMetadata Metadata => _metadata ??= BuildMetadata();

    private ToolMetadata BuildMetadata()
    {
        var m = Operation.Metadata;
        return new ToolMetadata
        {
            Id = m.Id,
            Name = m.Name,
            Description = m.Description,
            Version = "1.0.0",
            Category = ToolCategory.Database,
            RequiredPermissions = RequiredPermissions,
            Parameters = m.Parameters.Select(ToToolParameter).ToList(),
        };
    }

    private static ToolParameter ToToolParameter(DataFrameParam p) => new()
    {
        Name = p.Name,
        Type = p.Type,
        Required = p.Required,
        Description = p.Description ?? string.Empty,
        Pattern = p.Pattern,
        MinValue = ToNullableDouble(p.MinValue),
        MaxValue = ToNullableDouble(p.MaxValue),
        AllowedValues = p.AllowedValues?.ToArray(),
        DefaultValue = p.DefaultValue,
    };

    private static double? ToNullableDouble(object? value) =>
        value is null ? null : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Defers schema validation to <see cref="DataFrameOperationBase"/> (which produces the documented
    /// envelope), so the sealed <c>ToolBase.ExecuteAsync</c> does not turn validation into a bare
    /// failure.
    /// </summary>
    public override IList<string> ValidateParameters(Dictionary<string, object?> parameters) =>
        new List<string>();

    /// <inheritdoc />
    protected override Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var limits = context.ResourceLimits;
        var options = new DataFrameExecuteOptions
        {
            MaxMemoryBytes = limits is { MaxMemoryBytes: > 0 } && limits.MaxMemoryBytes != FrameworkDefaultMaxMemoryBytes
                ? limits.MaxMemoryBytes
                : null,
            MaxExecutionTimeMs = limits is { MaxExecutionTimeMs: > 0 } ? limits.MaxExecutionTimeMs : null,
            CancellationToken = context.CancellationToken,
        };

        return Task.FromResult(ToToolResult(Operation.Execute(parameters, options)));
    }

    private static ToolResult ToToolResult(DataFrameResponse response)
    {
        var envelope = response.ToEnvelope();
        if (response.Success)
        {
            return ToolResult.Success(envelope);
        }

        return new ToolResult
        {
            IsSuccessful = false,
            ErrorMessage = response.Message,
            Data = envelope,
            Metadata = new Dictionary<string, object?> { ["error_code"] = response.ErrorCode },
        };
    }
}
