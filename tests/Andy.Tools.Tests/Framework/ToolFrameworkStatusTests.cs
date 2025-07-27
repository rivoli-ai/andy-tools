using Andy.Tools.Framework;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Framework;

public class ToolFrameworkStatusTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var status = new ToolFrameworkStatus();

        // Assert
        status.IsInitialized.Should().BeFalse();
        status.RegisteredToolsCount.Should().Be(0);
        status.ActiveExecutionsCount.Should().Be(0);
        status.TotalExecutions.Should().Be(0);
        status.InitializedAt.Should().BeNull();
        status.LastMaintenanceAt.Should().BeNull();
        status.StartupErrors.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void IsInitialized_ShouldBeSettable()
    {
        // Arrange
        var status = new ToolFrameworkStatus();

        // Act
        status.IsInitialized = true;

        // Assert
        status.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void RegisteredToolsCount_ShouldBeSettable()
    {
        // Arrange
        var status = new ToolFrameworkStatus();

        // Act
        status.RegisteredToolsCount = 42;

        // Assert
        status.RegisteredToolsCount.Should().Be(42);
    }

    [Fact]
    public void ActiveExecutionsCount_ShouldBeSettable()
    {
        // Arrange
        var status = new ToolFrameworkStatus();

        // Act
        status.ActiveExecutionsCount = 5;

        // Assert
        status.ActiveExecutionsCount.Should().Be(5);
    }

    [Fact]
    public void TotalExecutions_ShouldBeSettable()
    {
        // Arrange
        var status = new ToolFrameworkStatus();

        // Act
        status.TotalExecutions = 1000;

        // Assert
        status.TotalExecutions.Should().Be(1000);
    }

    [Fact]
    public void InitializedAt_ShouldBeSettable()
    {
        // Arrange
        var status = new ToolFrameworkStatus();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        status.InitializedAt = timestamp;

        // Assert
        status.InitializedAt.Should().Be(timestamp);
    }

    [Fact]
    public void LastMaintenanceAt_ShouldBeSettable()
    {
        // Arrange
        var status = new ToolFrameworkStatus();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        status.LastMaintenanceAt = timestamp;

        // Assert
        status.LastMaintenanceAt.Should().Be(timestamp);
    }

    [Fact]
    public void StartupErrors_ShouldBeSettable()
    {
        // Arrange
        var status = new ToolFrameworkStatus();
        var errors = new List<string> { "Error 1", "Error 2" };

        // Act
        status.StartupErrors = errors;

        // Assert
        status.StartupErrors.Should().BeSameAs(errors);
    }

    [Fact]
    public void StartupErrors_ShouldSupportAddingErrors()
    {
        // Arrange
        var status = new ToolFrameworkStatus();

        // Act
        status.StartupErrors.Add("First error");
        status.StartupErrors.Add("Second error");

        // Assert
        status.StartupErrors.Should().HaveCount(2);
        status.StartupErrors[0].Should().Be("First error");
        status.StartupErrors[1].Should().Be("Second error");
    }

    [Fact]
    public void Status_ShouldSupportFluentConfiguration()
    {
        // Arrange & Act
        var timestamp1 = DateTimeOffset.UtcNow.AddHours(-1);
        var timestamp2 = DateTimeOffset.UtcNow;
        
        var status = new ToolFrameworkStatus
        {
            IsInitialized = true,
            RegisteredToolsCount = 25,
            ActiveExecutionsCount = 3,
            TotalExecutions = 500,
            InitializedAt = timestamp1,
            LastMaintenanceAt = timestamp2,
            StartupErrors = ["Warning: Slow startup"]
        };

        // Assert
        status.IsInitialized.Should().BeTrue();
        status.RegisteredToolsCount.Should().Be(25);
        status.ActiveExecutionsCount.Should().Be(3);
        status.TotalExecutions.Should().Be(500);
        status.InitializedAt.Should().Be(timestamp1);
        status.LastMaintenanceAt.Should().Be(timestamp2);
        status.StartupErrors.Should().ContainSingle().Which.Should().Be("Warning: Slow startup");
    }

    [Fact]
    public void Status_ShouldBeIndependent_WhenMultipleInstancesCreated()
    {
        // Arrange
        var status1 = new ToolFrameworkStatus();
        var status2 = new ToolFrameworkStatus();

        // Act
        status1.IsInitialized = true;
        status1.RegisteredToolsCount = 10;
        status1.StartupErrors.Add("Error in status 1");
        
        status2.TotalExecutions = 100;
        status2.StartupErrors.Add("Error in status 2");

        // Assert
        status1.IsInitialized.Should().BeTrue();
        status2.IsInitialized.Should().BeFalse();
        
        status1.RegisteredToolsCount.Should().Be(10);
        status2.RegisteredToolsCount.Should().Be(0);
        
        status1.TotalExecutions.Should().Be(0);
        status2.TotalExecutions.Should().Be(100);
        
        status1.StartupErrors.Should().ContainSingle().Which.Should().Be("Error in status 1");
        status2.StartupErrors.Should().ContainSingle().Which.Should().Be("Error in status 2");
    }

    [Fact]
    public void Status_ShouldHandleNullTimestamps()
    {
        // Arrange
        var status = new ToolFrameworkStatus();

        // Act & Assert
        status.InitializedAt.Should().BeNull();
        status.LastMaintenanceAt.Should().BeNull();
        
        // Should be able to check for null
        var isInitialized = status.InitializedAt.HasValue;
        isInitialized.Should().BeFalse();
    }

    [Fact]
    public void Status_ShouldRepresentRealWorldScenario()
    {
        // Arrange - Simulate framework lifecycle
        var status = new ToolFrameworkStatus();
        
        // Act - Initialization phase
        status.StartupErrors.Add("Plugin directory not found");
        status.IsInitialized = true;
        status.InitializedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        status.RegisteredToolsCount = 15;
        
        // Act - Running phase
        status.ActiveExecutionsCount = 2;
        status.TotalExecutions = 47;
        
        // Act - After maintenance
        status.LastMaintenanceAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        
        // Assert
        status.IsInitialized.Should().BeTrue();
        status.RegisteredToolsCount.Should().Be(15);
        status.ActiveExecutionsCount.Should().Be(2);
        status.TotalExecutions.Should().Be(47);
        status.InitializedAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(-30), TimeSpan.FromSeconds(1));
        status.LastMaintenanceAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(-5), TimeSpan.FromSeconds(1));
        status.StartupErrors.Should().ContainSingle().Which.Should().Be("Plugin directory not found");
    }
}