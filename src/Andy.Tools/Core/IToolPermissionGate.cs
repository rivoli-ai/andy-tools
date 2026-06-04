namespace Andy.Tools.Core;

/// <summary>
/// Optional consent gate consulted by <see cref="IToolExecutor"/> before a tool runs. When no
/// implementation is registered in DI, the executor behaves exactly as before (no gating). A permission
/// system (e.g. Andy.Permissions) registers an implementation that authorizes the call against its rules
/// and, where needed, prompts for consent — returning a single allow/deny verdict so the executor itself
/// stays unaware of rules, layers, or prompts.
/// </summary>
public interface IToolPermissionGate
{
    /// <summary>Decides whether a tool call may proceed. Returns a verdict; never throws for a denial.</summary>
    public Task<ToolPermissionVerdict> CheckAsync(ToolPermissionGateRequest request, CancellationToken cancellationToken = default);
}

/// <summary>The information the gate needs to decide a tool call.</summary>
public sealed class ToolPermissionGateRequest
{
    /// <summary>The id of the tool being invoked.</summary>
    public required string ToolId { get; init; }

    /// <summary>The tool call parameters (used to resolve the governed resources).</summary>
    public required IReadOnlyDictionary<string, object?> Parameters { get; init; }

    /// <summary>The execution context (working directory, cancellation, permissions, …).</summary>
    public required ToolExecutionContext Context { get; init; }

    /// <summary>The tool's metadata, used for capability/confirmation fallback. May be null.</summary>
    public ToolMetadata? Metadata { get; init; }
}

/// <summary>The result of a <see cref="IToolPermissionGate"/> check.</summary>
public sealed class ToolPermissionVerdict
{
    /// <summary>True if the call may proceed.</summary>
    public bool Allowed { get; init; }

    /// <summary>Human-readable reason when denied (surfaced to the model as the tool error).</summary>
    public string? Reason { get; init; }

    /// <summary>A shared "allowed" verdict.</summary>
    public static ToolPermissionVerdict Allow { get; } = new() { Allowed = true };

    /// <summary>Creates a denial verdict with a reason.</summary>
    public static ToolPermissionVerdict Deny(string reason) => new() { Allowed = false, Reason = reason };
}
