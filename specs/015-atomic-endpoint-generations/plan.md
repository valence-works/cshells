# Implementation Plan: Atomic Shell Endpoint Generations

**Branch**: `codex/1345-atomic-endpoint-generations`

## Design

The dynamic endpoint data source is the candidate-publication seam. It owns route normalization,
method-overlap checks, conflict diagnostics, immutable snapshot replacement, generation-specific
removal, and an identity-bound prepare/commit/rollback transaction. Preparation is externally
invisible and may overlap. Commit revalidates against the latest route inventory under one lock,
swaps the complete snapshot, and retains only the retired snapshot needed for rollback. Because
route collisions are global, the data source permits one rollback-capable commit at a time and
rejects competing commits plus additive route or host-inventory mutations until its owner completes
or rolls it back. Removals are monotonic: other-shell cleanup applies immediately, while cleanup for
the pending shell generation is deduplicated and replayed before completion or after rollback
restoration. Rollback builds the final snapshot with those removals already applied before one
publication notification, so routing never observes a restored disposed route. This keeps rollback
infallible without allowing one slow activation to retain another shell's disposed routes or
collectible feature code.
Change-token sources are atomically exchanged and cancelled without racing eager disposal.

`IShellGenerationActivationParticipant` gives `ShellRegistry` a two-phase activation boundary.
`ShellEndpointRegistrationHandler` prepares feature endpoints and the middleware pipeline before
the Active notification. After subscribers accept the candidate, the registry first inserts the
generation into `Active`/`All`, then commits participants in registration order. The handler stages
the pipeline before committing endpoints, so the first route-change notification can resolve both
the exact shell and its pipeline. A later commit failure rolls participants back in reverse order,
restores the prior registry slot, and disposes the rejected provider. Only after every commit
succeeds do participants discard rollback evidence. The registry verifies that the candidate is
still Active and retained before accepting it, and restores the prior generation only when that
generation is also still Active and retained. If cleanup wins after rollback evidence is discarded,
the registry leaves the slot empty, drains the un-restorable prior generation, and lets the next
activation rebuild a coherent route/pipeline generation.

Route ownership is represented by typed endpoint metadata and carried into the structured conflict
result. Shell endpoints identify the dynamic shell, generation, and owning feature; host endpoints
retain standard metadata and use their display name as a deterministic diagnostic owner when no
typed owner is present.

`ShellEndpointGenerationMatcherPolicy` runs after method and constraint policies and acquires at
most one exact-generation `IShellScope` while matching still owns the endpoint generation. The
request-local lease is idempotent across policy re-entry and is released through `OnCompleted`.
`ShellMiddleware` reuses that lease. When cold activation requires its manual endpoint rematch, it
uses the same lease seam before calling `SetEndpoint`; a failed handoff exposes no shell endpoint
and returns 503. Unmatched requests retain the existing lazy activation behavior.

## Constitution check

- Abstraction-first: the small lifecycle participant contract lives in the framework-neutral core;
  ASP.NET Core route metadata and matching policy remain in the implementation package.
- Lifecycle/concurrency: publication uses an async-safe lifecycle boundary already serialized
  by the registry; the endpoint snapshot is immutable and atomically replaced under the data
  source's short synchronization gate.
- Explicit errors: conflicts carry both owners, route, and method set.
- Test coverage: focused routing, handler, and middleware tests cover each acceptance criterion;
  `CShells.DynamicFeatureFixture` supplies a real dynamically loaded endpoint feature for a
  five-cycle collectible `AssemblyLoadContext` test through the production route-builder and data
  source seams, proving published and retired endpoint delegates release dynamically loaded feature
  code. The middleware suite also drives a real registry reload after routing leases an old matched
  request and verifies drain waits for that response's `OnCompleted` callback. Real-registry reload
  tests prove invisible subscriber fan-out, reverse participant rollback, and registry/route/pipeline
  restoration after a post-publication commit rejection. Same-shell drain interleavings prove that
  neither a disposed prior generation nor a disposed candidate can become or remain Active, that
  rollback observers see no transient disposed route, and that a later request reactivates and serves.

## Files

- `src/CShells.AspNetCore/Routing/DynamicShellEndpointDataSource.cs`
- `src/CShells.AspNetCore/Routing/ShellEndpointGenerationMatcherPolicy.cs`
- `src/CShells.Abstractions/Lifecycle/IShellGenerationActivationParticipant.cs`
- `src/CShells/Lifecycle/ShellRegistry.cs`
- `src/CShells.AspNetCore/Routing/ShellEndpointMetadata.cs`
- `src/CShells.AspNetCore/Routing/ShellEndpointRouteBuilder.cs`
- `src/CShells.AspNetCore/Notifications/ShellEndpointRegistrationHandler.cs`
- `src/CShells.AspNetCore/Middleware/ShellMiddleware.cs`
- focused integration tests under `tests/CShells.Tests/Integration/AspNetCore/`
