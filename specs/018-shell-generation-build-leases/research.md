# Research: Shell Generation Build Leases

## Decision: Reserve identity before composition and validate before acquisition

- **Decision**: Under the existing per-name semaphore, reserve the next descriptor generation and snapshot blueprint metadata into an immutable `ShellDescriptor` before calling `ComposeAsync`. Pair it with `new ShellId(blueprint.Name)`. Preserve the composed-name validation and begin participants only after it succeeds. Keep a case-insensitive name-to-counter high-water mark for the registry lifetime, independently of removable shell slots, so unregister/recreate cannot reuse a generation. Reserve atomically, including when a removed slot overlaps a replacement, and reject any generation greater than `int.MaxValue` before narrowing.
- **Rationale**: This gives each attempted build one framework-owned identity even if composition fails, prevents mutable blueprint metadata from changing the context, and avoids descriptor wrap/reuse.
- **Alternatives considered**: Assigning the descriptor after composition (lets callbacks miss the attempt identity and observe mutated metadata); deriving identity from composed settings (lets the blueprint select another shell identity); unchecked cast (wraps the public descriptor generation).

## Decision: Make participation optional and root-owned

- **Decision**: Add the public contracts under `CShells.Abstractions` in `CShells.Lifecycle`. `ShellRegistry` resolves participants once from root DI in registration order; the default shell-service exclusion provider excludes them from child providers. With no participants the set is empty and normal behavior remains unchanged.
- **Rationale**: The contract is a host integration seam and must not be recreated per shell. Existing `IShellGenerationActivationParticipant` registration/exclusion patterns provide the narrowest implementation precedent.
- **Alternatives considered**: Registering through shell DI (duplicates participants and gives them the wrong lifetime); adding a feature or package-specific CShells dependency (couples core runtime to downstream policy).

## Decision: Pass the exact selected detailed snapshot before feature construction

- **Decision**: After the builder initializes and reads the detailed catalog snapshot, invoke acquired leases sequentially with that exact snapshot, before selection dependency resolution and feature `ConfigureServices`. Keep the callback outside catalog and global locks. Thread an explicit immutable `ShellGenerationBuildContext` and one internal lease-set owner through `ShellProviderBuilder.BuildAsync` and its `BuildResult`.
- **Rationale**: `RuntimeFeatureCatalogSnapshot` carries the assemblies omitted by the projection interface, and it is the exact object used by feature selection. The current builder has one production caller in `ShellRegistry` and no direct test callers, so a required explicit context avoids fabricated identities.
- **Alternatives considered**: A second projected-only contract (loses assembly identity); callbacks after feature construction (too late for acquisition protection); a context-less builder overload (can misidentify attempts).

## Decision: Use one small idempotent lease-set owner

- **Decision**: The registry's lease-set object owns leases during acquisition and pre-provider build. The set passes through the builder result and transfers to `Shell` before initializer resolution. It releases in reverse acquisition order, attempts all leases, and retains only lease objects whose release fails. One shared in-flight release operation and terminal state prevent duplicate attempts. After completion, the retained owner drops the task and its original exception graph; concurrent callers receive the original release failure, while later calls report a lightweight prior-failure error without retrying.
- **Rationale**: A single owner object makes handoff and exactly-once release observable without a public lifecycle state machine. It supports reverse unwind for partial acquisition and normal teardown.
- **Alternatives considered**: A list copied between layers (can double-release or orphan resources); a public lease state machine (unnecessary API complexity); disposing leases directly from lifecycle subscribers (notification occurs before provider teardown and is fallible).

## Decision: Release only after confirmed provider and lifecycle teardown

- **Decision**: On normal disposal, wait for the `Disposed` callback and provider disposal to complete before releasing leases. If notification or provider disposal fails, retain the unresolved owner in a private synchronized collection on root-lifetime `ShellRegistry`. For unpublished initializer failure, return the partial-provider disposal outcome explicitly; release after success or retain on failure, without adding lifecycle transitions or disposing the provider twice.
- **Rationale**: `Shell.DisposeCoreAsync` currently advances state to `Disposed` before disposing the provider. The state notification therefore precedes actual completion. The shell registry removes generations from slot history at the successful `Disposed` callback, so unresolved protection needs an independent root-owned reference.
- **Alternatives considered**: Releasing on the `Disposed` notification (too early); retaining the Shell or provider (prevents normal garbage collection); silently swallowing teardown outcome (cannot distinguish safe release from uncertain teardown).

## Decision: Preserve primary failures and isolate cleanup logging

- **Decision**: Begin/build/initializer/activation errors remain primary when cleanup or its logging also fails. On successful teardown, aggregate lease-release failures deterministically after trying all leases. Log cleanup faults through a tiny safe logging helper where logging must not mask the primary error. Use `CancellationToken.None` for cleanup.
- **Rationale**: Teardown is best-effort on failure, but diagnostics must not replace the event that caused cleanup. A cancelled request token must not suppress release attempts.
- **Alternatives considered**: Replacing the primary failure with an aggregate cleanup failure (hides the actual build failure); abandoning remaining lease releases after the first fault; passing the cancelled build token into cleanup.

## Source findings at implementation baseline

- `ShellRegistry.CreateGenerationAsync` is called under a per-name semaphore, assigns a `long` generation, composes and validates settings, builds the provider, then creates the descriptor. It is the only production caller of `ShellProviderBuilder.BuildAsync`.
- `ShellProviderBuilder.BuildAsync` initializes the runtime feature catalog and captures its detailed `CurrentSnapshot` before dependency selection and feature construction. `BuildResult` currently carries only provider, holder and enabled features.
- `Shell` transitions to `Disposed` and awaits lifecycle subscribers before asynchronously disposing its provider. Registry cleanup removes the shell from slot history inside the `Disposed` callback.
- Initializer failure uses `DisposePartialProviderAsync`, which currently swallows disposal failures; this path must report the outcome without changing the primary initializer exception or adding state transitions.
- `Shell` is directly constructed by lifecycle unit tests. Keep a small optional internal constructor argument/delegating overload so those tests do not need synthetic lease owners.
- Root-only DI participant patterns are `IShellGenerationActivationParticipant`, `ShellRegistry` constructor enumeration, and `DefaultShellServiceExclusionProvider`.

## Approved scope boundaries

The task's reviewed contract resolves the root retention seam to a private synchronized collection on `ShellRegistry`; an internal count may be added only if needed to make tests deterministic. No retry, timeout, public recovery API, Nuplane/Elsa policy, package-store deletion rule, or assembly-unload guarantee belongs here. Same-name lifecycle reentry from a participant callback is unsupported and must be documented; different names remain concurrently buildable.
