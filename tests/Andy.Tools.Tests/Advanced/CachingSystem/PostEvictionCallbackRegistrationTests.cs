using Andy.Tools.Advanced.CachingSystem;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Advanced.CachingSystem;

public class PostEvictionCallbackRegistrationTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var registration = new PostEvictionCallbackRegistration();

        // Assert
        registration.Should().NotBeNull();
        registration.EvictionCallback.Should().BeNull();
        registration.State.Should().BeNull();
    }

    [Fact]
    public void EvictionCallback_ShouldBeSettable()
    {
        // Arrange
        var registration = new PostEvictionCallbackRegistration();
        Action<object, object?, EvictionReason, object?> callback = (key, value, reason, state) => { };

        // Act
        registration.EvictionCallback = callback;

        // Assert
        registration.EvictionCallback.Should().BeSameAs(callback);
    }

    [Fact]
    public void State_ShouldBeSettable()
    {
        // Arrange
        var registration = new PostEvictionCallbackRegistration();
        var state = new { Id = 123, Name = "Test" };

        // Act
        registration.State = state;

        // Assert
        registration.State.Should().BeSameAs(state);
    }

    [Fact]
    public void Registration_ShouldSupportFluentConfiguration()
    {
        // Arrange
        var callbackInvoked = false;
        var stateObject = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var registration = new PostEvictionCallbackRegistration
        {
            EvictionCallback = (key, value, reason, state) => { callbackInvoked = true; },
            State = stateObject
        };

        // Assert
        registration.EvictionCallback.Should().NotBeNull();
        registration.State.Should().BeSameAs(stateObject);
        
        // Verify callback can be invoked
        registration.EvictionCallback!.Invoke("key", "value", EvictionReason.Expired, stateObject);
        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void EvictionCallback_ShouldCaptureCorrectParameters()
    {
        // Arrange
        object? capturedKey = null;
        object? capturedValue = null;
        EvictionReason? capturedReason = null;
        object? capturedState = null;

        var registration = new PostEvictionCallbackRegistration
        {
            EvictionCallback = (key, value, reason, state) =>
            {
                capturedKey = key;
                capturedValue = value;
                capturedReason = reason;
                capturedState = state;
            },
            State = "TestState"
        };

        // Act
        registration.EvictionCallback!.Invoke("TestKey", 42, EvictionReason.Capacity, "TestState");

        // Assert
        capturedKey.Should().Be("TestKey");
        capturedValue.Should().Be(42);
        capturedReason.Should().Be(EvictionReason.Capacity);
        capturedState.Should().Be("TestState");
    }

    [Fact]
    public void Registration_ShouldBeIndependent_WhenMultipleInstancesCreated()
    {
        // Arrange
        var registration1 = new PostEvictionCallbackRegistration();
        var registration2 = new PostEvictionCallbackRegistration();

        // Act
        registration1.EvictionCallback = (k, v, r, s) => { };
        registration1.State = "State1";
        
        registration2.State = "State2";

        // Assert
        registration1.EvictionCallback.Should().NotBeNull();
        registration2.EvictionCallback.Should().BeNull();
        
        registration1.State.Should().Be("State1");
        registration2.State.Should().Be("State2");
    }

    [Fact]
    public void EvictionCallback_ShouldHandleNullValues()
    {
        // Arrange
        var callbackExecuted = false;
        var registration = new PostEvictionCallbackRegistration
        {
            EvictionCallback = (key, value, reason, state) =>
            {
                callbackExecuted = true;
                key.Should().BeNull();
                value.Should().BeNull();
                state.Should().BeNull();
            }
        };

        // Act
        registration.EvictionCallback!.Invoke(null, null, EvictionReason.Removed, null);

        // Assert
        callbackExecuted.Should().BeTrue();
    }

    [Fact]
    public void State_ShouldAcceptAnyType()
    {
        // Arrange
        var registration = new PostEvictionCallbackRegistration();

        // Act & Assert - String state
        registration.State = "StringState";
        registration.State.Should().Be("StringState");

        // Act & Assert - Integer state
        registration.State = 42;
        registration.State.Should().Be(42);

        // Act & Assert - Complex object state
        var complexState = new List<KeyValuePair<string, int>> 
        { 
            new("key1", 1), 
            new("key2", 2) 
        };
        registration.State = complexState;
        registration.State.Should().BeSameAs(complexState);
    }
}