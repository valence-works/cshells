# Validation: Apply Live Nuplane Integration Options

This guide defines the proof to run on the final implementation head. No implementation or acceptance test is claimed by this planning artifact.

## Deterministic owner tests

Run the focused Nuplane adapter suites through the repository's shared `dotnet` wrapper:

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~NuplaneRefreshCoordinatorTests|FullyQualifiedName~NuplaneCompositionTests|FullyQualifiedName~NuplaneReloadResultsTests'
```

The focused cases must prove:

- Standard binding/configuration changes are observed by the next eligible callback without rebuilding the host provider.
- A refresh barrier captures all four policy values before awaits. Mutating the held source object and replacing `OnReloadResults` while that operation is blocked does not alter it; the next delivery sees the new values and callback.
- `Enabled = false` admits no new observer epochs but a build consumes older pending freshness without consulting live options.
- `AutoReload = false` retains failed/deferred reload work while refreshing eligible freshness. A later enabled delivery retries without a redundant catalog scan when that snapshot is already fresh.
- Failed-only, empty, and already-canceled deliveries make no monitor read, epoch mutation, registry access, or catalog refresh.
- An undefined refresh trigger produces an actionable error before epoch mutation and registry access; options-monitor and options-validation exceptions propagate before those side effects. Follow each rejected policy/read with an unchanged valid delivery or build and prove that no work was recorded.
- Existing fluent configuration, dependency-aware configuration, refresh triggers, result callbacks, alias identity, ordering, build reentry, and lifecycle behavior remain covered.

Use `TaskCompletionSource` gates and bounded waits. Release gates and join every owned operation in cleanup; no sleeps are evidence of ordering.

## Owner suite and target builds

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release --no-restore
dotnet build src/CShells.Nuplane/CShells.Nuplane.csproj -c Release
```

Confirm the adapter build covers `net8.0`, `net9.0`, and `net10.0`, with no new warnings or errors. Capture the exact head and command results in the issue or validation record.

## Causal mutation

In an isolated copy of the final tree, restore the prior construction-time `IOptions<T>.Value` capture (or equivalent immutable startup-only behavior). Build and execute the dynamic options regression. It must fail because the second eligible delivery does not apply changed settings. Restore the production source byte-for-byte and execute the regression successfully. A compile failure does not count as the negative result.

## Public-package acceptance

After normal owner-repository merge and package publication, audit all ten actual public Feedz archives for exact source commit/version, family dependencies, archive identity, and `net8.0`/`net9.0`/`net10.0` assets. Then run a fresh-cache, outside-checkout PackageReference-only consumer with `CShells` and `CShells.Abstractions` plus the optional adapter as required by the public composition. Run on actual .NET 8, 9, and 10 runtimes; verify archive hashes, NuGet source metadata, loaded assembly identities, and the dynamic-options cases. Local builds, private packs, or a probe that only reproduces the old snapshot gap do not satisfy this acceptance.

## Acceptance boundary

Record each exact source/package identity and results. This feature does not complete stable release, Foundation pin/lock updates, Foundation Host/Workbench adoption, `#2164` readability acceptance, or `#2362` unloading/pruning work.
