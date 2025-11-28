namespace CShells.Configuration;

/// <summary>
/// Transforms configuration models into runtime ShellSettings instances.
/// </summary>
public static class ShellSettingsFactory
{
    /// <summary>
    /// Creates a <see cref="ShellSettings"/> instance from a <see cref="ShellConfig"/>.
    /// </summary>
    /// <param name="config">The shell configuration.</param>
    /// <returns>A new <see cref="ShellSettings"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public static ShellSettings Create(ShellConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var shellId = new ShellId(config.Name);
        var normalizedFeatures = config.Features
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f!.Trim())
            .ToArray();
        var settings = new ShellSettings(shellId, normalizedFeatures);

        foreach (var property in config.Properties)
        {
            settings.Properties[property.Key] = property.Value;
        }

        return settings;
    }

    /// <summary>
    /// Creates a collection of <see cref="ShellSettings"/> instances from <see cref="CShellsOptions"/>.
    /// </summary>
    /// <param name="options">The CShells options.</param>
    /// <returns>A collection of <see cref="ShellSettings"/> instances.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public static IReadOnlyList<ShellSettings> CreateAll(CShellsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var duplicates = (options.Shells
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)).ToArray();

        if (duplicates.Any())
        {
            throw new ArgumentException($"Duplicate shell names found: {string.Join(", ", duplicates)}", nameof(options));
        }
        return options.Shells.Select(Create).ToList();
    }
}
