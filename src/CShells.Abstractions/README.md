# CShells.Abstractions

Core abstractions and interfaces for building shell features without dependencies on the full CShells framework.

## Purpose

This package contains the fundamental interfaces and models needed to build CShells features. By referencing only this package in your feature libraries, you avoid pulling in the entire CShells runtime and its dependencies.

## When to Use

- Building feature libraries that will be consumed by CShells applications
- Creating reusable features without coupling to the full framework
- Keeping feature library dependencies minimal

## Key Types

- `IShellFeature` - Base interface for defining features that register services
- `ShellSettings` - Configuration model for shell settings
- `IShellInitializer` - Startup hook resolved from the shell provider before a shell becomes active
- `ISettledShellRegistry` - Optional nonactivating observation of the current generation after activation settlement
- `IShellGenerationBuildParticipant` and `IShellGenerationBuildLease` - Optional host-owned protection tied to one shell build and its selected feature-catalog snapshot
- `ShellActivationAttempt` and `ShellActivationAttemptState` - Transient attempt and safe target-snapshot diagnostics, including returned and verified generation values
- `LifecyclePhase`, `LifecycleOrderAttribute`, and `AddShellInitializer<T>()` - First-class initializer ordering APIs
- `IDrainHandler` - Cooperative drain hook invoked in parallel while an old shell generation shuts down
- Core abstractions for extensibility

## Installation

```bash
dotnet add package CShells.Abstractions
```

## Example Usage

```csharp
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

[ShellFeature("MyFeature")]
public class MyFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMyService, MyService>();
        services.AddShellInitializer<MyFeatureInitializer>(
            LifecyclePhase.Prepare,
            order: 100);
    }
}
```

`AddShellInitializer<T>()` registers the initializer as transient (unless you have already registered it yourself, in which case your lifetime is preserved) and attaches deterministic phase/order metadata. Existing direct `IShellInitializer` registrations remain valid and run in `LifecyclePhase.Default` using DI registration order.

Hosts that protect runtime feature assemblies or related resources can register an `IShellGenerationBuildParticipant` in the root container. CShells reserves the generation identity and immutable blueprint metadata before composition, then calls participants in root registration order after composition/name validation and before feature-catalog initialization or access. Each returned lease receives the exact detailed `RuntimeFeatureCatalogSnapshot` selected for that build before feature construction begins. Successful callback fan-out is sequential; when a callback fails, later callbacks are skipped and all acquired leases are unwound. A participant that throws before returning a lease is responsible for cleaning up its own partial acquisition.

Leases belong to the attempted generation. For a published shell, CShells releases them in reverse order only after the Disposed notification and complete shell-provider teardown succeed. Failed builds and unpublished initializer candidates unwind their leases after partial provider cleanup. If lifecycle/provider cleanup fails, or an individual lease cannot release, CShells keeps unresolved leases alive for the root registry lifetime without retaining the shell or provider. Callbacks for different shell names may run concurrently. A callback must not reenter activation, reload, or unregister for the same shell name. This API coordinates protection lifetime; it does not itself unload assemblies or delete package files.

`ISettledShellRegistry` is a separate optional lifecycle capability. The built-in registry implements it on the same object as `IShellRegistry`; cast the existing registry and distinguish an unsupported implementation from a supported query returning null. `GetSettledActive(name)` is synchronous and nonactivating. It returns only the current generation after activation settlement, returns null during a provisional replacement rather than substituting a historical generation, and does not grant a use lease or future-lifetime guarantee. Completion callback failures remain diagnostic-only, and existing routing visibility is unchanged.

`IRuntimeFeatureCatalogCommitSource` is an optional capability for observing committed catalog snapshots. Resolve the configured `IRuntimeFeatureCatalog`, then capability-test that instance; custom implementations do not need to add this interface. Subscribe before initialization to observe the initial commit. Subscriptions do not replay past commits, so when subscribing to an already active catalog, subscribe first and then read `CurrentSnapshot` to reconcile. A refresh can commit during that read, so compare generations to avoid missing or processing a generation twice.

```csharp
if (catalog is IRuntimeFeatureCatalogCommitSource commits)
{
    commits.SnapshotCommitted += snapshot => queueReconciliation(snapshot);
}
```

The event carries the exact detailed `RuntimeFeatureCatalogSnapshot`, including its generation. Notifications run synchronously outside the refresh lock, in commit order, and subscriber exceptions are isolated. Keep handlers quick and enqueue expensive work elsewhere. Concurrent refreshes can advance `CurrentSnapshot` beyond the generation currently being delivered.

## Learn More

- [Main Documentation](https://github.com/sfmskywalker/cshells)
- [CShells Package](../CShells) - Core runtime implementation
