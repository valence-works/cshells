namespace CShells;

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
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    /// <summary>
    /// Gets or initializes the unique identifier for this feature.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of feature IDs that this feature depends on.
    /// </summary>
    private IReadOnlyList<string> _dependencies = [];
    public IReadOnlyList<string> Dependencies
    {
        get => _dependencies;
        set => _dependencies = value ?? throw new ArgumentNullException(nameof(Dependencies), "Dependencies cannot be set to null.");
    }

    /// <summary>
    /// Gets or sets the metadata associated with this feature.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or initializes the startup type that implements <c>IShellStartup</c> for this feature.
    /// </summary>
    public Type? StartupType { get; init; }
}
