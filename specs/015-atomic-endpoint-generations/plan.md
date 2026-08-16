# Implementation Plan: Atomic Shell Endpoint Generations

**Branch**: `codex/1345-atomic-endpoint-generations`

## Design

The dynamic endpoint data source is the candidate-publication seam. It owns route
normalization, method-overlap checks, conflict diagnostics, immutable snapshot replacement,
and generation-specific removal. `ShellEndpointRegistrationHandler` maps features into an
unpublished candidate, composes the generation middleware pipeline, asks the data source to
publish the candidate, and only then commits the pipeline entry. A failed map, pipeline
composition, or validation leaves the prior snapshot and pipeline intact.

Route ownership is represented by typed endpoint metadata. Shell endpoints identify the
dynamic shell, generation, and owning feature; host endpoints retain standard metadata and
use their display name as a deterministic diagnostic owner when no typed owner is present.

`ShellMiddleware` first checks matched endpoint metadata for an exact generation and uses
`IShellRegistry.GetAll` to bind that request to the matching shell. Unmatched/cold requests
retain the existing lazy activation behavior.

## Constitution check

- Abstraction-first: no new public framework abstraction is required; route metadata remains
  in the ASP.NET Core implementation package and existing lifecycle interfaces are reused.
- Lifecycle/concurrency: publication uses an async-safe lifecycle boundary already serialized
  by the registry; the endpoint snapshot is immutable and atomically replaced under the data
  source's short synchronization gate.
- Explicit errors: conflicts carry both owners, route, and method set.
- Test coverage: focused routing, handler, and middleware tests cover each acceptance criterion.

## Files

- `src/CShells.AspNetCore/Routing/DynamicShellEndpointDataSource.cs`
- `src/CShells.AspNetCore/Routing/ShellEndpointMetadata.cs`
- `src/CShells.AspNetCore/Routing/ShellEndpointRouteBuilder.cs`
- `src/CShells.AspNetCore/Notifications/ShellEndpointRegistrationHandler.cs`
- `src/CShells.AspNetCore/Middleware/ShellMiddleware.cs`
- focused integration tests under `tests/CShells.Tests/Integration/AspNetCore/`
