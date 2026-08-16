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
   and middleware pipeline, even after generation N+1 is active; its scope remains held
   until response completion.
5. Old endpoint generations can be removed during drain without removing the replacement,
   and drain completion waits for an in-flight old-generation request to release its
   `OnCompleted` scope.
6. Candidate publication remains provisional until the prior generation begins normal
   deactivation. If a later lifecycle subscriber rejects the candidate, the previous endpoint
   snapshot and middleware pipeline remain live and the candidate is removed atomically.
7. Middleware composition failure rejects activation instead of publishing a degraded pipeline,
   and all HTTP method comparisons use ordinal, case-insensitive semantics.
8. A generation's middleware pipeline is available before its endpoint snapshot becomes visible;
   rejected endpoint publication removes the staged candidate pipeline.

## Non-goals

- Replacing ASP.NET Core routing or adding a parallel endpoint framework.
- Changing static host endpoint registration semantics.
- Moving lifecycle policy or authorization rules into route-path middleware.
