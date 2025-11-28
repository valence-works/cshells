namespace CShells;

/// <summary>
/// Holds shell configuration including shell ID, enabled features, and arbitrary properties.
/// </summary>
public class ShellSettings
{
    /// <summary>
    /// Gets or sets the shell identifier.
    /// </summary>
    public ShellId Id { get; set; }

    /// <summary>
    /// Gets or sets the list of enabled features for this shell.
    /// </summary>
    public IReadOnlyList<string> EnabledFeatures { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets arbitrary properties associated with this shell.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

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
    public ShellSettings(ShellId id, IReadOnlyList<string> enabledFeatures)
    {
        Id = id;
        EnabledFeatures = enabledFeatures ?? throw new ArgumentNullException(nameof(enabledFeatures));
    }
}
