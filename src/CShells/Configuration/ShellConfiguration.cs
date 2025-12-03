using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace CShells.Configuration;

/// <summary>
/// Provides shell-scoped configuration that merges shell-specific settings with the root application configuration.
/// Shell-specific settings take precedence over root configuration values.
/// </summary>
public class ShellConfiguration(ShellSettings shellSettings, IConfiguration rootConfiguration) : IConfiguration
{
    private readonly IConfiguration _rootConfiguration = Guard.Against.Null(rootConfiguration);
    private readonly IConfiguration _shellConfiguration = BuildShellConfiguration(Guard.Against.Null(shellSettings));

    private static IConfiguration BuildShellConfiguration(ShellSettings settings)
    {
        var builder = new ConfigurationBuilder();

        if (settings.ConfigurationData.Count > 0)
        {
            builder.AddInMemoryCollection(
                settings.ConfigurationData.Select(kvp =>
                    new KeyValuePair<string, string?>(kvp.Key, kvp.Value?.ToString())));
        }

        return builder.Build();
    }

    /// <inheritdoc />
    public string? this[string key]
    {
        get => _shellConfiguration[key] ?? _rootConfiguration[key];
        set => throw new NotSupportedException("ShellConfiguration is read-only.");
    }

    /// <inheritdoc />
    public IConfigurationSection GetSection(string key)
    {
        var shellSection = _shellConfiguration.GetSection(key);
        return shellSection.Exists() ? shellSection : _rootConfiguration.GetSection(key);
    }

    /// <inheritdoc />
    public IEnumerable<IConfigurationSection> GetChildren()
    {
        var shellChildren = _shellConfiguration.GetChildren().ToDictionary(s => s.Key);
        var rootChildren = _rootConfiguration.GetChildren().ToDictionary(s => s.Key);

        // Start with shell children (they take precedence)
        var merged = new Dictionary<string, IConfigurationSection>(shellChildren);

        // Add root children that don't exist in shell
        foreach (var rootChild in rootChildren.Where(rc => !merged.ContainsKey(rc.Key)))
        {
            merged[rootChild.Key] = rootChild.Value;
        }

        return merged.Values;
    }

    /// <inheritdoc />
    public IChangeToken GetReloadToken() => new CompositeChangeToken([_shellConfiguration.GetReloadToken(), _rootConfiguration.GetReloadToken()]);
}

/// <summary>
/// A change token that represents multiple change tokens.
/// </summary>
internal sealed class CompositeChangeToken(IChangeToken[] changeTokens) : IChangeToken
{
    public bool HasChanged => changeTokens.Any(t => t.HasChanged);
    public bool ActiveChangeCallbacks => changeTokens.Any(t => t.ActiveChangeCallbacks);

    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
    {
        var registrations = changeTokens.Select(token => token.RegisterChangeCallback(callback, state)).ToArray();
        return new CompositeDisposable(registrations);
    }
}

/// <summary>
/// Disposes multiple disposables.
/// </summary>
internal sealed class CompositeDisposable(IDisposable[] disposables) : IDisposable
{
    public void Dispose()
    {
        foreach (var disposable in disposables)
            disposable.Dispose();
    }
}
