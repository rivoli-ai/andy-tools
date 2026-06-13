using System.Collections.Generic;
using Andy.Tools;
using Andy.Tools.Core.OutputLimiting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Andy.Tools.Tests;

/// <summary>
/// Regression test for issue #27: output-limiter options were bound by calling
/// services.BuildServiceProvider() inside a Configure lambda (a throwaway container, ASP0000). Binding
/// now flows through an IConfigureOptions that reads IConfiguration from the real container.
/// </summary>
public class ServiceCollectionOptionsBindingTests
{
    [Fact]
    public void OutputLimiterOptions_BindFromRegisteredConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ToolOutputLimiterOptions.SectionName}:MaxOutputCharacters"] = "1234",
                [$"{ToolOutputLimiterOptions.SectionName}:MaxFileListEntries"] = "7",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddAndyTools();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ToolOutputLimiterOptions>>().Value;

        options.MaxOutputCharacters.Should().Be(1234);
        options.MaxFileListEntries.Should().Be(7);
    }

    [Fact]
    public void Registration_WithoutConfiguration_DoesNotThrow_AndUsesDefaults()
    {
        // No IConfiguration registered — binding must be a no-op (defaults retained), not a failure.
        var services = new ServiceCollection();
        services.AddAndyTools();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ToolOutputLimiterOptions>>().Value;

        options.MaxOutputCharacters.Should().Be(50_000); // documented default
    }
}
