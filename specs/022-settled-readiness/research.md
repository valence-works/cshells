# Research: Settled Active Observation

## Existing authority

Current CShells main `2d81b21023387e11231d48720434ab8e88b38a93` publishes the candidate in `ShellRegistry.CreateGenerationAsync` before participant Commit. The internal `Shell.IsActivationCommitted` marker is published only after Commit, diagnostic-only Complete callbacks, and final active eligibility. `GetOrActivateAsync` waits for this boundary but can activate a cold shell. The activation runner's successful target is terminal and is not a current-readiness monitor.

Decision: expose a synchronous optional registry observation using that existing authority. Rationale: the copied Foundation.Host and Workbench endpoints both returned HTTP 200 for a gated provisional candidate, including a subsequently rejected reload, when run with actual public `.166` packages. The bounded probe's expected and observed exit was 42; this is a counterexample, not full-host acceptance. Program evidence is retained under `foundation-readiness-settlement-spike-20261008` in the program artifact directory.

## Alternatives

- Raw `GetActive`: includes provisional publication and reproduces the defect.
- Call `GetOrActivateAsync`: changes a read into activation and waits behind a transaction.
- Treat startup runner success as live readiness: remains satisfied after later reloads.
- Add a member to `IShellRegistry`: unnecessary implementation burden for custom registries; capability casting distinguishes unsupported from empty.
- Register a second DI service: unnecessary alias and child-provider exclusion surface.
- Add a readiness cache or lifecycle subscriber: duplicates authority and risks stale rollback state.

## Requirements interrogation

Goal, scope, generic ownership, optional compatibility, platform targets, and acceptance are resolved by the existing readiness feature and reproduced evidence. No storage or trust boundary changes. Host HTTP status and custom-registry fallback policy remain downstream. Package-store admission is a separately deferred owner decision with a revisit trigger before destructive pruning; it cannot invalidate this observation API. No new product-owner choice is required.
