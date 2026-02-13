using CShells.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Hosting;

/// <summary>
/// Ensures all shells are activated during application startup and deactivated during shutdown.
/// </summary>
/// <remarks>
/// This hosted service coordinates shell lifecycle with the application lifecycle by:
/// <list type="bullet">
///   <item><description>Publishing <see cref="ShellActivated"/> notifications for all configured shells on startup</description></item>
///   <item><description>Publishing <see cref="ShellDeactivating"/> notifications for all shells on shutdown</description></item>
/// </list>
/// </remarks>
public class ShellStartupHostedService : IHostedService
{
    private readonly IShellHost _shellHost;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<ShellStartupHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellStartupHostedService"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host containing all configured shells.</param>
    /// <param name="notificationPublisher">The notification publisher for shell lifecycle events.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public ShellStartupHostedService(
        IShellHost shellHost,
        INotificationPublisher notificationPublisher,
        ILogger<ShellStartupHostedService>? logger = null)
    {
        _shellHost = Guard.Against.Null(shellHost);
        _notificationPublisher = Guard.Against.Null(notificationPublisher);
        _logger = logger ?? NullLogger<ShellStartupHostedService>.Instance;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activating all shells on application startup");

        var shells = _shellHost.AllShells;
        _logger.LogInformation("Found {ShellCount} shell(s) to activate", shells.Count);

        foreach (var shell in shells)
        {
            try
            {
                _logger.LogDebug("Publishing ShellActivated notification for shell '{ShellId}'", shell.Id);
                await _notificationPublisher.PublishAsync(
                    new ShellActivated(shell),
                    strategy: null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate shell '{ShellId}' during application startup", shell.Id);
                throw;
            }
        }

        _logger.LogInformation("Successfully activated {ShellCount} shell(s)", shells.Count);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deactivating all shells on application shutdown");

        IReadOnlyCollection<ShellContext> shells;
        try
        {
            shells = _shellHost.AllShells;
        }
        catch (ObjectDisposedException)
        {
            _logger.LogDebug("Shell host already disposed, skipping deactivation");
            return;
        }

        _logger.LogInformation("Found {ShellCount} shell(s) to deactivate", shells.Count);

        var failedCount = 0;

        foreach (var shell in shells)
        {
            try
            {
                _logger.LogDebug("Publishing ShellDeactivating notification for shell '{ShellId}'", shell.Id);
                await _notificationPublisher.PublishAsync(
                    new ShellDeactivating(shell),
                    strategy: null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                failedCount++;
                // Log but don't throw during shutdown - attempt to deactivate all shells
                _logger.LogError(ex, "Failed to deactivate shell '{ShellId}' during application shutdown", shell.Id);
            }
        }

        if (failedCount > 0)
        {
            _logger.LogWarning("Deactivated {SuccessCount}/{TotalCount} shell(s) ({FailedCount} failed)",
                shells.Count - failedCount, shells.Count, failedCount);
        }
        else
        {
            _logger.LogInformation("Successfully deactivated {ShellCount} shell(s)", shells.Count);
        }
    }
}
