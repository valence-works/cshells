# Validation: Settled Active Observation

Use the repository .NET SDK through the shared build-slot wrapper. The focused scenario uses real DI/registry composition and gated participants; no elapsed-time sleeps determine correctness.

```bash
dotnet restore CShells.sln
dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release --filter FullyQualifiedName~ShellRegistrySettledObservationTests
dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release --no-restore
dotnet test tests/CShells.Tests.EndToEnd/CShells.Tests.EndToEnd.csproj -c Release --no-restore
dotnet build src/CShells/CShells.csproj -c Release --no-restore
```

Expected: cold observations do no provider/build work; gated initial and replacement commit/completion remain absent; successful settlement exposes the exact current instance; commit rejection restores the prior settled generation; draining/removed shells disappear. Invalid names and case-insensitive matching retain existing behavior.

For consumption, resolve `IShellRegistry` and cast to the optional contract documented in [contracts/ISettledShellRegistry.md](contracts/ISettledShellRegistry.md). The host decides what unsupported capability means for its own readiness policy.

Root acceptance additionally records a valid compiled marker-bypass mutation that fails the pending-commit test, its restored pass, hosted exact-head/main checks, and an external consumer restored from the actual public preview on net8/net9/net10. Inspect nuspec repository commit, archive hash, and loaded DLL bytes. Local source references and privately packed previews do not satisfy that public-package gate. Foundation's complete-host regression/E2E proof remains separate.

## Source qualification — 2026-10-08

Production/test source at `ddbed6ac28e268dd308f42c71f30ee505266e149` plus validation-guide-only `3aaf6654898e01e1a3b2b047838682d6ae49704a` passed root and independent pinned review. Root's second review corrected the cold fixture's omitted teardown shell and nested activation joining after a drain-join failure. All five product/test/document snapshot hashes were independently verified; no product blocker remained.

- Root local focused observation + existing settlement + runner tests: 40/40, zero failed/skipped, Release/net10.0.
- [PR CI 37833413147](https://github.com/valence-works/cshells/actions/runs/37833413147) for head `3aaf665` (tested merge result `b4a012d`): complete solution build for net8/net9/net10, zero warnings/errors; full 789 library tests and 31 EndToEnd tests passed. These full/build gates ran on the hosted runner; they are not represented as an additional local full-suite run.
- Isolated compiled mutation removed only the committed-marker guard from `GetSettledActive`: the pending-initial-Commit regression failed (one executed, one failed). Restoring byte-identical registry source made that one test pass. Original SHA256: `d8fcdf56f08a62450e2bb5f8438f862bb7fbde99012184aa660a4cd45cf5422d`. Both commands actually built/executed tests; a compile failure is not counted as a regression bite.

Raw source-review manifests, local TRX/logs, hosted CI metadata/log, and mutation `result.json` are local-only under `/Users/sipke/.codex-workspaces/artifacts/modular-hosting-2500/`, in `cshells-158-source-review-v2/`, `cshells-158-local-tests/`, `publication-preparation/`, and `cshells-158-mutation-proof/` respectively. [PR #159](https://github.com/valence-works/cshells/pull/159) is the reviewable org-branch delivery. No Copilot request was made; Greptile is optional under owner decision D15. At that source checkpoint, new-head hosted checks, normal merge/main publication, and the actual-public-package consumer were still pending T010 gates; the public-package qualification below completes them. A source-reviewed eight-case outside-checkout consumer is prepared, including committed drain while an outstanding scope retains the raw active pointer; its private net10 candidate preflight passed all eight cases, which is not public acceptance. The private pack intentionally included only net10 assets and emitted NU5128 multi-target asset warnings; the public gate requires all three target frameworks.


## Public-package qualification — 2026-10-08

[PR #159](https://github.com/valence-works/cshells/pull/159) merged normally as `94eab66fea5a5d33ceb8e660c0c841a23db9d96f`, with a tree identical to reviewed head `edd27aa0c89f9a5311706e0900f37736d10cdbf9`. Required [PR CI 37834987489](https://github.com/valence-works/cshells/actions/runs/37834987489) passed for that head (tested merge result `6974834`); all five pinned product/test/document hashes match the actual merge.

The actual main-push [Packages run 37835400396](https://github.com/valence-works/cshells/actions/runs/37835400396) passed full solution build (net8/net9/net10, zero warnings/errors), 789 library tests and 31 EndToEnd tests, then published all ten `0.0.30-preview.167` packages to Feedz. The public archive audit found every archive byte-identical to its owner-run artifact, with exact version/source commit, internal-family dependency versions and all three framework assets. `CShells.Nuplane` alone has the allowed Nuplane `.99` preview dependencies.

A fresh outside-checkout PackageReference-only consumer restored from the public feed into an isolated cache, built all three targets with zero warnings/errors, and passed eight scenarios on each actual runtime (.NET 8.0.10, 9.0.9 and 10.0.8): cold/unknown inert observation; gated initial Commit/Complete then success; diagnostic Complete failure accepted; initial rejection; successful reload with pre-publication old/provisional-null/new states; rollback restoring old; direct drain during Complete rejecting the candidate; and committed-current drain with a held scope retaining the raw pointer while settled observation returns null. It uses public APIs only and bounded cleanup; its held-scope case deliberately uses an unbounded drain policy and does not claim drain-timeout behavior.

Root checked canonical/copied source hashes, command exits, exact scenario names/runtime target, the matching ten-package audit, cache source/archive/SHA512, assembly informational source commit, and all six loaded DLLs against the published target-framework assets. Independent review separately audited the consumer and runner; the initially missing family-audit linkage was corrected and re-reviewed before execution. This proves the generic optional observation contract, not Foundation health policy, complete Host/Workbench boot, stable release, package readability/unloading, or pruning. Feature #143 and Foundation adoption remain open.

Raw retained evidence is local-only under `/Users/sipke/.codex-workspaces/artifacts/modular-hosting-2500/cshells-published-preview-167/` and `cshells-158-public-consumer-0.0.30-preview.167/`; the owning [Task #158](https://github.com/valence-works/cshells/issues/158) records the public acceptance checkpoint. D15 applies: no Copilot request and no external reviewer approval claimed.
