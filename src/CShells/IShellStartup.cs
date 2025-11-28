using Microsoft.Extensions.DependencyInjection;

namespace CShells;

/// <summary>
/// Defines the contract for shell startup configuration classes.
/// This is the core interface that does not depend on ASP.NET Core.
/// </summary>
public interface IShellStartup
{
    /// <summary>
    /// Configures the services for the shell.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    void ConfigureServices(IServiceCollection services);
}
