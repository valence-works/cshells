namespace CShells.Configuration;

/// <summary>
/// Composite implementation of <see cref="IShellSettingsProvider"/> that aggregates multiple providers.
/// This enables shells to be loaded from multiple sources (e.g., configuration, database, code-first).
/// </summary>
/// <remarks>
/// Providers are queried in the order they were registered. If multiple providers return shells
/// with the same ID, the last provider's shell wins (later providers override earlier ones).
/// </remarks>
public class CompositeShellSettingsProvider(IEnumerable<IShellSettingsProvider> providers) : IShellSettingsProvider
{
    private readonly IReadOnlyList<IShellSettingsProvider> _providers = Guard.Against.Null(providers).ToList();

    /// <inheritdoc />
    public async Task<IEnumerable<ShellSettings>> GetShellSettingsAsync(CancellationToken cancellationToken = default)
    {
        var shellsById = new Dictionary<ShellId, ShellSettings>();

        // Query all providers and merge results (last provider wins for duplicate IDs)
        foreach (var provider in _providers)
        {
            var shells = await provider.GetShellSettingsAsync(cancellationToken);
            
            foreach (var shell in shells)
            {
                shellsById[shell.Id] = shell;
            }
        }

        return shellsById.Values;
    }
}

