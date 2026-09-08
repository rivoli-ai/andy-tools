using Andy.Tools.Core;
using Andy.Tools.Library.System;
using Xunit;

namespace Andy.Tools.Tests.Library.System;

public class AgentIdentityToolsTests
{
    private sealed class HostIdentity : IAgentIdentity
    {
        private AgentIdentitySnapshot _state = new()
        {
            AgentId = Guid.NewGuid().ToString("N"),
            Activation = new() { InstanceId = "remote-caller", ProcessId = 987, Platform = "remote", StartedAtUtc = DateTimeOffset.UnixEpoch }
        };
        public AgentIdentitySnapshot GetSnapshot() => _state;
        public AgentIdentitySnapshot SetName(string? name) => _state = _state with { Name = name };
    }

    [Fact]
    public async Task SetAndReadUseCallerIdentityWithoutCrossAgentState()
    {
        var first = new HostIdentity();
        var second = new HostIdentity();
        var setter = new SetAgentNameTool();
        var reader = new GetAgentIdentityTool();
        await setter.InitializeAsync();
        await reader.InitializeAsync();
        var result = await setter.ExecuteAsync(new() { ["name"] = "cedar" }, new() { AgentIdentity = first });
        Assert.True(result.IsSuccessful, result.ErrorMessage);
        var read = await reader.ExecuteAsync(new(), new() { AgentIdentity = first });
        var snapshot = Assert.IsType<AgentIdentitySnapshot>(read.Data);
        Assert.Equal("cedar", snapshot.Name);
        Assert.Equal(987, snapshot.Activation.ProcessId);
        Assert.Equal("remote-caller", snapshot.Activation.InstanceId);
        Assert.Null(second.GetSnapshot().Name);
        Assert.NotEqual(first.GetSnapshot().AgentId, second.GetSnapshot().AgentId);
    }

    [Fact]
    public async Task UnsupportedHostReportsFailureInsteadOfCreatingGlobalIdentity()
    {
        var tool = new SetAgentNameTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(new() { ["name"] = "cedar" }, new());
        Assert.False(result.IsSuccessful);
        Assert.Contains("host", result.ErrorMessage);
        var getter = new GetAgentIdentityTool();
        await getter.InitializeAsync();
        Assert.False((await getter.ExecuteAsync(new(), new())).IsSuccessful);
    }

    [Fact]
    public async Task EmptyStringIsPassedToHostForExplicitReset()
    {
        var identity = new HostIdentity();
        var tool = new SetAgentNameTool();
        await tool.InitializeAsync();
        Assert.True((await tool.ExecuteAsync(new() { ["name"] = "" }, new() { AgentIdentity = identity })).IsSuccessful);
        Assert.Equal("", identity.GetSnapshot().Name);
    }
}
