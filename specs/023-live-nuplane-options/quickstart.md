# Validation: Apply Live Nuplane Integration Options

This guide defines the source and publication gates for Task #161. The source checks below passed; actual public-package acceptance remains pending.

## Deterministic owner tests

Run the focused Nuplane adapter suites through the repository's shared `dotnet` wrapper:

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~NuplaneRefreshCoordinatorTests|FullyQualifiedName~NuplaneCompositionTests|FullyQualifiedName~NuplaneLiveOptionsTests|FullyQualifiedName~NuplaneReloadResultsTests'
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

## Source qualification record (2026-10-08)

Product/test head: `8cb10fba9e58c68040da993a9e634fad02f4a372`, based on `353f9e37db0607d9fb1540e139bce02aecf14e16`. Subsequent validation-record changes contain documentation only. Root and independent source review found no material blocker after correcting disabled/pending counter assertions, cleanup ownership, and real configuration reload coverage.

| Gate | Exact command/result |
| --- | --- |
| Focused adapter tests | `dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~CShells.Tests.Integration.Nuplane' --logger trx` — 34 passed, zero failed/skipped. |
| Full library suite | `dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release --no-build --no-restore --logger trx` — 795 passed, zero failed/skipped. |
| Adapter Release targets | `dotnet build src/CShells.Nuplane/CShells.Nuplane.csproj -c Release --no-restore` — net8.0, net9.0, net10.0 passed, zero warnings/errors. |
| Compiled causal mutation | Isolated exact-head worktree; replace monitor field with construction-time `CurrentValue` capture and use it in policy capture. Run `dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release --filter FullyQualifiedName~ConfigurationReload_UpdatesRegisteredObserverPolicy_OnLaterDelivery --logger trx` — one executed, one failed at `Assert.NotSame` because the shell was not reloaded. Exit 1; no compilation or timeout failure. |
| Byte-identical restoration | Restore coordinator SHA-256 `6cfb3cb0cc725994b508cb9838f75bb849f05156eba574b9623475c351106092`; repeat the mutation command with `--no-restore` — one executed/passed, zero failed/skipped, exit 0. |

TRX, build logs, both mutation sources, command arrays, hashes, and root/independent review records are retained with the program delivery artifacts. Hosted owner-repository CI, normal merge/main publication, all ten public archives, and the outside-checkout three-runtime consumer remain required before T010 and issue closure. Copilot was not requested; Greptile is optional under the maintainer's D15 decision.
