# Implementation Plan: Optional Nuplane Feature Discovery and Deferred Catalog Freshness

**Branch**: `codex/156-nuplane-integration` | **Feature directory**: `021-nuplane-adapter` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification for CShells issue #156, on actual main `94f4b16eda0b12dc3d5e565cf0ecc4bc99b4e1ab`.

## Summary

Add an optional `CShells.Nuplane` package that explicitly supplies Nuplane's loaded package assemblies to CShells feature discovery and coordinates refresh-before-build and host-selected reload behavior. One root-owned coordinator serves the Nuplane observer and CShells build-participant aliases. The adapter references only CShells and Nuplane public abstractions, shares a private nonparticipant holder into shell providers, and defers registry resolution to observer work to avoid eager registry construction cycles. Per-shell reload errors are passed unchanged to a generic options callback so hosts retain their own refusal translation and logging policies.

The source implementation targets `net8.0;net9.0;net10.0`, uses Nuplane public abstraction packages `0.0.11-preview.99` as interim pins, adds a tenth project to the explicit pack list, and documents the integration in the package README. Final qualification remains an outside-checkout consumer against the published adapter and actual Nuplane package feeds.

## Technical Context

**Language/Version**: C# 14 / .NET 8, 9, and 10 target frameworks; `LangVersion=latest`
**Primary Dependencies**: `CShells`, `CShells.Abstractions`, `Nuplane.Abstractions` and `Nuplane.Loading.Abstractions` at `0.0.11-preview.99`, `Microsoft.Extensions.Options` and DI abstractions
**Storage**: N/A; coordinator state is process-local and managed
**Testing**: Existing xUnit project `tests/CShells.Tests`, focused integration tests under `Integration/Nuplane`
**Target Platform**: .NET hosts on `net8.0`, `net9.0`, and `net10.0`
**Project Type**: Optional .NET library package
**Performance Goals**: Catalog scans occur only for configured eligible completions, pending refresh work, or a requested build with outstanding freshness. No timer or background retry scan is added.
**Constraints**: Use only loaded `Assembly` instances from `PackageAssemblies.Assemblies`; never load `AssemblyReferences`. Do not reference Nuplane runtime packages or Elsa. Do not add unload, readability, refusal, restart, locked-package, or store mutation behavior. Keep the observer after Nuplane autoload registration. Preserve nine current packages and add the tenth package.
**Scale/Scope**: One provider, one observer/build-participant coordinator, three public option controls plus a generic result callback, three library TFMs, four host-policy combinations in tests/docs.

## Constitution Check

| Principle | Status | Design evidence |
|---|---|---|
| I. Abstraction-First Architecture | PASS | The adapter implements existing public contracts and adds no new public interface. Its downstream project references do not add Nuplane or Elsa edges to CShells core. |
| II. Feature Modularity | PASS | The provider is selected explicitly with `WithAssemblyProvider<TProvider>()`; current provider selection remains available unless the host opts in. |
| III. Modern C# Style | PASS | Nullable enabled, file-scoped namespaces, collection expressions, primary constructors where appropriate, and all three supported TFMs. |
| IV. Explicit Error Handling | PASS | Catalog refresh/cancellation failures retain pending epochs and propagate. Reload result errors remain raw and visible to the configured callback; no generic failure is silently cleared. |
| V. Test Coverage | PASS | Required behavior is covered with deterministic xUnit DI and registry-pipeline tests, plus an actual published-package consumer gate. |
| VI. Simplicity & Minimalism | JUSTIFIED | The private holder is required because participant descriptors are excluded from shell providers, while a directly shared participant would also be excluded. One coordinator is needed to share epochs between observer and build callback. The generic options callback preserves host policy without an Elsa dependency. |
| VII. Lifecycle & Concurrency Contracts | PASS | `SemaphoreSlim` serializes async refresh and reload paths; monotonic epochs preserve changes arriving during refresh; reload begins after the refresh gate is released; callback and cancellation failures leave work pending. |

The constitution's technology snapshot still describes eight packages; the current canonical workflow explicitly packs nine. This work uses the live explicit list as the package-family baseline and adds one entry. It does not change constitution governance as part of the adapter leaf.

## Design

### Public API

Add `CShells.Nuplane` with the following public API in namespace `CShells.Nuplane`:

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

public sealed class NuplaneFeatureAssemblyProvider(IPackageAssemblyCatalog packageAssemblyCatalog)
    : IFeatureAssemblyProvider
{
    public Task<IEnumerable<Assembly>> GetAssembliesAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}

