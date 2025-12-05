using CShells.Configuration;
using CShells.Hosting;
using CShells.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Management;

/// <summary>
/// Default implementation of <see cref="IShellManager"/> that manages shell lifecycle
/// and publishes notifications for shell state changes.
/// </summary>
public class DefaultShellManager : IShellManager
{
    private readonly IShellHost _shellHost;
    private readonly ShellSettingsCache _cache;
    private readonly IShellSettingsProvider _provider;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<DefaultShellManager> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShellManager"/> class.
    /// </summary>
    public DefaultShellManager(
        IShellHost shellHost,
        ShellSettingsCache cache,
        IShellSettingsProvider provider,
        INotificationPublisher notificationPublisher,
        ILogger<DefaultShellManager>? logger = null)
    {
        _shellHost = shellHost;
        _cache = cache;
        _provider = provider;
        _notificationPublisher = notificationPublisher;
        _logger = logger ?? NullLogger<DefaultShellManager>.Instance;
    }

    /// <inheritdoc />
    public async Task AddShellAsync(ShellSettings settings, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(settings);

        lock (_lock)
        {
            _logger.LogInformation("Adding shell '{ShellId}'", settings.Id);

            // Add to cache
            _cache.Load(_cache.GetAll().Append(settings));

            // Build shell context (this triggers feature service registration)
            _ = _shellHost.GetShell(settings.Id);

            _logger.LogInformation("Shell '{ShellId}' added successfully", settings.Id);
        }

        // Publish notification (outside lock to avoid deadlocks)
        await _notificationPublisher.PublishAsync(new ShellAddedNotification(settings), strategy: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveShellAsync(ShellId shellId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _logger.LogInformation("Removing shell '{ShellId}'", shellId);

            // Remove from cache
            var remainingShells = _cache.GetAll().Where(s => !s.Id.Equals(shellId));
            _cache.Clear();
            _cache.Load(remainingShells);

            _logger.LogInformation("Shell '{ShellId}' removed successfully", shellId);
        }

        // Publish notification (outside lock to avoid deadlocks)
        await _notificationPublisher.PublishAsync(new ShellRemovedNotification(shellId), strategy: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateShellAsync(ShellSettings settings, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(settings);

        _logger.LogInformation("Updating shell '{ShellId}'", settings.Id);

        // Remove existing shell
        await RemoveShellAsync(settings.Id, cancellationToken);

        // Add updated shell
        await AddShellAsync(settings, cancellationToken);

        _logger.LogInformation("Shell '{ShellId}' updated successfully", settings.Id);
    }

    /// <inheritdoc />
    public async Task ReloadAllShellsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reloading all shells from provider");

        // Load fresh shell settings from provider
        var settings = await _provider.GetShellSettingsAsync(cancellationToken);
        var settingsList = settings.ToList();

        lock (_lock)
        {
            // Update cache
            _cache.Clear();
            _cache.Load(settingsList);
        }

        _logger.LogInformation("Reloaded {Count} shell(s)", settingsList.Count);

        // Publish notification (outside lock to avoid deadlocks)
        await _notificationPublisher.PublishAsync(new ShellsReloadedNotification(settingsList), strategy: null, cancellationToken);
    }
}
