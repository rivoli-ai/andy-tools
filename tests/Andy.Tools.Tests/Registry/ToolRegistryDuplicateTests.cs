using System;
using Andy.Tools.Core;
using Andy.Tools.Registry;
using Andy.Tools.Validation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Andy.Tools.Tests.Registry;

/// <summary>
/// Regression test for issue #29: the registry checked ContainsKey outside the lock and ignored
/// TryAdd's result, so a duplicate could silently fail to store yet still raise ToolRegistered.
/// </summary>
public class ToolRegistryDuplicateTests
{
    private static ToolMetadata Meta(string id) => new()
    {
        Id = id,
        Name = id,
        Description = "test",
        Version = "1.0.0",
        Category = ToolCategory.Utility
    };

    [Fact]
    public void RegisterTool_DuplicateId_ThrowsAndDoesNotRaiseEventTwice()
    {
        var registry = new ToolRegistry(new ToolValidator(), NullLogger<ToolRegistry>.Instance);
        var registeredEvents = 0;
        registry.ToolRegistered += (_, _) => registeredEvents++;

        ITool Factory(IServiceProvider _) => null!;

        registry.RegisterTool(Meta("dup"), Factory);
        Action second = () => registry.RegisterTool(Meta("dup"), Factory);

        second.Should().Throw<InvalidOperationException>();
        registeredEvents.Should().Be(1, "the failed duplicate registration must not raise ToolRegistered");
        registry.GetTool("dup").Should().NotBeNull();
    }
}
