namespace CShells;

/// <summary>
/// Holds shell configuration including shell ID, enabled features, and arbitrary properties.
/// </summary>
public class ShellSettings
{
    public ShellSettings() { }

    public ShellSettings(ShellId id) => Id = id;

    public ShellSettings(ShellId id, IReadOnlyList<string> enabledFeatures)
    {
        Id = id;
        EnabledFeatures = Guard.Against.Null(enabledFeatures);
    }

    /// <summary>
    /// Gets or initializes the shell identifier.
    /// </summary>
    public ShellId Id { get; init; }

    /// <summary>
    /// Gets or sets the list of enabled features for this shell.
    /// </summary>
    public IReadOnlyList<string> EnabledFeatures
    {
        get;
        set => field = [..value];
    } = [];

    /// <summary>
    /// Gets or sets arbitrary properties associated with this shell.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets shell-specific configuration data.
    /// </summary>
    public IDictionary<string, object> ConfigurationData { get; set; } = new Dictionary<string, object>();
}
