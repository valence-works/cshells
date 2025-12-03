using Microsoft.Extensions.Configuration;

namespace CShells.Configuration;

/// <summary>
/// Provides shell settings from the .NET configuration system (e.g., appsettings.json, environment variables).
/// </summary>
public class ConfigurationShellSettingsProvider(IConfiguration configuration, string sectionName = CShellsOptions.SectionName) : IShellSettingsProvider
{
    private readonly IConfiguration _configuration = Guard.Against.Null(configuration);
    private readonly string _sectionName = Guard.Against.NullOrWhiteSpace(sectionName);

    /// <inheritdoc />
    public Task<IEnumerable<ShellSettings>> GetShellSettingsAsync(CancellationToken cancellationToken = default)
    {
        var shellsSection = _configuration.GetSection(_sectionName).GetSection("Shells");
        var shellConfigurations = shellsSection.GetChildren().ToList();

        if (shellConfigurations.Count == 0)
            throw new InvalidOperationException($"No shells configured in the configuration section '{_sectionName}'.");

        var shells = shellConfigurations.Select(ShellSettingsFactory.CreateFromConfiguration);
        return Task.FromResult(shells);
    }
}
