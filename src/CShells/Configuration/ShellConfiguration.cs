using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace CShells.Configuration;

/// <summary>
/// Provides shell-scoped configuration that merges shell-specific settings with the root application configuration.
/// Shell-specific settings take precedence over root configuration values.
/// </summary>
public class ShellConfiguration : IConfiguration
{
    private readonly IConfiguration _rootConfiguration;
    private readonly IConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellConfiguration"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings containing configuration data.</param>
    /// <param name="rootConfiguration">The root application configuration.</param>
    public ShellConfiguration(ShellSettings shellSettings, IConfiguration rootConfiguration)
    {
        Guard.Against.Null(shellSettings);
        Guard.Against.Null(rootConfiguration);

        _rootConfiguration = rootConfiguration;

        // Build a configuration from the shell's ConfigurationData
        var builder = new ConfigurationBuilder();

        if (shellSettings.ConfigurationData.Count > 0)
        {
            builder.AddInMemoryCollection(
                shellSettings.ConfigurationData.Select(kvp =>
                    new KeyValuePair<string, string?>(kvp.Key, kvp.Value?.ToString())));
        }

        _shellConfiguration = builder.Build();
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
        // Try shell configuration first, then fall back to root
        var shellSection = _shellConfiguration.GetSection(key);

        // Check if the shell section exists (has a value or children)
        if (shellSection.Exists())
        {
            return shellSection;
        }

        return _rootConfiguration.GetSection(key);
    }

    /// <inheritdoc />
    public IEnumerable<IConfigurationSection> GetChildren()
    {
        // Merge children from both configurations, shell takes precedence
        var shellChildren = _shellConfiguration.GetChildren().ToDictionary(s => s.Key);
        var rootChildren = _rootConfiguration.GetChildren().ToDictionary(s => s.Key);

        // Start with shell children (they take precedence)
        var merged = new Dictionary<string, IConfigurationSection>(shellChildren);

        // Add root children that don't exist in shell
        foreach (var rootChild in rootChildren)
        {
            if (!merged.ContainsKey(rootChild.Key))
            {
                merged[rootChild.Key] = rootChild.Value;
            }
        }

        return merged.Values;
    }

    /// <inheritdoc />
    public IChangeToken GetReloadToken()
    {
        // Return a composite change token that triggers when either configuration changes
        return new CompositeChangeToken(new[]
        {
            _shellConfiguration.GetReloadToken(),
            _rootConfiguration.GetReloadToken()
        });
    }
}

/// <summary>
/// A change token that represents multiple change tokens.
/// </summary>
internal class CompositeChangeToken : IChangeToken
{
    private readonly IChangeToken[] _changeTokens;

    public CompositeChangeToken(IChangeToken[] changeTokens)
    {
        _changeTokens = changeTokens;
    }

    public bool HasChanged => _changeTokens.Any(t => t.HasChanged);

    public bool ActiveChangeCallbacks => _changeTokens.Any(t => t.ActiveChangeCallbacks);

    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
    {
        var registrations = _changeTokens
            .Select(token => token.RegisterChangeCallback(callback, state))
            .ToArray();

        return new CompositeDisposable(registrations);
    }
}

/// <summary>
/// Disposes multiple disposables.
/// </summary>
internal class CompositeDisposable : IDisposable
{
    private readonly IDisposable[] _disposables;

    public CompositeDisposable(IDisposable[] disposables)
    {
        _disposables = disposables;
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }
}
