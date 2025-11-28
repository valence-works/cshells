using Microsoft.Extensions.DependencyInjection;

namespace CShells;

/// <summary>
/// Defines the contract for a shell feature.
/// </summary>
public interface IShellFeature
{
    /// <summary>
    /// Configures the services for the shell feature.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    void ConfigureServices(IServiceCollection services);
}
