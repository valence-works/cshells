# Implementation Plan: Share Host Singletons with Shells

**Branch**: `016-share-host-singletons` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/016-share-host-singletons/spec.md`

## Summary

Add opt-in service-type sharing to `CShellsBuilder`. At each shell-provider build, validate the selected service type against the final root registration set, resolve every matching unkeyed singleton registration from the root provider once as an ordered enumerable, and copy those resolved instances into the shell provider as instance descriptors. This preserves default root ownership and prevents shell provider disposal from taking ownership of host-created disposable instances.

## Technical Context

**Language/Version**: C# 14, source projects multi-target `net8.0;net9.0;net10.0`
**Primary Dependencies**: Microsoft.Extensions.DependencyInjection 10.0 abstractions/runtime
**Storage**: N/A
**Testing**: xUnit 2.x, `tests/CShells.Tests/` on net10.0
**Target Platform**: .NET 8, 9, and 10
**Project Type**: .NET library
**Performance Goals**: One root enumerable resolution per selected service type per shell-provider build
**Constraints**: Leave keyed descriptors unchanged; preserve descriptor order and ordinary later feature-registration precedence; only share closed service types with an all-singleton unkeyed registration set; fail on root-only exclusions.
**Scale/Scope**: One additive builder API, lifecycle copy logic, focused integration coverage, and package documentation.

## Constitution Check

- **I. Abstraction-first / shell isolation**: PASS. No new extensibility interface is needed; the existing public `CShellsBuilder` API configures CShells itself. Shells keep isolated containers while selected singleton instances are explicitly host-owned.
- **III. Modern C# style**: PASS. Use current nullable/C# conventions and XML documentation for public overloads.
- **IV. Explicit error handling**: PASS. Validate missing, mixed-lifetime, open-generic, and excluded selections with a message that explains the supported registration set.
- **V. Test coverage**: PASS. Add integration tests for identity, ordering, owner disposal, invalid selections, keyed descriptors, overrides, and overlapping generations.
- **VI. Simplicity**: PASS. Store only selected service types; do not introduce an ownership wrapper or new service abstraction.
- **VII. Lifecycle and concurrency**: PASS. Root singletons survive overlapping shell generations; shell disposal does not dispose shared instances.

## Phase 0: Research

See [research.md](research.md). The key design choice is to replace selected root descriptors in each shell with `ImplementationInstance` descriptors after resolving them from the root, rather than registering shell factories that return root-owned objects.

## Phase 1: Design & Contracts

- [data-model.md](data-model.md) defines the selection and ownership invariants.
- [contracts/CShellsBuilder.md](contracts/CShellsBuilder.md) specifies the public API and validation behavior.
- [quickstart.md](quickstart.md) provides executable behavior scenarios for implementation and review.
- No additional agent-context technology entry is warranted; this feature changes no project-level technology or setup convention.

## Project Structure

```text
src/CShells/DependencyInjection/CShellsBuilder.cs
src/CShells/DependencyInjection/ServiceCollectionExtensions.cs
src/CShells/Lifecycle/ShellProviderBuilder.cs
src/CShells/Hosting/DefaultShellServiceExclusionProvider.cs
tests/CShells.Tests/Integration/DependencyInjection/ShareSingletonWithShellsTests.cs
src/CShells/README.md
specs/016-share-host-singletons/
```

**Structure Decision**: Extend the existing builder and shell-provider construction path. Add focused integration tests that build real shell providers; no new package, project, or runtime abstraction is required.

## Complexity Tracking

No constitution violations or additional architecture are required.
