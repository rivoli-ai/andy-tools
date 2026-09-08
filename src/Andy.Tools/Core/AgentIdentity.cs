namespace Andy.Tools.Core;

/// <summary>Host-owned, agent-scoped identity. Tools must not substitute their own worker process.</summary>
public interface IAgentIdentity
{
    AgentIdentitySnapshot GetSnapshot();
    /// <summary>Set the display name. Null or whitespace clears it without erasing history.</summary>
    AgentIdentitySnapshot SetName(string? name);
}

/// <summary>One runtime activation; a PID is descriptive, not a persistent identity.</summary>
public sealed record AgentActivation
{
    public required string InstanceId { get; init; }
    public int? ProcessId { get; init; }
    public string? Platform { get; init; }
    public string? OperatingSystem { get; init; }
    public string? Architecture { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
}

public sealed record AgentIdentityEvent
{
    public long Sequence { get; init; }
    public required string Kind { get; init; }
    public string? PreviousName { get; init; }
    public string? Name { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public required AgentActivation Activation { get; init; }
}

/// <summary>Portable identity and append-only observations. Names do not identify authenticated users.</summary>
public sealed record AgentIdentitySnapshot
{
    public const int CurrentVersion = 1;
    public int Version { get; init; } = CurrentVersion;
    public required string AgentId { get; init; }
    public string? Name { get; init; }
    public required AgentActivation Activation { get; init; }
    public IReadOnlyList<AgentIdentityEvent> History { get; init; } = Array.Empty<AgentIdentityEvent>();
}
