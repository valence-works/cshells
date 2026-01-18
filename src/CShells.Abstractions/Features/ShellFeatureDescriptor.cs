namespace CShells.Features;

/// <summary>
/// Describes a shell feature including its identity, dependencies, metadata, and startup type.
/// </summary>
public class ShellFeatureDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShellFeatureDescriptor"/> class.
    /// </summary>
    public ShellFeatureDescriptor()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellFeatureDescriptor"/> class with the specified ID.
    /// </summary>
    /// <param name="id">The unique identifier for the feature.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is null.</exception>
    public ShellFeatureDescriptor(string id)
    {
        Guard.Against.Null(id);
        Id = id;
    }

    /// <summary>
    /// Gets or initializes the unique identifier for this feature.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    public IReadOnlyList<string> Dependencies
    {
        get;
        set => field = Guard.Against.Null(value);
    } = [];

    public IDictionary<string, object> Metadata
    {
        get;
        set => field = Guard.Against.Null(value);
    } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or initializes the startup type that implements <c>IShellStartup</c> for this feature.
    /// </summary>
    public Type? StartupType { get; init; }
}
