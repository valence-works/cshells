using CShells.Serialization;
using System.Text.Json;

namespace CShells.Tests.Unit;

public class ShellSettingsExtensionsTests
{
    [Fact(DisplayName = "GetProperty with string value returns string")]
    public void GetProperty_WithStringValue_ReturnsString()
    {
        // Arrange
        var settings = new ShellSettings
        {
            Properties = new Dictionary<string, object>
            {
                ["key"] = "value"
            }
        };

        // Act
        var result = settings.GetProperty<string>("key");

        // Assert
        Assert.Equal("value", result);
    }

    [Fact(DisplayName = "GetProperty with JsonElement returns deserialized value")]
    public void GetProperty_WithJsonElement_ReturnsDeserializedValue()
    {
        // Arrange
        var settings = new ShellSettings
        {
            Properties = new Dictionary<string, object>
            {
                ["key"] = JsonDocument.Parse("\"value\"").RootElement
            }
        };

        // Act
        var result = settings.GetProperty<string>("key");

        // Assert
        Assert.Equal("value", result);
    }

    [Fact(DisplayName = "GetProperty with complex object returns deserialized object")]
    public void GetProperty_WithComplexObject_ReturnsDeserializedObject()
    {
        // Arrange
        var json = """{"name": "Test", "value": 42}""";
        var settings = new ShellSettings
        {
            Properties = new Dictionary<string, object>
            {
                ["key"] = JsonDocument.Parse(json).RootElement
            }
        };

        // Act
        var result = settings.GetProperty<TestData>("key");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Equal(42, result.Value);
    }

