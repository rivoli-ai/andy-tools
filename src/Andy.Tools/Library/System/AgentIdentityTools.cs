using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.System;

/// <summary>Names the calling agent using the identity service supplied by its host.</summary>
public sealed class SetAgentNameTool : ToolBase
{
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "set_agent_name",
        Name = "Set Agent Name",
        Description = "Set the current agent's display name using one string. An empty name clears it, retaining identity and runtime history.",
        Category = ToolCategory.System,
        Parameters = [new() { Name = "name", Type = "string", Required = true, Description = "Display name; empty clears the name without deleting history." }]
    };

    protected override Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (context.AgentIdentity is null)
            return Task.FromResult(ToolResult.Failure("The calling host does not provide agent identity."));
        if (parameters["name"] is not string name)
            return Task.FromResult(ToolResult.Failure("name must be a string."));
        return Task.FromResult(ToolResults.Success(context.AgentIdentity.SetName(name)));
    }
}

/// <summary>Reads the calling agent's name, current runtime, and retained history.</summary>
public sealed class GetAgentIdentityTool : ToolBase
{
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "get_agent_identity",
        Name = "Get Agent Identity",
        Description = "Read the current agent's display name, logical ID, runtime activation (PID, platform, OS and UTC time), and naming/activation history.",
        Category = ToolCategory.System
    };

    protected override Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(context.AgentIdentity is { } identity
            ? ToolResults.Success(identity.GetSnapshot())
            : ToolResult.Failure("The calling host does not provide agent identity."));
    }
}
