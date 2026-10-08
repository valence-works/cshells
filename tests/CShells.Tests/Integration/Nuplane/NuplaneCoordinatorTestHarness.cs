using System.Reflection;
using CShells.Features;
using CShells.Lifecycle;
using Nuplane.Abstractions;

namespace CShells.Tests.Integration.Nuplane;

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
            return HasActiveShell ? new IShell[] { null! } : Array.Empty<IShell>();
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
}
