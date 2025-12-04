namespace CShells.Workbench.Features.Core;

/// <summary>
/// Configuration options for the Core feature.
/// These are bound from shell-specific configuration.
/// </summary>
public class CoreOptions
{
    /// <summary>
    /// Key to look up the connection string for this shell.
    /// </summary>
    public string ConnectionStringKey { get; set; } = "DefaultConnection";

    /// <summary>
    /// UI theme for this shell.
    /// </summary>
    public string Theme { get; set; } = "Light";

    /// <summary>
    /// Maximum upload size in megabytes for this shell.
    /// </summary>
    public int MaxUploadSizeMB { get; set; } = 10;

    /// <summary>
    /// Optional tenant tier (e.g., "Free", "Premium", "Enterprise").
    /// </summary>
    public string? Tier { get; set; }
}
