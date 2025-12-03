using CShells.Serialization;
using System.Text.Json;

namespace CShells.Tests.Unit.Serialization;

public class SystemTextJsonShellPropertySerializerTests
{
    private readonly SystemTextJsonShellPropertySerializer _serializer = new();

    [Fact(DisplayName = "Deserialize with null value returns null")]
    public void Deserialize_WithNullValue_ReturnsNull()
    {
        // Act
        var result = _serializer.Deserialize<string>(null);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Deserialize string value returns string")]
    public void Deserialize_WithStringValue_ReturnsString()
    {
        // Arrange
        const string value = "test";

        // Act
        var result = _serializer.Deserialize<string>(value);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact(DisplayName = "Deserialize JsonElement string returns string")]
    public void Deserialize_WithJsonElementString_ReturnsString()
    {
        // Arrange
        var jsonElement = JsonDocument.Parse("\"test\"").RootElement;

        // Act
        var result = _serializer.Deserialize<string>(jsonElement);

        // Assert
        Assert.Equal("test", result);
    }

    [Fact(DisplayName = "Deserialize JsonElement object to complex type")]
    public void Deserialize_WithJsonElementObject_ReturnsComplexType()
    {
        // Arrange
        var json = """{"name": "John", "age": 30}""";
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _serializer.Deserialize<TestPerson>(jsonElement);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("John", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact(DisplayName = "Deserialize JSON string to complex type")]
    public void Deserialize_WithJsonString_ReturnsComplexType()
    {
        // Arrange
        var json = """{"name": "Jane", "age": 25}""";

        // Act
        var result = _serializer.Deserialize<TestPerson>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Jane", result.Name);
        Assert.Equal(25, result.Age);
    }

    [Fact(DisplayName = "Deserialize with non-generic method and Type parameter")]
    public void Deserialize_WithTypeParameter_ReturnsObject()
    {
        // Arrange
        var json = """{"name": "Bob", "age": 35}""";
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _serializer.Deserialize(jsonElement, typeof(TestPerson));

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TestPerson>(result);
        var person = (TestPerson)result;
        Assert.Equal("Bob", person.Name);
        Assert.Equal(35, person.Age);
    }

    [Fact(DisplayName = "Deserialize with null targetType throws ArgumentNullException")]
    public void Deserialize_WithNullTargetType_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => _serializer.Deserialize("test", null!));
        Assert.Equal("targetType", ex.ParamName);
    }

    [Fact(DisplayName = "Deserialize value already of target type returns value")]
    public void Deserialize_WithCorrectType_ReturnsValue()
    {
        // Arrange
        var person = new TestPerson { Name = "Alice", Age = 28 };

        // Act
        var result = _serializer.Deserialize<TestPerson>(person);

        // Assert
        Assert.Same(person, result);
    }

    [Fact(DisplayName = "Deserialize invalid JSON string returns null")]
    public void Deserialize_WithInvalidJsonString_ReturnsNull()
    {
        // Arrange
        const string invalidJson = "not valid json";

        // Act
        var result = _serializer.Deserialize<TestPerson>(invalidJson);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Serialize null value returns null")]
    public void Serialize_WithNullValue_ReturnsNull()
    {
        // Act
        var result = _serializer.Serialize(null);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Serialize string value returns string")]
    public void Serialize_WithStringValue_ReturnsString()
    {
        // Arrange
        const string value = "test";

        // Act
        var result = _serializer.Serialize(value);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact(DisplayName = "Serialize primitive value returns primitive")]
    public void Serialize_WithPrimitiveValue_ReturnsPrimitive()
    {
        // Arrange
        const int value = 42;

        // Act
        var result = _serializer.Serialize(value);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact(DisplayName = "Serialize complex object returns JsonElement")]
    public void Serialize_WithComplexObject_ReturnsJsonElement()
    {
        // Arrange
        var person = new TestPerson { Name = "Charlie", Age = 40 };

        // Act
        var result = _serializer.Serialize(person);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<JsonElement>(result);
        var jsonElement = (JsonElement)result;
        Assert.Equal("Charlie", jsonElement.GetProperty("name").GetString());
        Assert.Equal(40, jsonElement.GetProperty("age").GetInt32());
    }

    [Fact(DisplayName = "Serializer with custom options uses those options")]
    public void Serializer_WithCustomOptions_UsesCustomOptions()
    {
        // Arrange
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        var serializer = new SystemTextJsonShellPropertySerializer(options);
        var person = new TestPerson { Name = "David", Age = 45 };

        // Act
        var result = serializer.Serialize(person);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<JsonElement>(result);
        var jsonElement = (JsonElement)result;
        // Should use snake_case naming
        Assert.True(jsonElement.TryGetProperty("name", out _));
    }

    [Fact(DisplayName = "Round-trip serialization preserves data")]
    public void RoundTrip_PreservesData()
    {
        // Arrange
        var original = new TestPerson { Name = "Eve", Age = 50 };

        // Act
        var serialized = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize<TestPerson>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Age, deserialized.Age);
    }

    [Fact(DisplayName = "Deserialize handles nested complex objects")]
    public void Deserialize_WithNestedObjects_ReturnsCorrectStructure()
    {
        // Arrange
        var json = """{"name": "Frank", "age": 55, "address": {"street": "Main St", "city": "NYC"}}""";
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var result = _serializer.Deserialize<TestPersonWithAddress>(jsonElement);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Frank", result.Name);
        Assert.Equal(55, result.Age);
        Assert.NotNull(result.Address);
        Assert.Equal("Main St", result.Address.Street);
        Assert.Equal("NYC", result.Address.City);
    }

    private class TestPerson
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    private class TestPersonWithAddress
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public TestAddress? Address { get; set; }
    }

    private class TestAddress
    {
        public string? Street { get; set; }
        public string? City { get; set; }
    }
}
