# Implementation Plan: Observe Settled Active Generations

**Branch**: `022-settled-readiness` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/022-settled-readiness/spec.md`

## Summary

Expose the existing activation-settlement authority through an optional synchronous `ISettledShellRegistry.GetSettledActive(string name)` capability in `CShells.Abstractions/Lifecycle`. The built-in `ShellRegistry` implements it directly. Discover it by casting the existing `IShellRegistry`; introduce no additional DI registration, service alias, holder, cache, or state machine.

## Technical Context

**Language/Version**: C# 14; source targets net8.0, net9.0, net10.0  
**Primary Dependencies**: Existing CShells abstractions and runtime; no new dependency  
**Storage**: Existing in-memory name slots and committed marker only  
**Testing**: xUnit 2.x, net10.0; external public-package consumer on net8/net9/net10  
**Target Platform**: All currently supported .NET runtimes  
**Project Type**: Library  
**Performance Goals**: Bounded in-memory read with no semaphore wait, provider access, or callbacks  
**Constraints**: Preserve raw active routing visibility and runner terminal semantics; unsupported custom-registry policy stays host-owned  
**Scale/Scope**: One optional interface, one built-in method, focused lifecycle tests, package documentation

## Constitution Check

- I: Public consumer capability lives in Abstractions before runtime implementation. No concrete dependency or feature-container registration is added.
- II: Observation belongs to the existing registry lifecycle; no unrelated feature or application policy is added.
- III/IV: Existing targets/style, `Guard.Against.NullOrWhiteSpace`, case-insensitive name slot, and public XML documentation.
- V: Deterministic gates exercise initial commit, completion, reload composition/publication, rollback, drain, and custom-registry compatibility; all affected suites required.
- VI: Reuse the existing committed marker and slot; optional capability is justified by reproduced readiness failures and introduces no compatibility shim.
- VII: Read volatile/atomic published state without mutation or async locks; recheck active identity and lifecycle eligibility. No guarantee extends beyond the observation.

Pre-research and post-design checks pass. No exception is required. The existing constitution's package-count statement predates the adapter; this task does not change the ten-package publication list.

## Project Structure

```text
specs/022-settled-readiness/
  spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
  checklists/requirements.md
  contracts/ISettledShellRegistry.md
src/CShells.Abstractions/Lifecycle/ISettledShellRegistry.cs
src/CShells/Lifecycle/ShellRegistry.cs
src/CShells/README.md
src/CShells.Abstractions/README.md
tests/CShells.Tests/Integration/Lifecycle/ShellRegistrySettledObservationTests.cs
```

**Structure Decision**: Keep production implementation on the built-in registry and reuse focused test fixtures when they clarify cleanup. No new package or registration API.

## Design and Test Cadence

Read the current slot and candidate. Reject absent or uncommitted candidates. Recheck that the candidate is the current active identity and still lifecycle Active before returning; check the slot mapping too if necessary to cover unregister/recreate. Use the existing `IsPublishedActiveGeneration` eligibility helper where appropriate. Do not take the per-name activation semaphore or call `GetOrActivateAsync`.

Tests are required by the CShells constitution. It does not mandate TDD; author contract/regression scenarios with implementation and use a causal marker-bypass mutation afterward. Synchronous observations during a blocked participant callback prove independence without timing-only assertions. Every gate and owned activation/reload/drain operation must be released/joined in finally/idiomatic async disposal.

## Verification

Run the focused observation and existing settlement/runner/lifecycle suites first, then the full CShells.Tests suite and EndToEnd suite once on the final candidate. Build source for net8/net9/net10. Root independently reviews the exact head and mutates the committed-marker guard in an isolated copy: the pending-commit regression must fail, then pass restored. Hosted required CI and main publication gates remain required. After public preview publication, qualify an outside-checkout PackageReference-only consumer on all three runtimes and verify archive/loaded-assembly provenance. Actual Foundation host acceptance remains in its adoption task.
