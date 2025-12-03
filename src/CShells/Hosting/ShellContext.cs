namespace CShells.Hosting;

/// <summary>
/// Represents an initialized shell with its settings and service provider.
/// </summary>
public class ShellContext(ShellSettings settings, IServiceProvider serviceProvider)
{
    /// <summary>
    /// Gets the shell settings.
    /// </summary>
    public ShellSettings Settings { get; } = Guard.Against.Null(settings);

    /// <summary>
    /// Gets the service provider for this shell.
    /// </summary>
    public IServiceProvider ServiceProvider { get; } = Guard.Against.Null(serviceProvider);

    /// <summary>
    /// Gets the shell identifier.
    /// </summary>
    public ShellId Id => Settings.Id;
}
