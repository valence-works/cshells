using CShells.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.DependencyInjection;

/// <summary>Opt-in registration for the generic shell activation runner.</summary>
public static class ShellActivationRunnerServiceCollectionExtensions
{
    /// <summary>Registers a root singleton activation runner without starting or hosting a run.</summary>
    /// <param name="services">The host service collection.</param>
    /// <returns>The same service collection for further registration.</returns>
    /// <remarks>
    /// Registration is TryAdd-style and may be called before <see cref="ServiceCollectionExtensions.AddCShells"/>.
    /// The runner resolves <see cref="CShells.Lifecycle.IShellRegistry"/> only when the root service provider
    /// resolves the runner. This method does not add an <see cref="Microsoft.Extensions.Hosting.IHostedService"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddShellActivationRunner(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ILogger<ShellActivationRunner>>(serviceProvider =>
            serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<ShellActivationRunner>()
            ?? NullLogger<ShellActivationRunner>.Instance);
        services.TryAddSingleton<IShellActivationRunner, ShellActivationRunner>();
        return services;
    }
}
