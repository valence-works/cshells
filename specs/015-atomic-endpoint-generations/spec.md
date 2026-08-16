# Atomic Shell Endpoint Generations

## Problem

Dynamic shell endpoints are currently removed and re-added during reload. A mapping
failure can therefore leave a shell without its previous routes, and a successful
replacement can expose an empty routing snapshot. The current collision check only
compares the first HTTP method and raw route text, so equivalent parameter templates,
multi-method routes, same-batch duplicates, and host/shell conflicts are not rejected.
Requests matched before a reload must continue through the exact shell generation that
owns the endpoint metadata.

## Scope

- Build a complete shell generation endpoint candidate before publication.
- Validate candidate routes against the existing host and shell route inventory.
- Publish one immutable replacement snapshot per shell generation.
- Preserve the previous snapshot if mapping, middleware composition, validation, or a later
  activation subscriber fails.
- Resolve matched requests by shell identifier and exact generation metadata.
- Keep standard ASP.NET Core endpoint data sources and framework coexistence intact.
- Exercise repeated replacement/removal/unload behavior with a real feature assembly loaded
  into a collectible `AssemblyLoadContext`; the test must prove that published and retired
  endpoint delegates do not retain that context.

## Functional requirements

1. Conflicts are deterministic and identify both endpoint owners. Conflict detection is
   conservative for equivalent route templates and overlapping HTTP method sets. Dynamic owner
   diagnostics include shell identifier, generation, and owning feature as structured data.
2. Candidate validation occurs before the published endpoint snapshot changes.
3. New requests see either the previous complete snapshot or the new complete snapshot;
   no intermediate empty state is published.
4. A request with `ShellEndpointMetadata.Generation = N` uses generation N's shell scope
   and middleware pipeline, even when routing matched it before generation N+1 became active.
   Routing acquires that exact-generation lease before the old generation can drain, and releases
   it at response completion even if downstream middleware short-circuits.
5. Old endpoint generations can be removed during drain without removing the replacement,
   and drain completion waits for an in-flight old-generation request to release its
   `OnCompleted` scope.
6. Feature mapping, middleware composition, and route validation prepare an externally invisible
   candidate. Lifecycle subscribers must accept it before the registry makes the candidate exactly
   addressable and activation participants commit. A commit failure rolls participants back in
   reverse order and atomically restores the prior registry, route, and pipeline state.
7. Middleware composition failure rejects activation instead of publishing a degraded pipeline,
   and all HTTP method comparisons use ordinal, case-insensitive semantics.
8. A generation's middleware pipeline is available before its endpoint snapshot becomes visible;
   rejected endpoint publication removes the staged candidate pipeline.
9. Overlapping preparation is allowed, but only one rollback-capable commit may be pending in the
   route inventory. A second commit and additive route/host inventory mutations are rejected until
   the owner completes or rolls back the transaction. Removals for other shells apply immediately;
   generation-specific cleanup for the pending shell is deferred and replayed idempotently during
   completion or folded into the rollback snapshot before its single publication notification.
   Rollback cannot transiently republish a disposed route, resurrect an intermediate generation,
   conflict with an addition, or prevent unrelated lifecycle cleanup.
10. Endpoint change-token acquisition is race-safe with publication and does not expose a disposed
    token source or miss the snapshot change it is meant to observe.
11. Cold-activation manual re-matching acquires the exact generation lease before exposing the
    rematched endpoint, so a concurrent reload cannot dispose it in the handoff to middleware.
12. Activation succeeds only while the candidate remains the registry's retained, exact Active
    generation. Rejection restores a prior generation only when it is still Active and retained;
    a candidate or prior generation drained during participant fan-out is never returned or
    resurrected as Active, and a subsequent activation can recover a live serving generation.

## Non-goals

- Replacing ASP.NET Core routing or adding a parallel endpoint framework.
- Changing static host endpoint registration semantics.
- Moving lifecycle policy or authorization rules into route-path middleware.
