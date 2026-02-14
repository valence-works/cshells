using System.Text.Json;
using System.Text.Json.Serialization;

namespace CShells.Configuration;

/// <summary>
/// JSON converter for <see cref="List{FeatureEntry}"/> that handles polymorphic feature arrays.
/// </summary>
public class FeatureEntryListJsonConverter : JsonConverter<List<FeatureEntry>>
{
    private static readonly FeatureEntryJsonConverter ItemConverter = new();

    /// <inheritdoc/>
    public override List<FeatureEntry> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"Expected array, but found {reader.TokenType}");

        var entries = new List<FeatureEntry>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            var entry = ItemConverter.Read(ref reader, typeof(FeatureEntry), options);
            entries.Add(entry);
        }

        return entries;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, List<FeatureEntry> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var entry in value)
        {
            ItemConverter.Write(writer, entry, options);
        }

        writer.WriteEndArray();
    }
}

