using System.Collections.Concurrent;

namespace CShells.Configuration;

/// <summary>
/// Mutable in-memory implementation of <see cref="IShellSettingsProvider"/> for code-first shell configuration.
/// This provider supports adding shells both at startup and at runtime.
/// </summary>
public class MutableInMemoryShellSettingsProvider : IShellSettingsProvider
{
    private readonly ConcurrentDictionary<ShellId, ShellSettings> _shells = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MutableInMemoryShellSettingsProvider"/> class.
    /// </summary>
    public MutableInMemoryShellSettingsProvider()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MutableInMemoryShellSettingsProvider"/> class
    /// with initial shell settings.
    /// </summary>
    /// <param name="shells">Initial shell settings.</param>
    public MutableInMemoryShellSettingsProvider(IEnumerable<ShellSettings> shells)
    {
        Guard.Against.Null(shells);
        
        var shellList = shells.ToList();
        foreach (var shell in shellList)
        {
            _shells[shell.Id] = shell;
        }
    }

    /// <summary>
    /// Adds or updates a shell in the provider.
    /// </summary>
    /// <param name="settings">The shell settings to add or update.</param>
    public void AddOrUpdate(ShellSettings settings)
    {
        Guard.Against.Null(settings);
        _shells[settings.Id] = settings;
    }

    /// <summary>
    /// Removes a shell from the provider.
    /// </summary>
    /// <param name="shellId">The shell ID to remove.</param>
    /// <returns>True if the shell was removed; false if it didn't exist.</returns>
    public bool Remove(ShellId shellId)
    {
        return _shells.TryRemove(shellId, out _);
    }

    /// <summary>
    /// Clears all shells from the provider.
    /// </summary>
    public void Clear()
    {
        _shells.Clear();
    }

    /// <inheritdoc />
    public Task<IEnumerable<ShellSettings>> GetShellSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<ShellSettings>>(_shells.Values);
    }
}


