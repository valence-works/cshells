using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace CShells.AspNetCore;

/// <summary>
/// Extends <see cref="CShells.IShellStartup"/> with ASP.NET Core specific configuration.
/// </summary>
public interface IShellStartupWithPipeline : CShells.IShellStartup
{
    /// <summary>
    /// Configures the application pipeline for the shell.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="environment">The hosting environment.</param>
    void Configure(IApplicationBuilder app, IHostEnvironment environment);
}
