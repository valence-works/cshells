namespace CShells;

/// <summary>
/// Holds shell configuration including shell ID, enabled features, and shell-specific configuration.
/// </summary>
public class ShellSettings
{
    public ShellSettings() { }

    public ShellSettings(ShellId id) => Id = id;

    public ShellSettings(ShellId id, IReadOnlyList<string> enabledFeatures)
    {
        Guard.Against.Null(enabledFeatures);
        Id = id;
        EnabledFeatures = enabledFeatures;
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
    /// Gets or sets shell-specific configuration data as a dictionary.
    /// Keys use colon-separated format for hierarchical data (e.g., "WebRouting:Path").
    /// </summary>
    public IDictionary<string, object> ConfigurationData { get; set; } = new Dictionary<string, object>();
}
