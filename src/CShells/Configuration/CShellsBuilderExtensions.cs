using CShells.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CShells.Configuration;

/// <summary>
/// Extension methods for <see cref="CShellsBuilder"/>.
/// </summary>
public static class CShellsBuilderExtensions
{
    /// <summary>
    /// Configures CShells to use the shells defined via the fluent API.
    /// This registers an <see cref="InMemoryShellSettingsProvider"/> with the configured shells.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <returns>The builder for method chaining.</returns>
    public static DependencyInjection.CShellsBuilder WithInMemoryShells(this DependencyInjection.CShellsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var shells = builder.GetShells();
        if (shells.Count == 0)
        {
            throw new InvalidOperationException(
                "No shells have been configured. Use AddShell() to configure at least one shell before calling WithInMemoryShells().");
        }

        builder.Services.TryAddSingleton<IShellSettingsProvider>(
            new InMemoryShellSettingsProvider(shells));

        return builder;
    }
}
