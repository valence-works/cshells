using System.Text.Json;
using CShells.Configuration;

namespace CShells.Tests.Unit.Configuration;

public class FeatureEntryJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new FeatureEntryJsonConverter() }
    };

    [Fact(DisplayName = "Deserialize string feature entry")]
    public void Deserialize_StringFeatureEntry_ReturnsFeatureEntryWithName()
    {
        // Arrange
        var json = "\"Core\"";

        // Act
        var entry = JsonSerializer.Deserialize<FeatureEntry>(json, Options);

        // Assert
        Assert.NotNull(entry);
        Assert.Equal("Core", entry.Name);
        Assert.Empty(entry.Settings);
    }

    [Fact(DisplayName = "Deserialize object feature entry with settings")]
    public void Deserialize_ObjectFeatureEntry_ReturnsFeatureEntryWithSettings()
    {
        // Arrange
        var json = """{ "Name": "FraudDetection", "Threshold": 0.85, "MaxAmount": 5000 }""";

        // Act
        var entry = JsonSerializer.Deserialize<FeatureEntry>(json, Options);

        // Assert
        Assert.NotNull(entry);
        Assert.Equal("FraudDetection", entry.Name);
        Assert.Equal(2, entry.Settings.Count);
        Assert.Equal(0.85, entry.Settings["Threshold"]);
        Assert.Equal(5000, entry.Settings["MaxAmount"]);
    }

    [Fact(DisplayName = "Deserialize object feature entry with nested settings")]
    public void Deserialize_ObjectFeatureEntryWithNestedSettings_PreservesStructure()
    {
        // Arrange
        var json = """{ "Name": "Database", "Connection": { "Server": "localhost", "Port": 5432 } }""";

        // Act
        var entry = JsonSerializer.Deserialize<FeatureEntry>(json, Options);

        // Assert
        Assert.NotNull(entry);
        Assert.Equal("Database", entry.Name);
        Assert.Single(entry.Settings);
        var connectionElement = Assert.IsType<JsonElement>(entry.Settings["Connection"]);
        Assert.Equal("localhost", connectionElement.GetProperty("Server").GetString());
        Assert.Equal(5432, connectionElement.GetProperty("Port").GetInt32());
    }

    [Fact(DisplayName = "Deserialize array of mixed feature entries")]
    public void Deserialize_MixedArray_ReturnsCorrectEntries()
    {
        // Arrange
        var json = """["Core", { "Name": "FraudDetection", "Threshold": 0.85 }, "Logging"]""";
        var options = new JsonSerializerOptions
        {
            Converters = { new FeatureEntryListJsonConverter() }
        };

        // Act
        var entries = JsonSerializer.Deserialize<List<FeatureEntry>>(json, options);

        // Assert
        Assert.NotNull(entries);
        Assert.Equal(3, entries.Count);

        Assert.Equal("Core", entries[0].Name);
        Assert.Empty(entries[0].Settings);

        Assert.Equal("FraudDetection", entries[1].Name);
        Assert.Single(entries[1].Settings);
        Assert.Equal(0.85, entries[1].Settings["Threshold"]);

        Assert.Equal("Logging", entries[2].Name);
        Assert.Empty(entries[2].Settings);
    }

    [Fact(DisplayName = "Serialize feature entry without settings as string")]
    public void Serialize_FeatureEntryWithoutSettings_ReturnsString()
    {
        // Arrange
        var entry = FeatureEntry.FromName("Core");

        // Act
        var json = JsonSerializer.Serialize(entry, Options);

        // Assert
        Assert.Equal("\"Core\"", json);
    }

    [Fact(DisplayName = "Serialize feature entry with settings as object")]
    public void Serialize_FeatureEntryWithSettings_ReturnsObject()
    {
        // Arrange
        var entry = new FeatureEntry
        {
            Name = "FraudDetection",
            Settings = new() { ["Threshold"] = 0.85 }
        };

        // Act
        var json = JsonSerializer.Serialize(entry, Options);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("FraudDetection", root.GetProperty("Name").GetString());
        Assert.Equal(0.85, root.GetProperty("Threshold").GetDouble());
    }

    [Fact(DisplayName = "Deserialize trims whitespace from feature names")]
    public void Deserialize_TrimsWhitespace_FromFeatureNames()
    {
        // Arrange
        var json = "\"  Core  \"";

        // Act
        var entry = JsonSerializer.Deserialize<FeatureEntry>(json, Options);

        // Assert
        Assert.NotNull(entry);
        Assert.Equal("Core", entry.Name);
    }

    [Fact(DisplayName = "Deserialize throws for empty feature name")]
    public void Deserialize_EmptyFeatureName_ThrowsJsonException()
    {
        // Arrange
        var json = "\"\"";

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FeatureEntry>(json, Options));
    }

    [Fact(DisplayName = "Deserialize throws for object without Name property")]
    public void Deserialize_ObjectWithoutName_ThrowsJsonException()
    {
        // Arrange
        var json = """{ "Threshold": 0.85 }""";

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FeatureEntry>(json, Options));
    }

    [Fact(DisplayName = "Deserialize handles case-insensitive Name property")]
    public void Deserialize_CaseInsensitiveName_Works()
    {
        // Arrange
        var json = """{ "name": "FraudDetection", "Threshold": 0.85 }""";

        // Act
        var entry = JsonSerializer.Deserialize<FeatureEntry>(json, Options);

        // Assert
        Assert.NotNull(entry);
        Assert.Equal("FraudDetection", entry.Name);
    }

    [Fact(DisplayName = "Deserialize handles boolean settings")]
    public void Deserialize_BooleanSettings_Works()
    {
        // Arrange
        var json = """{ "Name": "Feature", "Enabled": true, "Debug": false }""";

        // Act
        var entry = JsonSerializer.Deserialize<FeatureEntry>(json, Options);

        // Assert
        Assert.NotNull(entry);
        Assert.Equal(true, entry.Settings["Enabled"]);
        Assert.Equal(false, entry.Settings["Debug"]);
    }

    [Fact(DisplayName = "Deserialize handles string settings")]
    public void Deserialize_StringSettings_Works()
    {
        // Arrange
        var json = """{ "Name": "Database", "ConnectionString": "Server=localhost" }""";

        // Act
        var entry = JsonSerializer.Deserialize<FeatureEntry>(json, Options);

        // Assert
        Assert.NotNull(entry);
        Assert.Equal("Server=localhost", entry.Settings["ConnectionString"]);
    }

    [Fact(DisplayName = "Deserialize handles array settings")]
    public void Deserialize_ArraySettings_Works()
    {
        // Arrange
        var json = """{ "Name": "Feature", "Tags": ["tag1", "tag2"] }""";

        // Act
        var entry = JsonSerializer.Deserialize<FeatureEntry>(json, Options);

        // Assert
        Assert.NotNull(entry);
        var tagsElement = Assert.IsType<JsonElement>(entry.Settings["Tags"]);
        Assert.Equal(JsonValueKind.Array, tagsElement.ValueKind);
        Assert.Equal(2, tagsElement.GetArrayLength());
    }
}

