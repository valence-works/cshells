using Microsoft.Extensions.Configuration;

namespace CShells.Configuration;

/// <summary>
/// Provides shell settings from the .NET configuration system (e.g., appsettings.json, environment variables).
/// </summary>
public class ConfigurationShellSettingsProvider : IShellSettingsProvider
{
    private readonly IConfiguration _configuration;
    private readonly string _sectionName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationShellSettingsProvider"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="sectionName">The configuration section name (default: "CShells").</param>
    public ConfigurationShellSettingsProvider(IConfiguration configuration, string sectionName = CShellsOptions.SectionName)
    {
        _configuration = Guard.Against.Null(configuration);
        _sectionName = Guard.Against.NullOrWhiteSpace(sectionName);
    }

    /// <inheritdoc />
    public Task<IEnumerable<ShellSettings>> GetShellSettingsAsync(CancellationToken cancellationToken = default)
    {
        var shellsSection = _configuration.GetSection(_sectionName).GetSection("Shells");
        var shellConfigurations = shellsSection.GetChildren().ToList();

        if (shellConfigurations.Count == 0)
        {
            throw new InvalidOperationException($"No shells configured in the configuration section '{_sectionName}'.");
        }

        var shells = shellConfigurations.Select(section => ShellSettingsFactory.CreateFromConfiguration(section));
        return Task.FromResult(shells);
    }
}
