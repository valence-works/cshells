# CShells.Nuplane Public Contract

## Dependencies

The adapter package references `CShells`, `CShells.Abstractions`, `Nuplane.Abstractions`, and `Nuplane.Loading.Abstractions`. Nuplane dependencies use central interim version `0.0.11-preview.99`. The package does not reference Nuplane runtime/loading implementations or Elsa.

## Feature assembly provider

```csharp
namespace CShells.Nuplane;

public sealed class NuplaneFeatureAssemblyProvider(IPackageAssemblyCatalog packageAssemblyCatalog)
    : IFeatureAssemblyProvider
{
    public Task<IEnumerable<Assembly>> GetAssembliesAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}
```

The provider calls `GetPackagedAssembliesAsync(cancellationToken)` and returns each `PackageAssemblies.Assemblies` in returned package order. It does not return or load `AssemblyReferences`, does not activate packages, and propagates cancellation. Nuplane's public catalog supplies an empty result when no loaded assembly catalog is available.

## Refresh and reload options

```csharp
public enum NuplaneRefreshTrigger
{
    EveryEligibleCompletion,
    ChangedOrPending
}

public sealed class NuplaneIntegrationOptions
{
    public bool Enabled { get; set; } = true;
    public NuplaneRefreshTrigger RefreshTrigger { get; set; } = NuplaneRefreshTrigger.ChangedOrPending;
    public bool AutoReload { get; set; }
    public Func<IReadOnlyList<ReloadResult>, CancellationToken, ValueTask>? OnReloadResults { get; set; }
}
```

`OnReloadResults` receives a copied read-only collection after `ReloadActiveAsync` and after the catalog refresh gate is released. Each `ReloadResult` and its `Error` reference, including nested exceptions and partial outcomes, is passed unchanged. This callback is host-owned reporting. The coordinator independently determines whether every shell reload succeeded. A callback failure leaves reload work pending, but does not undo a shell that already switched generations.

## Composition

```csharp
namespace CShells.Nuplane;

public static class CShellsNuplaneBuilderExtensions
{
    public static CShellsBuilder WithNuplaneFeatureDiscovery(
        this CShellsBuilder builder,
        Action<NuplaneIntegrationOptions>? configure = null);
}
```

Call after Nuplane's `AutoloadPackages()` and any existing observer registrations, before building the root service provider. The method registers the optional adapter coordinator aliases and explicit provider, then returns the same builder. Do not also call `NuplaneBuilder.OnPackagesChanged<T>()` for the coordinator; that API registers an implementation-type singleton.

Hosts can configure the callback through ordinary options DI, including dependency-aware configuration:

```csharp
services.AddOptions<NuplaneIntegrationOptions>()
    .Configure<ILoggerFactory>((options, loggerFactory) =>
    {
        var logger = loggerFactory.CreateLogger("ShellReloadResults");
        options.OnReloadResults = (results, _) =>
        {
            foreach (var result in results.Where(result => result.Error is not null))
                logger.LogError(result.Error, "Reloading shell {Shell} failed.", result.Name);
            return ValueTask.CompletedTask;
        };
    });
```

The adapter does not format host-specific errors. A host callback may recognize its own exception/refusal contracts and substitute host placeholders while keeping non-refusal failures at its chosen error severity.

## Registration and ordering guarantees

- The adapter appends its own `INuplaneObserver` factory after the loader observer. Existing observer descriptors remain in place and are not replaced.
- Root observer and root `IShellGenerationBuildParticipant` resolve one coordinator instance.
- Only the private, nonparticipant `CoordinatorHolder` is selected by `ShareSingletonWithShells<T>()`.
- The participant concrete and interface descriptors stay root-only. Shell observer aliases resolve the root coordinator through the shared holder.
- Registry resolution is lazy inside observer work through a root `Func<IShellRegistry>`; the coordinator constructor does not resolve the registry.
- The no-op build lease provides no assembly-readability or generation-lifetime protection.
