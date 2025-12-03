namespace CShells.Resolution;

/// <summary>
/// Provides a protocol-agnostic context for resolving shell identifiers.
/// </summary>
public class ShellResolutionContext
{
    public IDictionary<string, object> Data { get; init; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    public T? Get<T>(string key) => Data.TryGetValue(key, out var value) && value is T typed ? typed : default;

    public void Set<T>(string key, T value) where T : notnull => Data[key] = value;
}
