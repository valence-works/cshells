using CShells.Configuration;
using CShells.DependencyInjection;
using CShells.AspNetCore.Resolution;
using CShells.Resolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CShells.AspNetCore.Configuration;

/// <summary>
/// Extension methods for <see cref="CShellsBuilder"/> that require async shell loading.
/// </summary>
public static class AsyncCShellsBuilderExtensions
{
    /// <summary>
    /// Automatically registers shell resolution strategies based on shell properties from the configured provider.
    /// This method loads shells from the provider and scans for Path and Host properties.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The builder for method chaining.</returns>
    public static async Task<CShellsBuilder> WithAutoResolversAsync(this CShellsBuilder builder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Build a temporary service provider to get the shell settings provider
        var tempProvider = builder.Services.BuildServiceProvider();
        var settingsProvider = tempProvider.GetService<IShellSettingsProvider>();

        if (settingsProvider == null)
        {
            throw new InvalidOperationException(
                "No IShellSettingsProvider has been registered. " +
                "Call a provider registration method (e.g., WithFluentStorageProvider) before calling WithAutoResolversAsync.");
        }

        // Load shell settings from the provider
        var shells = await settingsProvider.GetShellSettingsAsync(cancellationToken);
        var shellsList = shells.ToList();

        // Collect path and host mappings from shell properties
        var pathMappings = new Dictionary<string, ShellId>(StringComparer.OrdinalIgnoreCase);
        var hostMappings = new Dictionary<string, ShellId>(StringComparer.OrdinalIgnoreCase);

        foreach (var shell in shellsList)
        {
            if (shell.Properties.TryGetValue(ShellPropertyKeys.Path, out var pathValue))
            {
                var path = pathValue switch
                {
                    string s => s,
                    System.Text.Json.JsonElement jsonElement when jsonElement.ValueKind == System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                    _ => null
                };

                if (path != null)
                {
                    pathMappings[path] = shell.Id;
                }
            }

            if (shell.Properties.TryGetValue(ShellPropertyKeys.Host, out var hostValue))
            {
                var host = hostValue switch
                {
                    string s => s,
                    System.Text.Json.JsonElement jsonElement when jsonElement.ValueKind == System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                    _ => null
                };

                if (host != null)
                {
                    hostMappings[host] = shell.Id;
                }
            }
        }

        // Register path resolver if any path mappings exist
        // Use AddSingleton (not TryAddEnumerable) to ensure our strategies are registered
        // even if DefaultShellResolverStrategy is already registered
        if (pathMappings.Count > 0)
        {
            builder.Services.AddSingleton<IShellResolverStrategy>(
                new PathShellResolver(pathMappings));
        }

        // Register host resolver if any host mappings exist
        if (hostMappings.Count > 0)
        {
            builder.Services.AddSingleton<IShellResolverStrategy>(
                new HostShellResolver(hostMappings));
        }

        return builder;
    }
}
