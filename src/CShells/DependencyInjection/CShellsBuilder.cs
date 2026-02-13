using CShells.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.DependencyInjection;

/// <summary>
/// Builder for configuring CShells services with a fluent API.
/// Supports both provider-based and code-first shell configuration.
/// </summary>
public class CShellsBuilder
{
    private readonly List<ShellSettings> _codeFirstShells = new();
    private readonly List<Action<IServiceProvider, List<IShellSettingsProvider>>> _providerRegistrations = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CShellsBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public CShellsBuilder(IServiceCollection services)
    {
        Services = Guard.Against.Null(services);
    }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets all code-first shell settings configured via AddShell.
    /// </summary>
    internal IReadOnlyList<ShellSettings> CodeFirstShells => _codeFirstShells.AsReadOnly();

    /// <summary>
    /// Registers a provider registration action.
    /// </summary>
    internal void RegisterProvider(Action<IServiceProvider, List<IShellSettingsProvider>> registration)
    {
        _providerRegistrations.Add(registration);
    }

    /// <summary>
    /// Builds all registered providers and returns them.
    /// </summary>
    internal List<IShellSettingsProvider> BuildProviders(IServiceProvider serviceProvider)
    {
        var providers = new List<IShellSettingsProvider>();
        
        foreach (var registration in _providerRegistrations)
        {
            registration(serviceProvider, providers);
        }
        
        return providers;
    }

    /// <summary>
    /// Adds a shell using a fluent builder.
    /// </summary>
    /// <param name="configure">Configuration action for the shell builder.</param>
    /// <returns>This builder for method chaining.</returns>
    public CShellsBuilder AddShell(Action<Configuration.ShellBuilder> configure)
    {
        Guard.Against.Null(configure);
        var shellBuilder = new Configuration.ShellBuilder(new ShellId(Guid.NewGuid().ToString()));
        configure(shellBuilder);
        _codeFirstShells.Add(shellBuilder.Build());
        return this;
    }

    /// <summary>
    /// Adds a shell with the specified ID using a fluent builder.
    /// </summary>
    /// <param name="id">The shell identifier.</param>
    /// <param name="configure">Configuration action for the shell builder.</param>
    /// <returns>This builder for method chaining.</returns>
    public CShellsBuilder AddShell(string id, Action<Configuration.ShellBuilder> configure)
    {
        Guard.Against.Null(id);
        Guard.Against.Null(configure);
        var shellBuilder = new Configuration.ShellBuilder(new ShellId(id));
        configure(shellBuilder);
        _codeFirstShells.Add(shellBuilder.Build());
        return this;
    }

    /// <summary>
    /// Adds a pre-configured shell.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    /// <returns>This builder for method chaining.</returns>
    public CShellsBuilder AddShell(ShellSettings settings)
    {
        _codeFirstShells.Add(Guard.Against.Null(settings));
        return this;
    }
}
