# Validation: Apply Live Nuplane Integration Options

This guide defines the source and publication gates for Task #161. The source checks below passed; actual public-package qualification is recorded below.

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

TRX, build logs, both mutation sources, command arrays, hashes, and root/independent review records are retained with the program delivery artifacts. The subsequent publication record below supplies hosted CI, normal main publication, all ten public archives, and the outside-checkout three-runtime consumer. Copilot was not requested; Greptile is optional under the maintainer's D15 decision.

### Hosted CI ordering correction

PR #162's first CI run (`37843878579`) built successfully but failed one existing lifecycle test (794/795 library tests; 31/31 end-to-end). `Drain_SameInstance_AcrossConcurrentDrainAsyncCalls` issued synchronous calls without holding the drain open; the first operation could finish and release its pointer before later calls. The corrected test uses the existing blocking drain feature, releases 16 callers through an async barrier, and compares the same in-flight operation before releasing the drain. Cleanup joins all owned calls and distinct drain handles and preserves assertion and cleanup failures. No lifecycle product code changed. Root and independent delta review passed; `dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ShellDrainPropertyTests --logger trx` passed all five tests, with zero failures/skips. Corrected-head CI37844568005 passed the full solution build (zero warnings/errors),795/795 library and31/31 end-to-end tests.

## Actual public-package qualification (2026-10-08)

[Public acceptance record](https://github.com/valence-works/cshells/issues/161#issuecomment-6069222750). [PR #162](https://github.com/valence-works/cshells/pull/162) normally squash-merged as `c29b3eb86e3e68fb18003d0604bddb1f7314d8e3`; its full tree equals reviewed `d2f1000d7e2a87b8731a6a187a8288320ee91f75`. [Corrected-head CI37844568005](https://github.com/valence-works/cshells/actions/runs/37844568005) and [main Packages37844769622](https://github.com/valence-works/cshells/actions/runs/37844769622) passed all-target Release builds,795 library+31 end-to-end tests, with zero warnings/errors. The main run published all ten `0.0.30-preview.169` packages to Feedz; NuGet.org deployment was skipped.

The root audit downloaded all ten public archives and found them byte-identical to owner artifacts, with exact source commit/version, matching family dependencies, no Nuplane dependency outside the adapter, and net8.0/net9.0/net10.0 assets. An outside-checkout PackageReference-only consumer used a newly isolated NuGet cache and the exact public adapter family plus Nuplane `0.0.11-preview.99` (`eb2cf6c2ee1f79dc2c45fb83cc415bbe4856d0d4`). Restore/build and all three runs exited0; build had zero warnings/errors.

| Actual runtime | Live-options cases | Loaded package assets verified |
| --- | --- | --- |
| .NET8.0.10 / net8.0 | 8 passed, zero failed | 5 |
| .NET9.0.9 / net9.0 | 8 passed, zero failed | 5 |
| .NET10.0.8 / net10.0 | 8 passed, zero failed | 5 |

The cases cover defaults; static/dependency-aware configuration; reload-off→on without invented work; disabled deliveries creating no epochs; failed freshness consumed by Begin while disabled; retained reload retry across disabled/off settings; in-flight copied options/callback versus later configuration; and undefined trigger rejection without state advancement. The runner verified canonical/copied source hashes, exact case sets and target runtimes, cache source/archive/SHA512, informational source commits, and every loaded CShells/CShells.Abstractions/CShells.Nuplane/Nuplane.Abstractions/Nuplane.Loading.Abstractions DLL against its public target-framework archive bytes. No private pack or project reference supplied acceptance.

Root and a separate reviewer recomputed all source/copy hashes, runtime/case sets, loaded DLL bytes, cache/archive/source/SHA512 and the complete-family audit linkage, with no discrepancies. Raw commands, source, cache, results, archive audits and review records are local-only under the program artifacts `cshells-published-preview-169/` and `cshells-161-public-consumer-0.0.30-preview.169/`. This qualifies the adapter's options policy through public interfaces and synthetic deliveries/doubles. Actual Nuplane installation-to-serving-shell behavior remains separately qualified at `.166`; complete Foundation host adoption, stable releases, readability/unload and safe pruning remain pending. Documentation-only follow-up publication does not create an executable consumer claim for another version.
