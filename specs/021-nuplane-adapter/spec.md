# Feature Specification: Optional Nuplane Feature Discovery and Deferred Catalog Freshness

**Feature Directory**: `[021-nuplane-adapter]`
**Assigned Branch**: `codex/156-nuplane-integration`
**Created**: 2026-10-08
**Status**: Draft
**Input**: CShells issue #156: add an optional Nuplane integration that discovers loaded package assemblies, refreshes the feature catalog before the next shell build, and supports host-specific automatic reload policy.

## User Scenarios & Testing

### User Story 1 - Discover features from loaded Nuplane packages (Priority: P1)

A host engineer installs the optional integration and explicitly selects it for feature discovery. Features in packages already loaded by Nuplane become available to CShells in the order Nuplane reports them. The integration does not load packages itself or infer loaded assemblies from durable file references.

**Why this priority**: This is the core value of the optional integration and is independently usable without automatic shell reload.

**Independent Test**: Compose a host with a fake Nuplane assembly catalog, request feature discovery, and verify that only the catalog's loaded assemblies are considered in catalog order. Verify cancellation and an empty or unavailable catalog.

**Acceptance Scenarios**:

1. **Given** a catalog containing package entries with loaded assemblies and durable assembly references, **When** CShells requests feature discovery, **Then** it receives only the loaded assemblies in catalog order.
2. **Given** an empty or unavailable catalog, **When** CShells requests feature discovery, **Then** discovery receives an empty sequence without loading or activating a package.
3. **Given** a cancelled discovery request, **When** the provider is queried, **Then** cancellation is honored.
4. **Given** a host that does not install or opt into the adapter, **When** it composes CShells, **Then** its existing host, explicit, and custom assembly-provider behavior remains available.

---

### User Story 2 - Refresh features before a requested shell build (Priority: P1)

A host can receive package changes while no shell is active. The integration records that work without scanning packages or activating a shell. When the host later requests a shell build, the feature catalog is refreshed before that build selects features.

**Why this priority**: Deferred freshness is required for cold-start and last-shell removal cases where an event cannot immediately reconcile an active shell.

**Independent Test**: Start with a stale feature catalog and no active shells, report a package change, and verify no scan, reload, or activation occurs. Request a real shell build and verify one successful refresh happens before catalog selection. With `ChangedOrPending` and auto reload enabled, follow that build with an unchanged eligible completion and verify there is no extra scan or gratuitous reload of the newly promoted shell. Include removal of the last package and verify its feature is absent on the next catalog read/build.

**Acceptance Scenarios**:

1. **Given** an eligible package-completion event arrives while no shell is active, **When** the event is observed, **Then** refresh work is retained without scanning packages, activating a shell, or requesting reload.
2. **Given** refresh work is pending, **When** a shell build is requested, **Then** refresh runs before feature selection and the build sees the refreshed catalog.
3. **Given** a successful refresh and a later failed shell promotion, **When** another build is requested without a newer source change, **Then** no redundant refresh is performed.
4. **Given** a committed removal of the final package produces an eligible completion with no applied packages, **When** the next catalog read/build occurs, **Then** features from that package are absent.
5. **Given** a refresh fails or is cancelled, **When** a later eligible event or requested build occurs, **Then** the captured work remains pending until a refresh succeeds.
6. **Given** `ChangedOrPending` and auto reload are enabled and a package change arrives with no active shell, **When** the next real build refreshes the catalog and promotes the first shell, **Then** a later unchanged eligible completion neither rescans nor reloads that newly promoted shell.

---

### User Story 3 - Preserve each host's refresh and reload policy (Priority: P1)

A host engineer independently controls whether observer-driven integration is enabled, which eligible package completions trigger refresh, and whether successful refreshes request automatic reload. Existing host profiles retain their distinct behavior: Foundation.Host refreshes and reloads on every eligible completion; Workbench refreshes on changes or pending work and does not automatically reload by default.

**Why this priority**: A single reload flag cannot represent the two established hosts, and refresh must continue when automatic reload is disabled.

**Independent Test**: Run the same unchanged eligible completion, changed completion, and pending-work scenarios under generic defaults and both host profiles; assert refresh and reload requests independently. Repeat with observer integration disabled.

**Acceptance Scenarios**:

