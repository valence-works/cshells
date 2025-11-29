namespace CShells.SampleApp.Background;

/// <summary>
/// A sample background service that demonstrates how to use shell context scopes
/// for executing background work within shell contexts.
/// </summary>
public class ShellDemoWorker : BackgroundService
{
    private readonly IShellHost _shellHost;
    private readonly IShellContextScopeFactory _scopeFactory;
    private readonly IBackgroundWorkObserver? _observer;
    private readonly ILogger<ShellDemoWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellDemoWorker"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host for accessing shell contexts.</param>
    /// <param name="scopeFactory">The factory for creating shell context scopes.</param>
    /// <param name="observer">Optional observer for monitoring background work execution.</param>
    /// <param name="logger">The logger instance.</param>
    public ShellDemoWorker(
        IShellHost shellHost,
        IShellContextScopeFactory scopeFactory,
        IBackgroundWorkObserver? observer,
        ILogger<ShellDemoWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(shellHost);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _shellHost = shellHost;
        _scopeFactory = scopeFactory;
        _observer = observer;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var shell in _shellHost.AllShells)
            {
                try
                {
                    await ExecuteForShellAsync(shell, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Shutdown requested, exit gracefully
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing background work for shell {ShellId}", shell.Id);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private Task ExecuteForShellAsync(ShellContext shell, CancellationToken _)
    {
        using var scope = _scopeFactory.CreateScope(shell);

        var workDescription = $"Background work executed for shell '{shell.Id.Name}'";
        _logger.LogInformation(workDescription);

        // Notify the observer if one is registered
        _observer?.OnWorkExecuted(shell.Id, workDescription);

        return Task.CompletedTask;
    }
}
