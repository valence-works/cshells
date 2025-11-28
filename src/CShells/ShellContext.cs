namespace CShells;

/// <summary>
/// Represents an initialized shell with its settings and service provider.
/// </summary>
public class ShellContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShellContext"/> class.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    /// <param name="serviceProvider">The service provider for this shell.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="serviceProvider"/> is null.</exception>
    public ShellContext(ShellSettings settings, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        Settings = settings;
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the shell settings.
    /// </summary>
    public ShellSettings Settings { get; }

    /// <summary>
    /// Gets the service provider for this shell.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the shell identifier.
    /// </summary>
    public ShellId Id => Settings.Id;
}
