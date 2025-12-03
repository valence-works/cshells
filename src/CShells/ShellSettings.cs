namespace CShells;

/// <summary>
/// Holds shell configuration including shell ID, enabled features, and arbitrary properties.
/// </summary>
public class ShellSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShellSettings"/> class.
    /// </summary>
    public ShellSettings()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellSettings"/> class with the specified shell ID.
    /// </summary>
    /// <param name="id">The shell identifier.</param>
    public ShellSettings(ShellId id)
    {
        Id = id;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellSettings"/> class with the specified shell ID and enabled features.
    /// </summary>
    /// <param name="id">The shell identifier.</param>
    /// <param name="enabledFeatures">The list of enabled features.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="enabledFeatures"/> is null.</exception>
    public ShellSettings(ShellId id, IReadOnlyList<string> enabledFeatures)
    {
        Id = id;
        // ShellId validates its own Name; only need to guard the collection.
        EnabledFeatures = Guard.Against.Null(enabledFeatures);
    }

    /// <summary>
    /// Gets or initializes the shell identifier.
    /// </summary>
    public ShellId Id { get; init; }

    /// <summary>
    /// Gets or sets the list of enabled features for this shell.
    /// The setter creates a defensive copy to maintain immutability.
    /// </summary>
    public IReadOnlyList<string> EnabledFeatures
    {
        get;
        set => field = value.ToArray();
    } = [];

    /// <summary>
    /// Gets or sets arbitrary properties associated with this shell.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets shell-specific configuration data.
    /// This data is used to create a shell-scoped IConfiguration instance
    /// that can be injected into services within the shell's service provider.
    /// </summary>
    public IDictionary<string, object> ConfigurationData { get; set; } = new Dictionary<string, object>();
}
