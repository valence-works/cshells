# Research: Settled Shell Activation Results

## Current behavior

- `ShellRegistry.GetOrActivateAsync` reads `NameSlot.Active` before acquiring the per-name semaphore and returns the value immediately. It repeats that return for any non-null `Active` after entering the semaphore.
- `CreateGenerationAsync` performs initialization and participant `PrepareAsync`, transitions the candidate to Active, publishes it in `slot.Active` and `slot.All`, then calls participant `Commit`.
- Commit failure rolls participants back in reverse order, restores the previous active generation, disposes the candidate, and throws `ShellGenerationActivationException`.
- `Complete` callbacks run after commit; their exceptions are logged and swallowed. A final published-active check rejects a candidate that was removed before the successful return boundary.
- `GetActive` and `GetAll` are lock-free reads. Their early candidate visibility is relied on by routing and lifecycle participants.
- The name semaphore already serializes activation/reload. Waiting callers do not own or cancel the operation they wait behind.

Source reviewed: `src/CShells/Lifecycle/ShellRegistry.cs` (`GetOrActivateAsync`, `CreateGenerationAsync`, `IsPublishedActiveGeneration`) and `src/CShells/Lifecycle/Shell.cs`.

## Decision

Store one internal committed flag on the concrete `Shell`, published with `Volatile.Write` and observed with `Volatile.Read`. This avoids changing `IShell` or custom registry contracts. The only setter call is after participant completion and the final published-active guard; put it before informational success logging.

The `GetOrActivateAsync` lock-free path returns only a committed shell. An uncommitted published candidate falls through to the existing per-name semaphore. Its in-lock recheck returns a committed active generation, continues ordinary activation if no active generation remains, and fails clearly if the invariant is violated and an uncommitted shell is still active after semaphore acquisition.

## Alternatives rejected

- Changing `GetActive`/`GetAll` to hide the candidate would break exact-generation routing and participant behavior during commit.
- Adding a registry event, queue, public status contract, or startup runner would duplicate coordination already provided by `NameSlot.Semaphore` and exceed this leaf's scope.
- Waiting with a separate task per shell would create a second synchronization contract and complicate cancellation without improving on the existing semaphore.

## Test strategy

Block `Commit` with deterministic task gates after publication. Assert the activation-request task is incomplete while the callback is blocked, then release and assert the settled generation. Repeat with a fail-once participant for rollback/recovery. Use a blocked blueprint for the pre-publication reload case. Always release gates and join activation/waiter tasks in `finally`; cap waits to avoid hung tests. Mutate the fast path back to unconditional return and prove the pending-caller test fails.
