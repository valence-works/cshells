# Research: Optional Nuplane Feature Discovery and Deferred Catalog Freshness

## R-001: Use the public loaded-assembly catalog

**Decision**: Implement `NuplaneFeatureAssemblyProvider` over `IPackageAssemblyCatalog.GetPackagedAssembliesAsync(cancellationToken)` and flatten only each `PackageAssemblies.Assemblies` collection in catalog order.

**Rationale**: The published Nuplane `.99` loading abstraction describes this query as the current active loaded package set and returns empty when loading is unavailable. `AssemblyReferences` represent durable files and include shared copies that must not be loaded in place of the host's copy. The adapter must not acquire package contexts or call a loader.

**Alternatives considered**: `IPackageAssemblyProvider` was rejected because the catalog applies active-package and load-state filtering. `AssemblyReferences` were rejected because they are file references, not already-loaded runtime assemblies. Reflection over Nuplane internals was rejected because the published public catalog is sufficient.

## R-002: Preserve CShells provider composition

**Decision**: Add a `CShellsBuilder.WithNuplaneFeatureDiscovery(...)` extension that explicitly selects `NuplaneFeatureAssemblyProvider` by calling the existing `WithAssemblyProvider<TProvider>()` path.

**Rationale**: CShells treats every configured provider as explicit composition. Installing the package must not alter host scanning or any existing provider. Hosts that want multiple sources call their normal provider extensions deliberately.

**Alternatives considered**: Registering an implicit provider from the Nuplane package reference was rejected because optional integration must not change a host's existing assembly source. Adding a provider-combination abstraction was rejected because `CShellsBuilder` already appends provider registrations.

## R-003: Append one observer alias after package autoload

**Decision**: The composition extension appends an `INuplaneObserver` factory after the host calls `AutoloadPackages()` and after its existing observer registrations. The factory resolves a private root `CoordinatorHolder`; it does not call `NuplaneBuilder.OnPackagesChanged<TCoordinator>()`.

**Rationale**: Nuplane dispatcher order is service registration order. `AutoloadPackages()` registers its loader observer first. `OnPackagesChanged<T>()` registers an implementation-type singleton, which is a separate coordinator state owner if a concrete singleton is also registered. The existing `.99` public-DI alias probe validates a single root coordinator, shared private holder, participant exclusion, and lazy registry resolution in either service-resolution order. The probe does not prove adapter behavior.

**Alternatives considered**: Reusing `OnPackagesChanged<TCoordinator>()` was rejected because it can create a second singleton. Sharing `INuplaneObserver` wholesale was rejected because it changes identity and disposal for unrelated observers. Constructor-injecting `IShellRegistry` was rejected because `ShellRegistry` eagerly resolves participants and would form a cycle.

## R-004: Use the build participant only for deferred catalog freshness

**Decision**: Run outstanding refresh epochs in `IShellGenerationBuildParticipant.BeginAsync`, which CShells calls before catalog initialization/read and feature construction. Return a no-op lease after refresh.

**Rationale**: A package event may happen before any shell is active. Keeping its epoch until a real build lets the provider query the current catalog before that build selects features. The participant interface also allows a no-op lease, but this adapter has no package-use lease contract.

**Alternatives considered**: Eagerly activating or reloading a shell to refresh the catalog was rejected because it changes host lifecycle. Holding a generation lease was rejected because no public Nuplane use-lease exists and Foundation #2164/#2362 retain readability/unload ownership. Adding a timer or retrying quiet cycles was rejected because Nuplane `.99` emits no callback for idle or failed-only cycles.

## R-005: Separate freshness requests from automatic reload requests

**Decision**: Track monotonic catalog-request/completion epochs and independent reload-request/completion epochs. Serialize async catalog work with a refresh `SemaphoreSlim`, release it before `ReloadActiveAsync`, and serialize auto reload separately.

**Rationale**: A newer package event during a refresh must remain pending. A failed reload must be retried without falsely marking the catalog stale. `ReloadActiveAsync` re-enters the build participant, so it must not run while the refresh semaphore is held.

**Alternatives considered**: One dirty boolean was rejected because success on an older captured refresh could clear a newer event. A single lock held over refresh and reload was rejected because registry reload re-enters the same participant. Timer-driven convergence and in-flight candidate invalidation are outside the accepted contract.

## R-006: Preserve host-owned reload failure handling with a generic callback

**Decision**: Add `NuplaneIntegrationOptions.OnReloadResults` with signature `Func<IReadOnlyList<ReloadResult>, CancellationToken, ValueTask>?`. Pass a copied read-only result list after the refresh gate is released. The coordinator independently inspects every result's `Error` and only acknowledges a reload epoch after the result set and callback both succeed.

**Rationale**: CShells exposes per-shell failures through `ReloadResult.Error`; `IShellLifecycleSubscriber` only receives state transitions. Existing Foundation Host/Workbench observers map nested `IEfModuleRefusal` values to operator-facing warning/error output and substitute the host placeholder. The callback lets those host-owned adapters keep that policy without adding Elsa references to CShells.Nuplane. Raw names, exceptions, inner-exception chains, and partial successes are passed unchanged.

**Alternatives considered**: Swallowing returned errors was rejected because CShells returns them instead of throwing. Translating refusal errors in the adapter was rejected because that introduces Elsa-specific policy and formats messages at the wrong owner. Adding a public refusal abstraction or new abstractions package was rejected as larger than a generic delegate callback. A result callback is reporting only; it does not override whether `ReloadActiveAsync` succeeded.

## R-007: Use central package management and Feedz source mapping

**Decision**: Add `.99` central versions for `Nuplane.Abstractions` and `Nuplane.Loading.Abstractions`; configure the public Nuplane Feedz source with a `Nuplane*` package mapping and keep other packages on nuget.org. Add the adapter to the solution and the one canonical explicit pack list in `.github/workflows/publish.yml`.

**Rationale**: `.99` is the publicly qualified interim Nuplane version accepted by issue #156. The adapter's release gate requires actual package provenance and a later outside-checkout consumer. The live pack list contains nine projects, while the constitution's technology note still says eight; the workflow is the current source for its explicit package count.

**Alternatives considered**: Changing Foundation `.94` package pins/locks was rejected because adoption belongs to a separate release task. Relying only on the current machine's package cache was rejected because CI and the outside consumer need a deterministic public source. Source-building the adapter was rejected as final compatibility evidence because the adapter itself must be published and consumed.
