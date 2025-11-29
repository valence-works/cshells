using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace CShells.AspNetCore;

/// <summary>
/// Extends <see cref="IShellFeature"/> with ASP.NET Core specific configuration.
/// </summary>
public interface IWebShellFeature : IShellFeature
{
    /// <summary>
    /// Configures the application pipeline for the shell feature.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="environment">The hosting environment, or null if not registered in the service provider.</param>
    void Configure(IApplicationBuilder app, IHostEnvironment? environment);
}
