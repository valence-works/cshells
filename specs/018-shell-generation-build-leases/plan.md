# Implementation Plan: Shell Generation Build Leases

**Branch**: `018-shell-generation-build-leases` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/018-shell-generation-build-leases/spec.md`

## Summary

Add optional root-owned build participants and generation leases. Reserve immutable generation identity before composition, acquire participants after composition/name validation but before catalog access, notify leases with the exact selected detailed catalog snapshot before feature construction, and transfer a single internal lease-set owner through provider build into the candidate shell. Release only after confirmed provider teardown; conservatively retain unresolved lease owners in the root-lifetime registry without retaining shells or providers. Preserve primary build/initializer/activation errors when cleanup faults.

## Technical Context

**Language/Version**: C# 14; source packages multi-target `net8.0;net9.0;net10.0`
**Primary Dependencies**: Existing `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`, and `System.Collections.Immutable`; no new package dependencies
**Storage**: N/A
**Testing**: xUnit 2.x; lifecycle integration and unit tests in `tests/CShells.Tests/` (test project targets net10.0)
**Target Platform**: .NET 8, .NET 9, and .NET 10
**Project Type**: .NET abstractions package and hosting/runtime library
**Performance Goals**: Preserve per-name concurrency; distinct names can build in parallel; participant callbacks are sequential per generation and run without global/catalog locks
**Constraints**: Exact detailed snapshot instance; root-only participants; reverse, exactly-once lease release; no release before successful lifecycle and provider teardown; root retention must not hold Shell/provider/snapshot; no package-deletion or assembly-unload claims
**Scale/Scope**: One optional public contract, a small internal idempotent owner, targeted changes to registry/provider builder/shell teardown, integration tests and package docs; no recovery API or retries

## Constitution Check

- **I. Abstraction-first architecture**: PASS. Public context/participant/lease contracts belong in `CShells.Abstractions`; implementation remains in `CShells`.
- **III. Modern C#**: PASS. Use nullable annotations, file-scoped namespaces, explicit public XML docs, current language conventions, and existing target frameworks.
- **IV. Explicit errors**: PASS. Null lease results fail clearly; primary build failures remain primary; normal teardown reports deterministic release failures; cleanup logging is guarded where it must not replace a primary error.
- **V. Test coverage**: PASS. Add deterministic integration tests for acquisition ordering, all cleanup paths, overlapping generations, blocked disposal, GC-root independence, and no-participant behavior.
- **VI. Simplicity**: PASS. One internal lease-set owner and one private synchronized retained-owner collection; no public state machine, retry loop, timeout, or recovery API.
- **VII. Lifecycle and concurrency**: PASS. Existing per-name semaphore serializes each generation; no locks surround async participant callbacks; leases release only after provider teardown.

Repository policy requires tests for new functionality but does not declare test-first cadence. Implement tests and production changes in the dependency order below; no test-first requirement is inferred.

## Phase 0: Research

See [research.md](research.md). `ShellRegistry.CreateGenerationAsync` is the sole production caller of the internal provider builder; it currently creates the descriptor after composition and uses an unchecked narrowing cast. The builder already selects one detailed catalog snapshot before any feature construction. `Shell.DisposeCoreAsync` signals `Disposed` before provider disposal, and initializer cleanup currently hides provider-disposal outcome. The design preserves these lifecycle semantics while adding the approved ownership boundary.

## Phase 1: Design & Contracts

- [data-model.md](data-model.md) describes immutable build identity, participant/lease roles, lease-set handoff, and root retention.
- [contracts/IShellGenerationBuildParticipant.md](contracts/IShellGenerationBuildParticipant.md) defines callback, ownership, teardown, failure and concurrency semantics.
- [quickstart.md](quickstart.md) maps deterministic proof cases to focused commands and the required mutation/revert check.
- Run the Spec Kit agent-context update script. The current `AGENTS.md` has no managed `SPECKIT START/END` block; if the script adds an active-feature pointer, retain only the generated section relevant to this feature and review its diff.

## Project Structure

```text
src/CShells.Abstractions/Lifecycle/ShellGenerationBuildContext.cs
src/CShells.Abstractions/Lifecycle/IShellGenerationBuildParticipant.cs
src/CShells.Abstractions/Lifecycle/IShellGenerationBuildLease.cs
src/CShells/Lifecycle/ShellGenerationBuildLeaseSet.cs
src/CShells/Lifecycle/ShellRegistry.cs
src/CShells/Lifecycle/ShellProviderBuilder.cs
src/CShells/Lifecycle/Shell.cs
src/CShells/DependencyInjection/ServiceCollectionExtensions.cs
src/CShells/Hosting/DefaultShellServiceExclusionProvider.cs
tests/CShells.Tests/Integration/Lifecycle/ShellGenerationBuildLeaseTests.cs
tests/CShells.Tests/Integration/Lifecycle/ShellRegistryActivateTests.cs
tests/CShells.Tests/Integration/Lifecycle/ShellRegistryInitializerTests.cs
tests/CShells.Tests/Integration/Lifecycle/ShellRegistryTerminatorTests.cs
src/CShells.Abstractions/README.md
src/CShells/README.md
specs/018-shell-generation-build-leases/
```

**Structure Decision**: Put public lifecycle contracts beside existing lifecycle abstractions. Keep all lease ownership and root retention internal to the runtime. Route every registry/provider/shell transfer through one internal owner; reuse integration lifecycle host patterns rather than introducing a separate test framework.

## Complexity Tracking

No constitution violations or new dependencies are required. The private retained-owner collection is required for the approved root-lifetime strong-reference guarantee when teardown cannot be confirmed.