public static class CShellsNuplaneBuilderExtensions
{
    public static CShellsBuilder WithNuplaneFeatureDiscovery(
        this CShellsBuilder builder,
        Action<NuplaneIntegrationOptions>? configure = null);
}
```

`WithNuplaneFeatureDiscovery` registers its options and adapter services, calls `builder.WithAssemblyProvider<NuplaneFeatureAssemblyProvider>()`, and selects only `CoordinatorHolder` for sharing. A host may compose other providers with ordinary CShells builder calls. The extension is intended to be called after `AutoloadPackages()` and other required Nuplane observer registrations.

`Enabled` gates observer-driven work; it does not gate feature provider queries or imply generation-readability protection. Hosts can add dependency-aware option configuration with the standard `services.AddOptions<NuplaneIntegrationOptions>().Configure<TDependency>(...)` API; this is how a host supplies its result callback without capturing a not-yet-built builder or service provider.

### Registration and ownership

The composition extension appends an `INuplaneObserver` factory descriptor to the service collection after existing registrations. It does not call `OnPackagesChanged<Coordinator>` because that registers a second implementation-type singleton. The factory resolves `CoordinatorHolder.Value`; the holder contains the one root coordinator. A separate root `IShellGenerationBuildParticipant` factory resolves that same coordinator. CShells excludes both participant descriptors from shell providers, but copies the observer factory and the explicitly shared holder. Two shell providers therefore resolve the same root coordinator through the holder while unrelated observer descriptors preserve their normal per-provider identity and disposal.

The coordinator's registry dependency is `Func<IShellRegistry>`, registered from the root provider and invoked only after registry construction in observer work. It never constructor-injects `IShellRegistry`, captures a shell provider, uses `HostContainer`, or uses a static service locator. The coordinator and holder own only managed state and are non-disposable.

### Refresh and reload state

The observer accepts an eligible completion only when `appliedPackages.Count > 0` or `changeSet.Removed.Count > 0`. Nuplane `.99` emits completion for successful application, including unchanged change sets, and for committed removals even with an empty applied list; idle empty cycles and failed-only cycles do not emit this callback.

For each eligible completion, the coordinator decides whether refresh is requested before checking for active shells. `EveryEligibleCompletion` requests one refresh epoch for each delivered callback. `ChangedOrPending` requests freshness for added, updated, or removed packages and retries an outstanding freshness epoch; an unchanged completion with no outstanding freshness does not scan. A package change increments the requested epoch before active-shell lookup. If no shell is active, the callback returns without scanning, activating, or requesting reload; the freshness epoch remains outstanding for the next requested build. That build already composes from the refreshed catalog, so the deferred event does not create a separate reload request.

`BeginAsync` checks outstanding refresh epochs before the runtime feature catalog's lazy initialization or read. A short synchronous state lock protects request/commit epoch reads and writes and is never held across an await. The coordinator takes the refresh semaphore, captures the current request epoch under the state lock, calls `IRuntimeFeatureCatalog.RefreshAsync`, and advances the catalog epoch only to the captured value after success. A later event during refresh increments the request epoch under the state lock and remains pending. Failure or cancellation leaves it pending. The returned build lease is a no-op; it does not hold the refresh gate, assemblies, or snapshot protection for the generation lifetime.

The automatic-reload epoch is independent of catalog freshness. Create a new reload epoch only when `AutoReload` is enabled, at least one shell is active, and the callback requests catalog refresh work. A later eligible callback with a fresh catalog may retry an already-pending reload epoch without incrementing it. Under `ChangedOrPending`, an unchanged callback with a fresh catalog and no reload work does neither refresh nor reload even when `AutoReload` is enabled; under `EveryEligibleCompletion`, each eligible callback requests a fresh scan and a new reload epoch. If no shell is active, the event records catalog freshness only; a later build creates the new shell from the refreshed catalog without an extra reload. Any reload epoch that was already pending remains pending while no shell is active, but that no-active callback neither increments nor attempts it. For active shells, after successful refresh the coordinator releases `refreshGate` before `IShellRegistry.ReloadActiveAsync`, since reload enters `BeginAsync` again. It serializes the automatic reload operation and its result callback under `reloadGate`, snapshots returned results into a stable read-only list, and invokes `OnReloadResults` only after `refreshGate` is released. It independently inspects every `ReloadResult.Error`; it does not trust callback side effects to decide success. Only a nonempty result set with no per-shell errors and a successful callback acknowledges the captured reload epoch. Partial errors, thrown/cancelled registry calls, and callback failures retain reload work. A callback failure can follow already-committed successful shell reloads; no rollback is claimed. The callback can request a manual shell build or registry reload without deadlocking on `refreshGate`, because that gate is released before reload and callback; participant work does not acquire `reloadGate`. The callback contract is host reporting, not reentrant Nuplane observer dispatch.

### Reload failure adaptation

CShells `ReloadActiveAsync` returns per-shell failures in `ReloadResult.Error`; they do not arrive through `IShellLifecycleSubscriber`. The generic `OnReloadResults` callback receives the original immutable result view, including partial success and nested exception chains. Foundation-owned adapters can apply their existing refusal recognition, placeholder substitution, and warning/error policy there. The Nuplane package does not reference `IEfModuleRefusal`, format Elsa messages, replace exception instances, or swallow errors. Callback exceptions propagate to Nuplane's existing observer-isolation boundary while the coordinator keeps its reload epoch pending.

### Package and test project structure

```text
src/CShells.Nuplane/
├── CShells.Nuplane.csproj
├── NuplaneFeatureAssemblyProvider.cs
├── NuplaneIntegrationOptions.cs
├── NuplaneRefreshTrigger.cs
├── CShellsNuplaneBuilderExtensions.cs
├── Internal/
│   ├── NuplaneRefreshCoordinator.cs
│   ├── CoordinatorHolder.cs
│   └── NoOpBuildLease.cs
└── README.md

