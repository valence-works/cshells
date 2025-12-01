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
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Name);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Name);
        ArgumentNullException.ThrowIfNull(enabledFeatures);
        Id = id;
        EnabledFeatures = enabledFeatures;
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
}
