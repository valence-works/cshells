using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CShells.Hosting;

internal sealed class ShellActivationRunner(
    IShellRegistry registry,
    IServiceProvider rootServices,
    ILogger<ShellActivationRunner> logger) : IShellActivationRunner
{
    private readonly IShellRegistry _registry = Guard.Against.Null(registry);
    private readonly IServiceProvider _rootServices = Guard.Against.Null(rootServices);
    private readonly ILogger<ShellActivationRunner> _logger = Guard.Against.Null(logger);

    public IShellActivationRun Start(
        IReadOnlyList<string> shellNames,
        ShellActivationRetryPolicy? retryPolicy = null,
        IShellActivationAttemptObserver? observer = null,
        CancellationToken startupCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shellNames);

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new List<string>(shellNames.Count);
        for (var index = 0; index < shellNames.Count; index++)
        {
            var name = shellNames[index];
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Shell names must not be null, empty, or whitespace.", nameof(shellNames));

            if (unique.Add(name))
                targets.Add(name);
        }

        var timeProvider = _rootServices.GetService<TimeProvider>() ?? TimeProvider.System;
        return new ShellActivationRun(_registry, timeProvider, _logger, targets, retryPolicy, observer, startupCancellationToken);
    }
}
