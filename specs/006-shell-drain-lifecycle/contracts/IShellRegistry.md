# Contract: IShellRegistry

**Namespace**: `CShells.Lifecycle`
**Project**: `CShells.Abstractions`

## Purpose

`IShellRegistry` is the authoritative API for registering **shell blueprints**, activating
and reloading shells to produce monotonic **generations**, and draining superseded or
explicitly-drained shells. Host code never authors generation numbers — the registry stamps
them in the order reloads are serialized.

## Interface Definition

```csharp
namespace CShells.Lifecycle;

/// <summary>
/// The authoritative registry for named, generation-stamped shells.
/// </summary>
/// <remarks>
/// One <see cref="IShellBlueprint"/> is registered per shell name. Each call to
/// <see cref="ActivateAsync"/> or <see cref="ReloadAsync"/> re-invokes the blueprint to
/// produce a fresh <see cref="ShellSettings"/>, builds a shell stamped with the next
/// monotonic generation number, runs its <see cref="IShellInitializer"/> services, promotes
/// it to <see cref="ShellLifecycleState.Active"/>, and initiates cooperative drain on the
/// previously-active generation (if any).
/// Multiple generations for the same name may coexist: exactly one is
/// <see cref="ShellLifecycleState.Active"/>, and any number may be in
/// <see cref="ShellLifecycleState.Deactivating"/>,
/// <see cref="ShellLifecycleState.Draining"/>, or <see cref="ShellLifecycleState.Drained"/>.
/// </remarks>
public interface IShellRegistry
{
    /// <summary>
    /// Registers a blueprint for a shell name. Subsequent
    /// <see cref="ActivateAsync"/> / <see cref="ReloadAsync"/> calls invoke this blueprint
    /// to compose fresh settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a blueprint for <c>blueprint.Name</c> is already registered (FR-003).
    /// </exception>
    void RegisterBlueprint(IShellBlueprint blueprint);

    /// <summary>
    /// Returns the blueprint registered for <paramref name="name"/>, or <c>null</c> if none.
    /// </summary>
    IShellBlueprint? GetBlueprint(string name);

    /// <summary>
    /// Returns every registered blueprint name.
    /// </summary>
    IReadOnlyCollection<string> GetBlueprintNames();

    /// <summary>
    /// Composes fresh settings from the registered blueprint, builds generation 1, runs its
    /// <see cref="IShellInitializer"/> services, and promotes it to
    /// <see cref="ShellLifecycleState.Active"/> (FR-009).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no blueprint is registered for <paramref name="name"/>, or when a shell
    /// for <paramref name="name"/> is already active (callers should use
    /// <see cref="ReloadAsync"/> to roll over).
    /// </exception>
    /// <remarks>
    /// Propagates any exception thrown during blueprint composition, provider construction,
    /// or initializer invocation. The partial provider (if built) is disposed; no partial
    /// shell entry is retained (FR-014).
    /// </remarks>
    Task<IShell> ActivateAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Composes fresh settings from the registered blueprint, builds the next generation,
    /// runs its initializers, promotes it to <see cref="ShellLifecycleState.Active"/>, and
    /// initiates cooperative drain on the previously-active generation — all in a single
    /// call (FR-010).
    /// </summary>
    /// <remarks>
    /// If no generation is currently active, behaves equivalently to
    /// <see cref="ActivateAsync"/>: generation 1 is produced and no prior generation is
    /// drained (FR-011).
    ///
    /// Concurrent calls for the same <paramref name="name"/> are serialized. Generation
    /// numbers are assigned in arrival order; the last call to complete becomes the active
    /// generation (FR-013).
    ///
    /// If blueprint composition, provider build, or any initializer throws, the exception
    /// propagates to the caller, the current active generation (if any) is unaffected, the
    /// partial provider is disposed, and no partial generation is retained (FR-014).
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no blueprint is registered for <paramref name="name"/>.
    /// </exception>
    Task<ReloadResult> ReloadAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads every registered blueprint. Independent names reload in parallel; per-name
    /// outcomes are returned so callers can distinguish successes from composition failures
    /// without aborting the batch (FR-012).
    /// </summary>
    Task<IReadOnlyList<ReloadResult>> ReloadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a cooperative drain on <paramref name="shell"/>. The drain runs three
    /// phases: (1) wait for all active <see cref="IShellScope"/> handles to release,
    /// (2) invoke all registered <see cref="IDrainHandler"/> services in parallel,
    /// (3) grace after deadline or force. Concurrent calls for the same shell return the
    /// same in-flight operation (FR-028).
    /// </summary>
    Task<IDrainOperation> DrainAsync(IShell shell, CancellationToken cancellationToken = default);

    /// <summary>
    /// The single <see cref="ShellLifecycleState.Active"/> shell for <paramref name="name"/>,
    /// or <c>null</c> if no active shell exists (FR-031).
    /// </summary>
    IShell? GetActive(string name);

    /// <summary>
    /// The generations currently retained for <paramref name="name"/>: the active generation plus any
    /// earlier generations still draining. A generation is released once it reaches
    /// <see cref="ShellLifecycleState.Disposed"/>, so fully torn-down generations are NOT returned —
    /// callers cannot rely on historical generations remaining available after teardown. Always contains
    /// the shell returned by <c>GetActive</c>; empty when the name has no retained generations (FR-032).
    /// </summary>
    IReadOnlyCollection<IShell> GetAll(string name);

    /// <summary>
    /// Registers a lifecycle subscriber notified of every state transition on every shell.
    /// </summary>
    void Subscribe(IShellLifecycleSubscriber subscriber);

    /// <summary>
    /// Removes a previously registered lifecycle subscriber.
    /// </summary>
    void Unsubscribe(IShellLifecycleSubscriber subscriber);
}
```

