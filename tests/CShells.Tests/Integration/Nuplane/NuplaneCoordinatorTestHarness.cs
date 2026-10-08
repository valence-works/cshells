using System.Reflection;
using System.Runtime.ExceptionServices;
using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using CShells.Nuplane;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nuplane.Abstractions;

namespace CShells.Tests.Integration.Nuplane;

internal sealed class TestOptionsMonitor<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
    where TOptions : class
{
    private TOptions _currentValue = currentValue;
    private Exception? _readException;
    private int _readCount;

    public int ReadCount => Volatile.Read(ref _readCount);

    public TOptions CurrentValue => Get(Options.DefaultName);

    public TOptions Get(string? name)
    {
        Interlocked.Increment(ref _readCount);
        if (Volatile.Read(ref _readException) is { } exception)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();

        return Volatile.Read(ref _currentValue);
    }

    public IDisposable? OnChange(Action<TOptions, string?> listener) => NoOpDisposable.Instance;

    public void Replace(TOptions options) => Volatile.Write(ref _currentValue, options);

    public void ThrowOnRead(Exception? exception) => Volatile.Write(ref _readException, exception);

    private sealed class NoOpDisposable : IDisposable
    {
        public static NoOpDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

internal sealed class TestRuntimeFeatureCatalog : IRuntimeFeatureCatalog
{
    public int RefreshCount { get; private set; }

    public Func<int, CancellationToken, Task>? RefreshHandler { get; set; }

    public IRuntimeFeatureCatalogSnapshot CurrentSnapshot => new TestSnapshot();

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<IRuntimeFeatureCatalogSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var refreshNumber = ++RefreshCount;
        if (RefreshHandler is { } refreshHandler)
            await refreshHandler(refreshNumber, cancellationToken).ConfigureAwait(false);

        return CurrentSnapshot;
    }

    private sealed record TestSnapshot : IRuntimeFeatureCatalogSnapshot
    {
        public long Generation => 0;

        public DateTimeOffset RefreshedAt => DateTimeOffset.UtcNow;

        public IReadOnlyList<RuntimeFeatureDescriptor> FeatureDescriptors => [];
    }
}

public class TestShellRegistry : DispatchProxy
{
    private int _activeReadCount;

    public bool HasActiveShell { get; set; } = true;

    public int ReloadCount { get; private set; }

    public int ActiveShellReadCount => _activeReadCount;

    public IReadOnlyList<ReloadResult> Results { get; set; } = [new ReloadResult("test", null, null, null)];

    public Func<int, CancellationToken, Task<IReadOnlyList<ReloadResult>>>? ReloadHandler { get; set; }

    public Action<int>? ActiveShellsRead { get; set; }

    public Func<int, IReadOnlyList<IShell>>? ActiveShellsHandler { get; set; }

    public static IShellRegistry Create(out TestShellRegistry state)
    {
        var registry = DispatchProxy.Create<IShellRegistry, TestShellRegistry>();
        state = (TestShellRegistry)(object)registry;
        return registry;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(IShellRegistry.GetActiveShells))
        {
            var activeRead = Interlocked.Increment(ref _activeReadCount);
            ActiveShellsRead?.Invoke(activeRead);
            return ActiveShellsHandler is { } activeShellsHandler
                ? activeShellsHandler(activeRead)
                : HasActiveShell ? new IShell[] { null! } : Array.Empty<IShell>();
        }

        if (targetMethod?.Name == nameof(IShellRegistry.ReloadActiveAsync))
        {
            var reloadCount = ++ReloadCount;
            var cancellationToken = args is { Length: > 1 } ? (CancellationToken)args[1]! : default;
            return ReloadHandler is { } reloadHandler
                ? reloadHandler(reloadCount, cancellationToken)
                : Task.FromResult(Results);
        }

        throw new NotSupportedException($"The test registry does not support {targetMethod?.Name}.");
    }
}

internal static class NuplaneCoordinatorTestCases
{
    public static ServiceCollection CreateAdapterServices(
        IRuntimeFeatureCatalog catalog,
        IShellRegistry registry,
        NuplaneTestLoggerProvider? loggerProvider = null)
    {
        var services = new ServiceCollection();
        if (loggerProvider is not null)
        {
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(loggerProvider);
            });
        }

        services.AddCShells(shells => shells.WithNuplaneFeatureDiscovery());
        services.RemoveAll<IRuntimeFeatureCatalog>();
        services.AddSingleton(catalog);
        services.RemoveAll<IShellRegistry>();
        services.AddSingleton(registry);
        return services;
    }

    public static void AssertAdapterError(NuplaneTestLoggerProvider loggerProvider, Exception exception, string correlationId)
    {
        var entry = Assert.Single(loggerProvider.Snapshot());
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Equal(nameof(INuplaneObserver.OnPackagesReconciledAsync), entry.Properties["Operation"]);
        Assert.Equal(correlationId, entry.Properties["CorrelationId"]);
    }

    public static Task NotifyAsync(
        INuplaneObserver observer,
        bool sourceChanged = true,
        bool applied = true,
        bool removed = false,
        CancellationToken cancellationToken = default)
    {
        var resolved = FakePackageAssemblyCatalog.Resolved("feature-package");
        var changeSet = sourceChanged
            ? FakePackageAssemblyCatalog.ChangeSet(added: [resolved], removed: removed ? ["removed-package"] : null)
            : FakePackageAssemblyCatalog.ChangeSet();
        IReadOnlyList<ResolvedPackage> appliedPackages = applied ? [resolved] : [];
        if (removed)
            changeSet = FakePackageAssemblyCatalog.ChangeSet(removed: ["removed-package"]);

        return observer.OnPackagesReconciledAsync(changeSet, appliedPackages, cancellationToken);
    }

    public static async Task RunWithGateCleanupAsync(Func<Task> exercise, Action releaseGates, params Func<Task?>[] inFlightTasks)
    {
        Exception? primaryFailure = null;
        try
        {
            await exercise();
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
        }
        finally
        {
            releaseGates();
        }

        var cleanupFailures = new List<Exception>();
        foreach (var getTask in inFlightTasks)
        {
            if (getTask() is not { } task)
                continue;

            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }
        }

        if (primaryFailure is not null)
        {
            if (cleanupFailures.Count > 0)
                throw new AggregateException("The gated test failed and cleanup also failed.", [primaryFailure, .. cleanupFailures]);

            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }

        if (cleanupFailures.Count == 1)
            ExceptionDispatchInfo.Capture(cleanupFailures[0]).Throw();
        if (cleanupFailures.Count > 1)
            throw new AggregateException("Gated test cleanup failed.", cleanupFailures);
    }
}