    [Fact(DisplayName = "GetProperty with non-existent key returns default")]
    public void GetProperty_WithNonExistentKey_ReturnsDefault()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act
        var result = settings.GetProperty<string>("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "GetProperty with null settings throws ArgumentNullException")]
    public void GetProperty_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        ShellSettings? settings = null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => settings!.GetProperty<string>("key"));
        Assert.Equal("settings", ex.ParamName);
    }

    [Fact(DisplayName = "GetProperty with null key throws ArgumentException")]
    public void GetProperty_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => settings.GetProperty<string>(null!));
    }

    [Fact(DisplayName = "GetProperty with whitespace key throws ArgumentException")]
    public void GetProperty_WithWhitespaceKey_ThrowsArgumentException()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => settings.GetProperty<string>("   "));
    }

    [Fact(DisplayName = "GetProperty non-generic with Type parameter returns object")]
    public void GetProperty_NonGeneric_ReturnsObject()
    {
        // Arrange
        var json = """{"name": "Test", "value": 42}""";
        var settings = new ShellSettings
        {
            Properties = new Dictionary<string, object>
            {
                ["key"] = JsonDocument.Parse(json).RootElement
            }
        };

        // Act
        var result = settings.GetProperty("key", typeof(TestData));

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TestData>(result);
        var data = (TestData)result;
        Assert.Equal("Test", data.Name);
        Assert.Equal(42, data.Value);
    }

    [Fact(DisplayName = "GetProperty non-generic with null Type throws ArgumentNullException")]
    public void GetProperty_NonGeneric_WithNullType_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => settings.GetProperty("key", null!));
        Assert.Equal("targetType", ex.ParamName);
    }

    [Fact(DisplayName = "SetProperty stores value in properties dictionary")]
    public void SetProperty_StoresValueInDictionary()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act
        settings.SetProperty("key", "value");

        // Assert
        Assert.True(settings.Properties.ContainsKey("key"));
        Assert.Equal("value", settings.Properties["key"]);
    }

    [Fact(DisplayName = "SetProperty with complex object serializes value")]
    public void SetProperty_WithComplexObject_SerializesValue()
    {
        // Arrange
        var settings = new ShellSettings();
        var data = new TestData { Name = "Test", Value = 42 };

        // Act
        settings.SetProperty("key", data);

        // Assert
        Assert.True(settings.Properties.ContainsKey("key"));
        Assert.IsType<JsonElement>(settings.Properties["key"]);
    }

    [Fact(DisplayName = "SetProperty with null settings throws ArgumentNullException")]
    public void SetProperty_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        ShellSettings? settings = null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => settings!.SetProperty("key", "value"));
        Assert.Equal("settings", ex.ParamName);
    }

    [Fact(DisplayName = "SetProperty with null key throws ArgumentException")]
    public void SetProperty_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => settings.SetProperty<string>(null!, "value"));
    }

    [Fact(DisplayName = "TryGetProperty with existing key returns true and value")]
    public void TryGetProperty_WithExistingKey_ReturnsTrueAndValue()
    {
        // Arrange
        var settings = new ShellSettings
        {
            Properties = new Dictionary<string, object>
            {
                ["key"] = "value"
            }
        };

        // Act
        var result = settings.TryGetProperty<string>("key", out var value);

        // Assert
        Assert.True(result);
        Assert.Equal("value", value);
    }

    [Fact(DisplayName = "TryGetProperty with non-existent key returns false")]
    public void TryGetProperty_WithNonExistentKey_ReturnsFalse()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act
        var result = settings.TryGetProperty<string>("nonexistent", out var value);

        // Assert
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact(DisplayName = "TryGetProperty with null settings throws ArgumentNullException")]
    public void TryGetProperty_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        ShellSettings? settings = null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => settings!.TryGetProperty<string>("key", out _));
        Assert.Equal("settings", ex.ParamName);
    }

    [Fact(DisplayName = "GetProperty with custom serializer uses that serializer")]
    public void GetProperty_WithCustomSerializer_UsesCustomSerializer()
    {
        // Arrange
        var settings = new ShellSettings
        {
            Properties = new Dictionary<string, object>
            {
                ["key"] = "custom_value"
            }
        };
        var customSerializer = new TestCustomSerializer();

        // Act
        var result = settings.GetProperty<string>("key", customSerializer);

        // Assert
        Assert.Equal("CUSTOM_VALUE", result); // Our test serializer uppercases
    }

    [Fact(DisplayName = "SetProperty with custom serializer uses that serializer")]
    public void SetProperty_WithCustomSerializer_UsesCustomSerializer()
    {
        // Arrange
        var settings = new ShellSettings();
        var customSerializer = new TestCustomSerializer();

        // Act
        settings.SetProperty("key", "value", customSerializer);

        // Assert
        Assert.Equal("VALUE", settings.Properties["key"]); // Our test serializer uppercases
    }

    [Fact(DisplayName = "DefaultSerializer can be set and retrieved")]
    public void DefaultSerializer_CanBeSetAndRetrieved()
    {
        // Arrange
        var originalSerializer = ShellSettingsExtensions.DefaultSerializer;
        var newSerializer = new SystemTextJsonShellPropertySerializer();

        try
        {
            // Act
            ShellSettingsExtensions.DefaultSerializer = newSerializer;

            // Assert
            Assert.Same(newSerializer, ShellSettingsExtensions.DefaultSerializer);
        }
        finally
        {
            // Cleanup
            ShellSettingsExtensions.DefaultSerializer = originalSerializer;
        }
    }

    [Fact(DisplayName = "DefaultSerializer with null value throws ArgumentNullException")]
    public void DefaultSerializer_WithNullValue_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ShellSettingsExtensions.DefaultSerializer = null!);
    }

    [Fact(DisplayName = "Round-trip set and get preserves data")]
    public void RoundTrip_SetAndGet_PreservesData()
    {
        // Arrange
        var settings = new ShellSettings();
        var original = new TestData { Name = "Test", Value = 123 };

        // Act
        settings.SetProperty("key", original);
        var result = settings.GetProperty<TestData>("key");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(original.Name, result.Name);
        Assert.Equal(original.Value, result.Value);
    }

    private class TestData
    {
        public string? Name { get; set; }
        public int Value { get; set; }
    }

    private class TestCustomSerializer : IShellPropertySerializer
    {
        public T? Deserialize<T>(object? value)
        {
            if (value is string str)
                return (T)(object)str.ToUpperInvariant();
            return default;
        }

        public object? Deserialize(object? value, Type targetType)
        {
            if (value is string str)
                return str.ToUpperInvariant();
            return null;
        }

        public object? Serialize(object? value)
        {
            if (value is string str)
                return str.ToUpperInvariant();
            return value;
        }
    }
}
