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
    public ShellFeatureDescriptor(string id)
    {
        Id = id;
    }

    /// <summary>
    /// Gets or sets the unique identifier for this feature.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of feature IDs that this feature depends on.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets the metadata associated with this feature.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the startup type that implements <c>IShellStartup</c> for this feature.
    /// </summary>
    public Type? StartupType { get; set; }
}
