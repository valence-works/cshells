namespace CShells.Configuration;

/// <summary>
/// Configuration model for a shell section in appsettings.json.
/// </summary>
public class ShellConfig
{
    /// <summary>
    /// Gets or sets the name of the shell.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of enabled features for this shell.
    /// </summary>
    public string?[] Features { get; set; } = [];

    /// <summary>
    /// Gets or sets arbitrary properties associated with this shell.
    /// Note: Configuration binding creates these as objects. They will be converted
    /// to JsonElement during ShellSettings creation for proper serialization support.
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>
    /// Gets or sets shell-specific configuration settings.
    /// These settings can be accessed through IConfiguration in the shell's service provider,
    /// allowing features to bind them to strongly-typed options classes.
    /// </summary>
    public Dictionary<string, object?> Settings { get; set; } = new();
}
