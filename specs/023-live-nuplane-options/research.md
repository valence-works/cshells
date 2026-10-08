# Research: Apply Live Nuplane Integration Options

## Current behavior and reproduced gap

`CShellsNuplaneBuilderExtensions.WithNuplaneFeatureDiscovery` registers one root-owned coordinator as both `INuplaneObserver` and `IShellGenerationBuildParticipant`, with the coordinator appended after existing Nuplane observers. Its registration resolves `IOptions<NuplaneIntegrationOptions>`. `NuplaneRefreshCoordinator` reads `.Value` once in its constructor and stores that mutable options object for its lifetime. All later observer deliveries consult this captured object, so an ordinary configuration reload can update `IOptionsMonitor.CurrentValue` while the singleton coordinator keeps using the earlier values.

The evidence is source-grounded in:

- `src/CShells.Nuplane/CShellsNuplaneBuilderExtensions.cs`: existing options configuration and root coordinator aliases.
- `src/CShells.Nuplane/Internal/NuplaneRefreshCoordinator.cs`: constructor snapshot and per-delivery eligibility, epoch, refresh, and reload decisions.
- `src/CShells.Nuplane/NuplaneIntegrationOptions.cs`: the four mutable policy properties and their current defaults.
- `src/CShells.Nuplane/README.md`: generic defaults, host-profile examples, observer eligibility, refresh/reload behavior, and callback contract.
- `tests/CShells.Tests/Integration/Nuplane/NuplaneRefreshCoordinatorTests.cs` and `NuplaneCompositionTests.cs`: current epoch, cold-build, disabled, configuration, alias, and host-profile regression patterns.

An outside-checkout public-package probe ran against actual CShells.Nuplane `0.0.30-preview.167` (source `94eab66fea5a5d33ceb8e660c0c841a23db9d96f`) and Nuplane abstractions `0.0.11-preview.99` (source `eb2cf6c2ee1f79dc2c45fb83cc415bbe4856d0d4`). It bound real options to a reloadable configuration provider, resolved the adapter's registered public observer, and delivered two eligible updates while a public fake catalog and registry counted work. After changing `AutoReload` from false to true, `IOptionsMonitor.CurrentValue` reported true and the second delivery refreshed the fake catalog, but no registry reload occurred. Root-local evidence is retained at `/Users/sipke/.codex-workspaces/artifacts/modular-hosting-2500/foundation-adoption-readiness/options-snapshot-probe/`; it is not part of this repository. This is evidence of the captured-snapshot gap, not a fix, Foundation host/store test, or runtime-adoption result.

## Decisions

### D1. Use the existing standard options monitor

**Decision**: The root coordinator depends on `IOptionsMonitor<NuplaneIntegrationOptions>` and reads its `CurrentValue` once for each eligible, non-canceled reconciliation delivery.

**Rationale**: This is the existing Microsoft options mechanism for current values in singleton consumers. It makes the next delivery see provider reloads without adding a package, public flag, background listener, or second configuration source. The existing options package reference already supplies the abstraction.

**Alternatives rejected**:

- Keep `IOptions<T>` and recreate the host: violates the per-delivery configuration behavior Foundation currently relies on.
- Subscribe to `IOptionsMonitor.OnChange` and cache a mutable settings object: adds a second lifetime/notification state machine and risks a callback changing an operation already in progress.
- Add a new CShells or Nuplane settings API: duplicates standard host configuration and expands public surface without a need.

### D2. Copy a small policy at the delivery boundary

**Decision**: After argument, cancellation, and eligibility checks, copy `Enabled`, `RefreshTrigger`, `AutoReload`, and `OnReloadResults` into one private immutable per-delivery value before epoch changes, registry access, or awaits. Use those copies for the whole operation, including invoking the captured reporting callback.

**Rationale**: A single current-options read ensures the operation does not intentionally reread mutable settings at later phases. Copying only the four values keeps the boundary small and makes the lifetime rule testable. A deterministic barrier can change the source object after capture and prove that the admitted delivery remains unchanged while a later delivery observes the new values.

**Limit**: Reading `CurrentValue` once is not a synchronization transaction over arbitrary callers mutating the same options instance concurrently while its four fields are copied. The contract guarantees that changes after capture do not alter the captured value; it makes no cross-property atomicity promise during that synchronous copy.

### D3. Preserve existing epoch semantics

**Decision**: Keep the catalog and reload epochs, locks, gates, commit timing, and retry rules. Disabled observer work records no new request but leaves existing epochs. Automatic reload disabled allows eligible freshness work while leaving pending reload work unattempted and unacknowledged. A later enabled eligible delivery can retry retained work; a fresh catalog is reused unless a newer source change requests another scan. `BeginAsync` remains independent of options and consumes already-recorded freshness before the catalog read.

**Rationale**: Existing source and tests make the epoch separation, build participation, and reentry boundaries explicit. The desired change is how each observer delivery receives policy, not a replacement state machine. Future Foundation/Workbench host defaults select their own profiles without changing generic defaults.

### D4. Fail invalid policy before coordinator side effects

**Decision**: Validate the captured `RefreshTrigger` before epoch mutation, registry resolution/access, or asynchronous work. An undefined enum receives an actionable configuration error. Options-monitor and options-validation exceptions propagate before state/registry work; the adapter does not promise a new wrapper for arbitrary provider failures. No last-known-good policy cache is added.

**Rationale**: Silently continuing with stale policy would hide invalid operator input. Failing before coordinator work leaves epochs and registry access unmodified and is directly testable with counters, while preserving the original type/message of monitor and validation failures.

### D5. Keep the options-source compatibility boundary explicit

**Decision**: Continue supporting `WithNuplaneFeatureDiscovery(Action<NuplaneIntegrationOptions>)`, standard options binding/configuration, and dependency-aware options configuration. Direct replacement of only `IOptions<NuplaneIntegrationOptions>` is explicitly unsupported; callers with a custom options source must provide `IOptionsMonitor<NuplaneIntegrationOptions>`.

**Rationale**: Standard options providers feed both `IOptions<T>` and `IOptionsMonitor<T>`. The current supported adapter examples already use the fluent callback and `services.AddOptions<T>().Configure<TDependency>(...)`. A hidden fallback that combines `IOptions<T>` and monitor values would create undefined precedence and retain the stale snapshot defect.

## Resolved uncertainty

The runtime option source, timing boundary, old-work behavior, error behavior, supported registration path, and ownership scope are settled by the reviewed Task #161 requirements gate and the audited public-package probe. No further owner choice is required before implementation planning. The gate and probe are root-local artifacts, not repository files. The probe's fake registry/catalog and single runtime are bounded diagnosis; the feature's actual acceptance still requires deterministic owner tests, full owner CI, all supported target builds, compiled mutation proof, and post-publication PackageReference consumers on actual net8/net9/net10.
