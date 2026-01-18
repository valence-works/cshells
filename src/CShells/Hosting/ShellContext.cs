namespace CShells.Hosting;

/// <summary>
/// Represents an initialized shell with its settings and service provider.
/// </summary>
public class ShellContext(ShellSettings settings, IServiceProvider serviceProvider, IReadOnlyList<string> enabledFeatures)
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

    /// <summary>
    /// Gets the list of enabled features for this shell, including resolved dependencies.
    /// </summary>
    /// <remarks>
    /// This list contains all features that are active for this shell, including:
    /// <list type="bullet">
    ///   <item><description>Features explicitly listed in <see cref="ShellSettings.EnabledFeatures"/></description></item>
    ///   <item><description>Features transitively required as dependencies</description></item>
    /// </list>
    /// Features are ordered by their dependency requirements (dependencies before dependents).
    /// </remarks>
    public IReadOnlyList<string> EnabledFeatures { get; } = Guard.Against.Null(enabledFeatures);
}
