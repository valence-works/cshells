# Contract: Shell Generation Build Lease

Namespace: `CShells.Lifecycle`, assembly/package `CShells.Abstractions`.

```csharp
public sealed record ShellGenerationBuildContext(
    ShellDescriptor Descriptor,
    ShellId ShellId);

public interface IShellGenerationBuildParticipant
{
    ValueTask<IShellGenerationBuildLease> BeginAsync(
        ShellGenerationBuildContext context,
        CancellationToken cancellationToken = default);
}

public interface IShellGenerationBuildLease : IAsyncDisposable
{
    ValueTask OnSnapshotSelectedAsync(
        RuntimeFeatureCatalogSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
```

## Ordering and ownership

1. CShells reserves the immutable descriptor/context from blueprint identity before composition.
2. CShells composes and validates `ShellSettings.Id.Name` against the blueprint name.
3. CShells calls root-registered participants sequentially in registration order. No catalog initialization/read or feature construction has started.
4. After selecting the exact detailed `RuntimeFeatureCatalogSnapshot`, CShells calls every acquired lease's snapshot callback sequentially in acquisition order, before constructing/configuring any feature.
5. The internal lease-set owner releases in reverse acquisition order. It transfers from registry to build result to Shell before initializer resolution.
6. A lease's `DisposeAsync` is called only after confirmed provider teardown. Cleanup uses a non-cancelled token boundary (the `IAsyncDisposable` method has no token).

## Failure semantics

- `BeginAsync` must clean any partial work if it throws before returning a lease. A null lease result is rejected.
- If a later begin fails, previously acquired leases unwind in reverse order.
- If snapshot callback, feature construction, provider build, initializer, activation, lifecycle notification, provider disposal, or lease release fails, CShells follows the teardown rules in `spec.md` and retains unresolved lease objects in the root registry.
- A primary build/initializer/activation failure is preserved if cleanup fails. On otherwise successful teardown, all release failures are aggregated deterministically after every lease was attempted.
- If `Disposed` notification or provider teardown is unconfirmed, no lease release runs; all unresolved leases remain rooted.

## Concurrency constraints

Participants are root-owned and may be called concurrently for different shell names. CShells serializes a given name using its existing name semaphore; participant callbacks must not reenter activation, reload or unregister for that same name. CShells holds no catalog/global lock while invoking callbacks.

See [the reviewed lifetime contract](../research.md) and [the runnable proof guide](../quickstart.md).