1. **Given** no host-specific configuration, **When** the integration is composed, **Then** observer integration is enabled, refresh uses the changed-or-pending trigger, and automatic reload is disabled.
2. **Given** Foundation.Host's profile and at least one active shell, **When** each eligible completion is observed, including an unchanged completion with successfully applied packages, **Then** the catalog refreshes and automatic reload is requested.
3. **Given** Workbench's profile, **When** an unchanged completion arrives with no pending work, **Then** no refresh or automatic reload occurs; changed or pending work refreshes without automatic reload.
4. **Given** automatic reload is disabled, **When** refresh succeeds, **Then** catalog freshness is still acknowledged and no automatic reload is requested.
5. **Given** observer integration is disabled, **When** package events arrive, **Then** observer-driven refresh and reload are suppressed while the provider's normal initial query remains available.

---

### User Story 4 - Retry failed work without coupling unrelated observers (Priority: P2)

A host can recover from refresh or shell-reload failures. Newer package changes remain pending when they arrive during a refresh. A failure from one observer does not prevent unrelated observers from receiving the same notification.

**Why this priority**: Explicitly retained pending work makes failures recoverable while keeping the integration bounded to callbacks and requested builds.

**Independent Test**: Inject refresh failures, cancellation, reload exceptions, partial per-shell reload errors, and an unrelated failing observer. Verify catalog freshness and reload pending state are acknowledged independently, failures remain retryable, and later observers still run.

**Acceptance Scenarios**:

1. **Given** a newer package event arrives while refresh is in progress, **When** that refresh succeeds, **Then** only its captured source epoch is acknowledged and the newer epoch remains pending.
2. **Given** reload throws or returns an error for one or more active shells, **When** the completion callback finishes, **Then** reload work remains pending for the next eligible completion.
3. **Given** catalog refresh succeeds but reload fails under the changed-or-pending trigger, **When** a later eligible completion arrives without a new package change, **Then** reload is retried without another catalog scan. Under the every-eligible-completion trigger, that later callback refreshes again before retrying reload.
4. **Given** one unrelated observer fails, **When** Nuplane dispatches the completion, **Then** other registered observers still receive it.
5. **Given** an idle or failed-only cycle produces no eligible completion, **When** it completes, **Then** it does not fabricate a retry callback; pending freshness is still handled before the next requested shell build.

## Edge Cases

- A completion for committed removal of the last package has an empty applied-package list and still carries removal work.
- Empty idle cycles and failed-only cycles are quiet and must not be treated as retry notifications.
- A package event can arrive during a refresh; the newer source epoch remains pending for a later eligible callback or requested build.
- A build already underway may select a snapshot older than a newer event. This feature does not invalidate in-flight builds or guarantee every overlapping candidate uses the newest package set.
- An automatic reload can fail by throwing or by returning per-shell errors; both forms keep reload work pending.
- Neither refresh nor reload retries run from a timer or background quiet-cycle loop.
- The integration does not promise durable exactly-once observer delivery across process failure or cancellation after Nuplane has persisted package state.
- The integration does not own package-context unloading, generation readability protection, store deletion, refusal translation, or restart/locked-package policy.

## Requirements

### Functional Requirements

