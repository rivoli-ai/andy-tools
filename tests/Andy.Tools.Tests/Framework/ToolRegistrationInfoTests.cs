using Andy.Tools.Framework;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Framework;

public class ToolRegistrationInfoTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var info = new ToolRegistrationInfo();

        // Assert
        info.ToolType.Should().Be(typeof(object));
        info.Configuration.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ToolType_ShouldBeSettable()
    {
        // Arrange
        var info = new ToolRegistrationInfo();
        var toolType = typeof(SampleTool);

        // Act
        info.ToolType = toolType;

        // Assert
        info.ToolType.Should().Be(toolType);
    }

    [Fact]
    public void Configuration_ShouldBeSettable()
    {
        // Arrange
        var info = new ToolRegistrationInfo();
        var config = new Dictionary<string, object?>
        {
            ["timeout"] = 30,
            ["enabled"] = true,
            ["name"] = "Test Tool"
        };

        // Act
        info.Configuration = config;

        // Assert
        info.Configuration.Should().BeSameAs(config);
        info.Configuration.Should().HaveCount(3);
    }

    [Fact]
    public void Configuration_ShouldSupportAddingItems()
    {
        // Arrange
        var info = new ToolRegistrationInfo();

        // Act
        info.Configuration["key1"] = "value1";
        info.Configuration["key2"] = 42;
        info.Configuration["key3"] = true;

        // Assert
        info.Configuration.Should().HaveCount(3);
        info.Configuration["key1"].Should().Be("value1");
        info.Configuration["key2"].Should().Be(42);
        info.Configuration["key3"].Should().Be(true);
    }

    [Fact]
    public void Configuration_ShouldSupportNullValues()
    {
        // Arrange
        var info = new ToolRegistrationInfo();

        // Act
        info.Configuration["nullable"] = null;

        // Assert
        info.Configuration.Should().ContainKey("nullable");
        info.Configuration["nullable"].Should().BeNull();
    }

    [Fact]
    public void RegistrationInfo_ShouldSupportFluentConfiguration()
    {
        // Arrange & Act
        var info = new ToolRegistrationInfo
        {
            ToolType = typeof(AnotherSampleTool),
            Configuration = new Dictionary<string, object?>
            {
                ["setting1"] = "value1",
                ["setting2"] = 123,
                ["setting3"] = false
            }
        };

        // Assert
        info.ToolType.Should().Be(typeof(AnotherSampleTool));
        info.Configuration.Should().HaveCount(3);
        info.Configuration["setting1"].Should().Be("value1");
        info.Configuration["setting2"].Should().Be(123);
        info.Configuration["setting3"].Should().Be(false);
    }

    [Fact]
    public void RegistrationInfo_ShouldBeIndependent_WhenMultipleInstancesCreated()
    {
        // Arrange
        var info1 = new ToolRegistrationInfo();
        var info2 = new ToolRegistrationInfo();

        // Act
        info1.ToolType = typeof(SampleTool);
        info1.Configuration["key"] = "value1";
        
        info2.ToolType = typeof(AnotherSampleTool);
        info2.Configuration["key"] = "value2";

        // Assert
        info1.ToolType.Should().Be(typeof(SampleTool));
        info2.ToolType.Should().Be(typeof(AnotherSampleTool));
        
        info1.Configuration["key"].Should().Be("value1");
        info2.Configuration["key"].Should().Be("value2");
    }

    [Fact]
    public void Configuration_ShouldSupportComplexTypes()
    {
        // Arrange
        var info = new ToolRegistrationInfo();
        var complexValue = new
        {
            Name = "Complex",
            Settings = new Dictionary<string, int>
            {
                ["timeout"] = 30,
                ["retries"] = 3
            }
        };

        // Act
        info.Configuration["complex"] = complexValue;

        // Assert
        var stored = info.Configuration["complex"];
        stored.Should().NotBeNull();
        stored.Should().BeSameAs(complexValue);
    }

    [Fact]
    public void RegistrationInfo_ShouldWorkWithRealToolTypes()
    {
        // Arrange
        var infos = new List<ToolRegistrationInfo>
        {
            new()
            {
                ToolType = typeof(SampleTool),
                Configuration = new Dictionary<string, object?>
                {
                    ["enabled"] = true,
                    ["timeout"] = TimeSpan.FromSeconds(30)
                }
            },
            new()
            {
                ToolType = typeof(AnotherSampleTool),
                Configuration = new Dictionary<string, object?>
                {
                    ["enabled"] = false,
                    ["maxRetries"] = 5
                }
            }
        };

        // Act & Assert
        infos.Should().HaveCount(2);
        infos[0].ToolType.Should().Be(typeof(SampleTool));
        infos[0].Configuration["enabled"].Should().Be(true);
        infos[0].Configuration["timeout"].Should().Be(TimeSpan.FromSeconds(30));
        
        infos[1].ToolType.Should().Be(typeof(AnotherSampleTool));
        infos[1].Configuration["enabled"].Should().Be(false);
        infos[1].Configuration["maxRetries"].Should().Be(5);
    }

    // Sample test classes
    private class SampleTool { }
    private class AnotherSampleTool { }
}