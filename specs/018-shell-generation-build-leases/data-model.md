# Data Model: Shell Generation Build Leases

## ShellGenerationBuildContext

Immutable input for one attempted generation.

- `Descriptor`: the unique blueprint-name/generation/timestamp/metadata identity reserved before composition.
- `ShellId`: the shell identity created from the same blueprint name as the descriptor.
- **Invariant**: Descriptor generation is in `1..int.MaxValue`; descriptor metadata is an immutable copy taken before composition.

## IShellGenerationBuildParticipant

Root-owned callback that starts an optional generation-specific protection attempt.

- Participants are enumerated once from root DI in service-registration order.
- `BeginAsync` is reached after composition and shell-name validation, but before catalog initialization or read.
- A participant that has no work returns a no-op lease.
- If begin throws before returning, that participant is responsible for cleaning up its own partial acquisition.

## IShellGenerationBuildLease

Participant-owned resource tied to one attempted shell generation.

- `OnSnapshotSelectedAsync` receives the exact detailed snapshot used by feature selection before feature construction.
- `DisposeAsync` releases external protection only when the framework confirms successful teardown.
- A failed release must leave participant-owned protection intact where possible; CShells retains the lease object but cannot reverse participant side effects.

## ShellGenerationBuildLeaseSet

Internal, idempotent owner of acquired lease objects and their descriptor. Ownership moves once across the build boundary: registry acquisition, builder result, then shell before initializer resolution.

- Cleanup attempts leases in reverse acquisition order exactly once.
- Successful leases are removed; unresolved leases remain on the owner.
- The owner does not retain a `Shell`, service provider, or catalog snapshot.

## Retained lease-set entry

Strong reference to an unresolved lease-set owner held in a synchronized private collection on root-lifetime `ShellRegistry`.

- It survives removal of a shell from slot history and does not point back to the shell/provider.
- Whole-set retention applies when lifecycle notification or provider disposal fails before release begins.
- Partial-set retention applies after normal teardown when one or more lease releases fail.
- Entries last for the root registry lifetime; this feature adds no retry or public removal operation.