- **FR-001**: The optional integration MUST supply feature discovery from the assemblies Nuplane currently reports as loaded, in catalog order.
- **FR-002**: The integration MUST ignore durable assembly-file references when selecting assemblies for feature discovery and MUST NOT load or activate packages itself.
- **FR-003**: A host MUST explicitly opt into the Nuplane provider through its CShells composition. Installing the optional package alone MUST NOT replace an existing provider.
- **FR-004**: The integration MUST expose independent controls for observer enablement, refresh trigger, and automatic reload.
- **FR-005**: Generic defaults MUST enable observer integration, refresh on changed or pending work, and disable automatic reload.
- **FR-006**: Foundation.Host MUST retain enabled integration, refresh on every eligible completion, and automatic reload. Workbench MUST retain enabled integration, refresh on changed or pending work, and automatic reload disabled.
- **FR-007**: Disabling the integration observer MUST suppress observer-driven refresh and reload while leaving the provider's normal initial query available.
- **FR-008**: An eligible event MUST record its source-change epoch before checking for an active shell. If none is active, the integration MUST retain the work and return without a package scan or shell activation.
- **FR-009**: Before feature catalog initialization or read during a requested shell build, the integration MUST refresh outstanding work and acknowledge only the epoch captured by a successful refresh.
- **FR-010**: Refresh failure or cancellation MUST leave the captured freshness work pending. A newer event received during refresh MUST remain pending after the captured refresh succeeds.
- **FR-011**: The integration MUST keep catalog freshness and automatic-reload pending state independent. Reload MUST begin only after releasing the refresh gate, and pending reload work MUST clear only when all applicable active-shell reloads succeed.
- **FR-012**: The integration MUST inspect both thrown reload exceptions and returned per-shell errors. Failed reload work MUST be retried on a later eligible completion.
- **FR-013**: The integration MUST make a stable, read-only snapshot of raw per-shell reload results available to a generic host callback. It MUST preserve result names, exception instances, nested exception chains, and partial outcomes without Elsa-specific translation. Callback failures MUST leave reload work pending; a successful reload already committed before callback failure is not rolled back.
- **FR-014**: Observer failure MUST NOT suppress later unrelated observers. The integration MUST NOT invent timer-based or quiet-cycle retry behavior.
- **FR-015**: The package MUST remain optional and downstream of CShells core, reference no Elsa types, and leave package protection/unload, store mutation, refusal translation, and restart/locked policy to their existing owners.
- **FR-016**: The package MUST be included in the repository's solution and canonical explicit pack/publish matrix, increasing the package-family output from nine to ten packages while retaining the three supported target frameworks.
- **FR-017**: Qualification MUST cover real dependency injection and shell-promotion flows, including loaded-package discovery, actual feature construction, last-package removal, both host profiles, retries, observer alias identity/isolation, and use without the optional provider.

### Key Runtime Concepts

- **Source-change epoch**: A monotonically advancing marker for Nuplane package changes that may require feature-catalog refresh.
- **Catalog freshness work**: A captured source-change epoch that remains outstanding until a successful refresh acknowledges it.
- **Automatic-reload work**: A separate pending request to reload applicable active shells after a successful refresh.
- **Eligible completion**: A delivered Nuplane `OnPackagesReconciled` callback with at least one successfully applied package or at least one committed removal. A callback with an empty applied list is eligible when it carries committed removals; unchanged empty idle cycles and failed-only cycles deliver no callback.

## Success Criteria

### Measurable Outcomes

- **SC-001**: In deterministic pipeline tests, every feature selected from Nuplane is backed by an assembly reported as loaded by the public package catalog; no durable assembly reference is used.
- **SC-002**: In a cold-start scenario with no active shell, package changes cause zero catalog scans before a real build request, and the next build refreshes before catalog selection.
- **SC-003**: In the two host profiles, refresh and reload counts match their specified policies for unchanged, changed, pending, disabled-integration, and reload-off cases.
- **SC-004**: For successful last-package removal, the next catalog read/build contains zero features from that package; a later quiet cycle produces no synthetic retry callback.
- **SC-005**: All three supported target frameworks build, and the explicit package matrix contains ten packages with no Nuplane dependency in CShells core and no Elsa dependency in the adapter.
- **SC-006**: An outside-checkout consumer restored from the published package feeds can discover a packaged feature, promote a shell that constructs it, and observe its removal after the final package is removed.
- **SC-007**: A host callback receives unchanged raw partial reload results, including nested refusal exceptions, so the host can apply its own warning/error and placeholder-translation policy.

## Assumptions

- Nuplane's published observer and loaded-assembly catalog contracts are the inputs to this integration; the adapter does not add package-store or load-context policy.
- The integration's build participant exists to refresh before CShells reads its catalog. Its lease does not promise assembly or snapshot readability protection through the generation lifetime.
- Source work newer than a successful refresh is handled by the next eligible completion or requested build; an already-overlapping build is not invalidated.
- Final qualification uses the actual published Nuplane and CShells package feeds after the adapter is included in a new CShells package family. Source-build success alone does not qualify the adapter.
- Fake-catalog and fake-observer tests prove deterministic adapter state and DI behavior; acceptance still requires a real Nuplane `.99` autoload/reconciliation/loading consumer using the published adapter package.
- Foundation package pins and lock files are updated by separate adoption/release work, not by this feature.
