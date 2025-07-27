using Andy.Tools.Core;
using Andy.Tools.Discovery;
using Andy.Tools.Framework;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Framework;

public class ToolFrameworkOptionsTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var options = new ToolFrameworkOptions();

        // Assert
        options.AutoDiscoverTools.Should().BeTrue();
        options.RegisterBuiltInTools.Should().BeTrue();
        options.DiscoveryOptions.Should().NotBeNull();
        options.DefaultResourceLimits.Should().NotBeNull();
        options.DefaultPermissions.Should().NotBeNull();
        options.EnableSecurity.Should().BeTrue();
        options.EnableResourceMonitoring.Should().BeTrue();
        options.EnableObservability.Should().BeTrue();
        options.EnableDetailedTracing.Should().BeFalse();
        options.EnableMetricsExport.Should().BeTrue();
        options.MetricsAggregationInterval.Should().Be(TimeSpan.FromMinutes(1));
        options.ObservabilityRetentionPeriod.Should().Be(TimeSpan.FromDays(7));
        options.SecurityViolationMaxAge.Should().Be(TimeSpan.FromDays(7));
        options.PluginDirectories.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void AutoDiscoverTools_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();

        // Act
        options.AutoDiscoverTools = false;

        // Assert
        options.AutoDiscoverTools.Should().BeFalse();
    }

    [Fact]
    public void RegisterBuiltInTools_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();

        // Act
        options.RegisterBuiltInTools = false;

        // Assert
        options.RegisterBuiltInTools.Should().BeFalse();
    }

    [Fact]
    public void DiscoveryOptions_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();
        var discoveryOptions = new ToolDiscoveryOptions();

        // Act
        options.DiscoveryOptions = discoveryOptions;

        // Assert
        options.DiscoveryOptions.Should().BeSameAs(discoveryOptions);
    }

    [Fact]
    public void DefaultResourceLimits_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();
        var resourceLimits = new ToolResourceLimits();

        // Act
        options.DefaultResourceLimits = resourceLimits;

        // Assert
        options.DefaultResourceLimits.Should().BeSameAs(resourceLimits);
    }

    [Fact]
    public void DefaultPermissions_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();
        var permissions = new ToolPermissions();

        // Act
        options.DefaultPermissions = permissions;

        // Assert
        options.DefaultPermissions.Should().BeSameAs(permissions);
    }

    [Fact]
    public void EnableSecurity_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();

        // Act
        options.EnableSecurity = false;

        // Assert
        options.EnableSecurity.Should().BeFalse();
    }

    [Fact]
    public void EnableResourceMonitoring_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();

        // Act
        options.EnableResourceMonitoring = false;

        // Assert
        options.EnableResourceMonitoring.Should().BeFalse();
    }

    [Fact]
    public void EnableObservability_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();

        // Act
        options.EnableObservability = false;

        // Assert
        options.EnableObservability.Should().BeFalse();
    }

    [Fact]
    public void EnableDetailedTracing_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();

        // Act
        options.EnableDetailedTracing = true;

        // Assert
        options.EnableDetailedTracing.Should().BeTrue();
    }

    [Fact]
    public void EnableMetricsExport_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();

        // Act
        options.EnableMetricsExport = false;

        // Assert
        options.EnableMetricsExport.Should().BeFalse();
    }

    [Fact]
    public void MetricsAggregationInterval_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();
        var interval = TimeSpan.FromMinutes(5);

        // Act
        options.MetricsAggregationInterval = interval;

        // Assert
        options.MetricsAggregationInterval.Should().Be(interval);
    }

    [Fact]
    public void ObservabilityRetentionPeriod_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();
        var period = TimeSpan.FromDays(30);

        // Act
        options.ObservabilityRetentionPeriod = period;

        // Assert
        options.ObservabilityRetentionPeriod.Should().Be(period);
    }

    [Fact]
    public void SecurityViolationMaxAge_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();
        var maxAge = TimeSpan.FromDays(14);

        // Act
        options.SecurityViolationMaxAge = maxAge;

        // Assert
        options.SecurityViolationMaxAge.Should().Be(maxAge);
    }

    [Fact]
    public void PluginDirectories_ShouldBeSettable()
    {
        // Arrange
        var options = new ToolFrameworkOptions();
        var directories = new List<string> { "/path/to/plugins", "/another/path" };

        // Act
        options.PluginDirectories = directories;

        // Assert
        options.PluginDirectories.Should().BeSameAs(directories);
    }

    [Fact]
    public void Options_ShouldSupportFluentConfiguration()
    {
        // Arrange & Act
        var options = new ToolFrameworkOptions
        {
            AutoDiscoverTools = false,
            RegisterBuiltInTools = false,
            EnableSecurity = false,
            EnableResourceMonitoring = false,
            EnableObservability = false,
            EnableDetailedTracing = true,
            EnableMetricsExport = false,
            MetricsAggregationInterval = TimeSpan.FromSeconds(30),
            ObservabilityRetentionPeriod = TimeSpan.FromDays(1),
            SecurityViolationMaxAge = TimeSpan.FromHours(12),
            PluginDirectories = ["/plugins"]
        };

        // Assert
        options.AutoDiscoverTools.Should().BeFalse();
        options.RegisterBuiltInTools.Should().BeFalse();
        options.EnableSecurity.Should().BeFalse();
        options.EnableResourceMonitoring.Should().BeFalse();
        options.EnableObservability.Should().BeFalse();
        options.EnableDetailedTracing.Should().BeTrue();
        options.EnableMetricsExport.Should().BeFalse();
        options.MetricsAggregationInterval.Should().Be(TimeSpan.FromSeconds(30));
        options.ObservabilityRetentionPeriod.Should().Be(TimeSpan.FromDays(1));
        options.SecurityViolationMaxAge.Should().Be(TimeSpan.FromHours(12));
        options.PluginDirectories.Should().ContainSingle().Which.Should().Be("/plugins");
    }

    [Fact]
    public void Options_ShouldBeIndependent_WhenMultipleInstancesCreated()
    {
        // Arrange
        var options1 = new ToolFrameworkOptions();
        var options2 = new ToolFrameworkOptions();

        // Act
        options1.AutoDiscoverTools = false;
        options1.PluginDirectories.Add("/path1");
        
        options2.EnableSecurity = false;
        options2.PluginDirectories.Add("/path2");

        // Assert
        options1.AutoDiscoverTools.Should().BeFalse();
        options2.AutoDiscoverTools.Should().BeTrue();
        
        options1.EnableSecurity.Should().BeTrue();
        options2.EnableSecurity.Should().BeFalse();
        
        options1.PluginDirectories.Should().ContainSingle().Which.Should().Be("/path1");
        options2.PluginDirectories.Should().ContainSingle().Which.Should().Be("/path2");
    }
}