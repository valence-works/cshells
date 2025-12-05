using System.Collections.Concurrent;

namespace CShells.Configuration;

/// <summary>
/// Default implementation of <see cref="IShellSettingsCache"/> that provides thread-safe,
/// in-memory caching of shell settings while preserving insertion order.
/// </summary>
public class ShellSettingsCache : IShellSettingsCache
{
    private readonly ConcurrentDictionary<ShellId, ShellSettings> _cache = new();
    // Ordered list to preserve insertion order (ConcurrentDictionary.Values doesn't guarantee order)
    private List<ShellSettings> _orderedSettings = [];
    private readonly object _lock = new();

    /// <inheritdoc />
    public IReadOnlyCollection<ShellSettings> GetAll()
    {
        lock (_lock)
        {
            return _orderedSettings.ToList();
        }
    }

    /// <inheritdoc />
    public ShellSettings? GetById(ShellId id)
    {
        _cache.TryGetValue(id, out var settings);
        return settings;
    }

    /// <summary>
    /// Loads shell settings into the cache, replacing any existing entries.
    /// </summary>
    /// <param name="settings">The shell settings to cache.</param>
    public void Load(IEnumerable<ShellSettings> settings)
    {
        var list = Guard.Against.Null(settings).ToList();

        lock (_lock)
        {
            _cache.Clear();
            _orderedSettings = list;

            foreach (var shell in _orderedSettings)
            {
                _cache[shell.Id] = shell;
            }
        }
    }

    /// <summary>
    /// Clears all cached shell settings.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _orderedSettings.Clear();
        }
    }
}
