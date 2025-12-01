using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Configuration;

/// <summary>
/// Background service that initializes the <see cref="ShellSettingsCache"/> at application startup
/// by loading shell settings from an <see cref="IShellSettingsProvider"/>.
/// </summary>
public class ShellSettingsCacheInitializer : IHostedService
{
    private readonly IShellSettingsProvider _provider;
    private readonly ShellSettingsCache _cache;
    private readonly ILogger<ShellSettingsCacheInitializer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellSettingsCacheInitializer"/> class.
    /// </summary>
    /// <param name="provider">The shell settings provider to load settings from.</param>
    /// <param name="cache">The cache to populate.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public ShellSettingsCacheInitializer(
        IShellSettingsProvider provider,
        ShellSettingsCache cache,
        ILogger<ShellSettingsCacheInitializer>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(cache);
        _provider = provider;
        _cache = cache;
        _logger = logger ?? NullLogger<ShellSettingsCacheInitializer>.Instance;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Loading shell settings into cache...");

        try
        {
            var settings = await _provider.GetShellSettingsAsync(cancellationToken);
            var settingsList = settings.ToList();
            _cache.Load(settingsList);

            _logger.LogInformation("Loaded {Count} shell(s) into cache", settingsList.Count);

            // Note: We don't publish ShellsReloadedNotification here because MapCShells() will handle
            // endpoint registration for shells loaded during startup. This avoids duplicate registrations.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load shell settings into cache");
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cache.Clear();
        return Task.CompletedTask;
    }
}
