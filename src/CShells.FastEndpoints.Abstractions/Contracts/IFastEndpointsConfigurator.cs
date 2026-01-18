using FastEndpoints;
using JetBrains.Annotations;

namespace CShells.FastEndpoints.Contracts;

/// <summary>
/// Allows shell features to customize FastEndpoints configuration.
/// Implementations should be registered in the shell's DI container during ConfigureServices.
/// </summary>
[PublicAPI]
public interface IFastEndpointsConfigurator
{
    /// <summary>
    /// Configures the FastEndpoints options for the shell.
    /// </summary>
    /// <param name="config">The FastEndpoints configuration to customize.</param>
    void Configure(Config config);
}