## Behaviour Contract

### RegisterBlueprint

- Adds the blueprint to the registry keyed case-insensitively by `blueprint.Name`.
- Throws `InvalidOperationException` if a blueprint for that name is already registered
  (FR-003). Duplicate registration is a programming error.
- Does not trigger activation; the built-in startup hosted service calls `ActivateAsync`
  for every registered blueprint at host start (FR-035). Hosts calling
  `RegisterBlueprint` after startup must call `ActivateAsync` themselves.

### ActivateAsync

- Acquires the per-name serialization semaphore.
- Invokes `blueprint.ComposeAsync(ct)` to obtain a fresh `ShellSettings`; validates that
  `settings.Id.Name` matches the blueprint name.
- Builds a new `IServiceCollection`, applies root-service copying + feature
  `ConfigureServices`, and constructs the shell's `IServiceProvider`.
- Increments the name's generation counter (starts at 1) and stamps the `ShellDescriptor`,
  copying the blueprint's `Metadata` onto the descriptor.
- Registers the shell as `Initializing`.
- Resolves `IEnumerable<IShellInitializer>` from the provider and awaits each
  sequentially in DI-registration order.
- Promotes the shell to `Active` and releases the semaphore.
- Fires `null → Initializing` and `Initializing → Active` transitions on subscribers.

### ReloadAsync

- Identical to `ActivateAsync` for the first generation when no prior active shell exists
  (FR-011).
- Otherwise: after promoting the new generation to `Active`, transitions the previous
  generation to `Deactivating` under the same semaphore, then — after releasing the
  semaphore — kicks off `DrainAsync(previous)` in the background.
- Returns a `ReloadResult { Name, NewShell, Drain, Error }`. `Drain` is non-null exactly
  when there was a previous active generation to drain. `Error` is non-null when blueprint
  composition, provider build, or any initializer threw; in that case `NewShell` is null
  and the current active generation is unchanged.

### ReloadAllAsync

- Enumerates every registered blueprint name, snapshots it, and runs `ReloadAsync` per
  name via `Task.WhenAll`.
- Aggregates per-name `ReloadResult` values; one failing name does not abort the others
  (FR-012).
- Blueprints for different names reload in parallel; a single name's reloads remain
  serialized per `ReloadAsync`'s contract.

### DrainAsync

- Idempotent via `Interlocked.CompareExchange` on a per-shell `DrainOperation?` slot
  (FR-028).
- Transitions the shell from `Deactivating` or `Active` to `Draining`.
- Runs drain phases 1–3 as described in `IDrainOperation.md`.
- Transitions to `Drained` when phases complete, then disposes the shell's service
  provider; fires the `Drained → Disposed` transition.