tests/CShells.Tests/Integration/Nuplane/
├── NuplaneFeatureAssemblyProviderTests.cs
├── NuplaneCompositionTests.cs
├── NuplaneRefreshCoordinatorTests.cs
├── NuplaneReloadResultsTests.cs
└── NuplaneTestHost.cs
```

The existing test project references the optional project for adapter-specific integration tests; the `CShells` package itself remains free of Nuplane references. Add the adapter project to `CShells.sln` and the explicit package list in `.github/workflows/publish.yml`. Add central NuGet versions only for `Nuplane.Abstractions` and `Nuplane.Loading.Abstractions`, both `.99`; add root `NuGet.Config` source mapping for `Nuplane*` to `https://f.feedz.io/valence-works/nuplane/nuget/index.json` and all other package IDs to nuget.org. The repository-wide source props also contribute `JetBrains.Annotations`, as they do for other source packages. Do not change Foundation pins or lock files.

## Validation and release qualification

Run focused adapter tests and the existing test project first, followed by builds of the adapter for `net8.0`, `net9.0`, and `net10.0`. Run solution build/test only when the focused gates are green and machine load permits; respect the build-slot wrapper. Pack and inspect the adapter nuspec for its two Nuplane abstraction dependencies and absence of Elsa or Nuplane runtime packages. Review all ten package IDs from the workflow list.

Deterministic source tests use fake catalogs and observer dispatch to prove provider selection, adapter DI identity/order, refresh epochs, shell promotion, removals, profile behavior, callback handling, and retry state. They do not prove Nuplane's real `AutoloadPackages` registration, reconciliation dispatch, or package loading. Before acceptance, root owns a separate private/published-package consumer gate using actual Nuplane `.99` runtime/loading and the published CShells adapter; it must compile the documented quickstart shape and exercise package loading, discovery, feature construction on promotion, and final-removal-to-zero without project references or private reflection. Reuse the root-owned producer/autoload baseline artifact under `cshells-nuplane-consumer-preparation` when available. The interim `.99` references are raised to final Nuplane `0.0.11` by the release owner before CShells `0.0.30`; this leaf does not publish packages.

## Complexity Tracking

| Addition | Why needed | Simpler alternative rejected because |
|---|---|---|
| Shared private `CoordinatorHolder` | It is the only safe shell-shared bridge to the root observer coordinator. | Sharing the coordinator itself is rejected because its participant role causes descriptor exclusion; sharing all `INuplaneObserver` aliases changes every observer's lifecycle. |
| Separate refresh and reload epochs/gates | Refresh must finish before reload, while reload re-enters the participant and can fail independently. | One boolean or one lock would clear unrelated work, lose newer events, or deadlock reload's `BeginAsync` callback. |
| Generic `OnReloadResults` option callback | Host-specific operators need the raw per-shell failures for their own refusal translation and warning/error policy. | An Elsa dependency is forbidden, and CShells lifecycle notifications do not carry `ReloadResult.Error`. |
