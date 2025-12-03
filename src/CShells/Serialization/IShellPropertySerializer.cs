namespace CShells.Serialization;

/// <summary>
/// Provides serialization and deserialization services for shell properties.
/// </summary>
public interface IShellPropertySerializer
{
    /// <summary>
    /// Deserializes a property value to the specified type.
    /// </summary>
    /// <typeparam name="T">The target type to deserialize to.</typeparam>
    /// <param name="value">The value to deserialize.</param>
    /// <returns>The deserialized value, or default(T) if deserialization fails.</returns>
    T? Deserialize<T>(object? value);

    /// <summary>
    /// Deserializes a property value to the specified type.
    /// </summary>
    /// <param name="value">The value to deserialize.</param>
    /// <param name="targetType">The target type to deserialize to.</param>
    /// <returns>The deserialized value, or null if deserialization fails.</returns>
    object? Deserialize(object? value, Type targetType);

    /// <summary>
    /// Serializes a property value.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The serialized value.</returns>
    object? Serialize(object? value);
}
