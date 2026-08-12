using CShells.Lifecycle;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Hosting;

/// <summary>
/// Host shutdown coordinator: drains every active shell using the configured drain policy and
/// disposes them, emergency-disposing any that don't complete within the host's shutdown timeout.
/// </summary>
/// <remarks>
/// Shell activation is lazy — blueprints activate on first touch (via
/// <see cref="IShellRegistry.GetOrActivateAsync"/>, typically from routing middleware).
/// This service's only responsibility is orderly shutdown.
/// </remarks>
internal sealed class CShellsStartupHostedService(
    IShellRegistry registry,
    ILogger<CShellsStartupHostedService>? logger = null) : IHostedService
{
    private readonly IShellRegistry _registry = Guard.Against.Null(registry);
    private readonly ILogger<CShellsStartupHostedService> _logger = logger ?? NullLogger<CShellsStartupHostedService>.Instance;
    private readonly object _shutdownGate = new();
    private Task? _shutdownTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_shutdownGate)
            return _shutdownTask ??= ExecuteShutdownAsync(cancellationToken);
    }

    private async Task ExecuteShutdownAsync(CancellationToken cancellationToken)
    {
        // The registry's in-memory index is the authoritative source of active shells at
        // shutdown. GetActiveShells reads only the in-memory slot dict — zero I/O — so
        // shutdown is decoupled from provider/storage availability.
        var actives = _registry.GetActiveShells().ToList();

        if (actives.Count == 0)
            return;

        _logger.LogInformation("CShells shutdown: draining {Count} active shell(s).", actives.Count);

        var drains = await Task.WhenAll(actives.Select(s => _registry.DrainAsync(s, cancellationToken))).ConfigureAwait(false);

        // Wait for every drain to complete OR the shutdown token to fire. Drain operations
        // already dispose their shell at the end of the normal path; shutdown-timeout breach
        // forces emergency disposal via the registry.
        var waitTasks = drains.Select(d => d.WaitAsync(cancellationToken));
        try
        {
            await Task.WhenAll(waitTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "CShells shutdown exceeded the host's shutdown timeout; emergency-disposing shells whose drain did not complete.");

            // Emergency dispose anything still not Disposed.
            foreach (var shell in actives.OfType<Lifecycle.Shell>())
            {
                if (shell.State != Lifecycle.ShellLifecycleState.Disposed)
                    await shell.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
