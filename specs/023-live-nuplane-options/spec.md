# Feature Specification: Apply Live Nuplane Integration Options

**Feature Branch**: `023-live-nuplane-options`

**Created**: 2026-10-08

**Status**: Draft

**Input**: User description: Make runtime changes to the optional Nuplane/CShells integration settings take effect for later package-reconciliation deliveries without restarting the host, while preserving existing refresh, reload, and callback behavior.

## User Scenarios & Testing

### User Story 1 - Apply updated integration settings on the next package change (Priority: P1)

A host operator changes Nuplane integration settings while the host is running. On the next eligible package-reconciliation delivery, the integration uses the settings that are current for that delivery. Work already admitted keeps the settings it captured when it began, and the next delivery observes later changes.

**Why this priority**: Hosts need the optional integration to preserve their existing per-delivery configuration behavior even though the integration coordinator is long-lived. Requiring a host restart to change reload or reporting policy would make the adapter behave differently from the host configuration contract it replaces.

**Independent Test**: Configure automatic reload off, deliver an eligible package change, then change the setting to on and deliver another eligible change. The second delivery refreshes the catalog and requests an active-shell reload. Separately hold one admitted delivery at a deterministic barrier while changing its settings, then prove that it completes with its captured policy and that the following delivery uses the new policy.

**Acceptance Scenarios**:

1. **Given** a running integration and automatic reload disabled, **When** the host changes automatic reload to enabled and a later eligible package update or removal is delivered, **Then** the later delivery applies the enabled policy without rebuilding the host service provider.
2. **Given** an eligible delivery paused at a deterministic operation barrier, **When** the host changes settings and replaces the reload-results callback, **Then** the paused delivery uses the values and callback it captured, and a later delivery observes the changed settings and callback.
3. **Given** standard options configuration or dependency-aware options configuration, **When** a supported setting changes through the configured options source, **Then** the next eligible delivery observes the change.

### User Story 2 - Preserve deferred work and configuration errors (Priority: P2)

A host can pause automatic reload or disable new observer work without losing work already recorded. When the host later enables the applicable behavior and delivers another eligible change, previously pending work can be retried. Invalid policy values fail clearly before the callback changes package epochs or queries the shell registry.

**Why this priority**: Runtime configurability must not discard deferred freshness or promotion work, and a bad value must not leave partially advanced coordinator state.

**Independent Test**: Arrange pending promotion work, disable automatic reload while allowing freshness, then enable it on a later eligible delivery and verify the pending promotion is retried without an unnecessary second catalog refresh. Arrange previously recorded freshness, disable new observer work, and verify a shell-generation build can consume that freshness. Verify invalid values and options-monitor failures leave epoch and registry-call counts unchanged.

**Acceptance Scenarios**:

1. **Given** a failed or deferred promotion remains pending, **When** automatic reload is disabled for a later eligible delivery, **Then** freshness may still advance but the pending promotion is neither attempted nor cleared; a later eligible delivery with automatic reload enabled retries it without rescanning an already-fresh catalog.
2. **Given** freshness work was recorded before observer work was disabled, **When** a shell generation begins while observer work remains disabled, **Then** the build consumes the recorded freshness before reading the catalog, without requiring or reading current observer options.
3. **Given** an undefined refresh policy or a failing current-options read, **When** an eligible callback arrives, **Then** the undefined policy produces an actionable configuration error and the current-options failure is surfaced before changing epochs or accessing the shell registry.
4. **Given** a callback is canceled or has no successfully applied packages and no committed removals, **When** it is delivered, **Then** it does no options read or epoch, catalog, or registry work.

## Edge Cases

- A configuration change during an admitted operation applies only to a later delivery; it does not cancel or rewrite the admitted operation.
- Capturing policy means reading the monitor's current options once and copying the four supported values synchronously before operation awaits. No atomic transaction is promised if a caller concurrently mutates the same options instance while those individual properties are being copied.
- `Enabled = false` admits no new observer work and does not erase previously recorded freshness or promotion epochs.
- `AutoReload = false` permits configured catalog freshness work but neither attempts nor acknowledges pending promotion work.
- A valid options update that changes only the reporting callback still applies to the next admitted reload operation; an operation already in progress uses the callback it captured.
- An options-monitor exception or undefined refresh-trigger value is surfaced before coordinator state or registry interaction changes.
- Direct replacement of `IOptions<NuplaneIntegrationOptions>` alone is unsupported; use standard options configuration/binding or supply the monitor contract.
- `BeginAsync` remains independent of live observer settings. It consumes recorded catalog freshness before the candidate reads the catalog and does not activate, reload, or report a shell.

## Requirements

### Functional Requirements

- **FR-001**: The optional integration MUST observe supported runtime setting changes on the next eligible package-reconciliation delivery without recreating the host service provider.
- **FR-002**: Each eligible delivery MUST use one captured set of the enabled flag, refresh policy, automatic-reload flag, and reload-results callback for all decisions and reporting in that operation.
- **FR-003**: Changes made after a delivery has captured its settings MUST affect later deliveries only; they MUST NOT cancel or change the admitted operation.
- **FR-004**: A delivery canceled before admission or with no successfully applied package and no committed removal MUST NOT read current options, mutate work epochs, access the shell registry, or refresh the catalog.
- **FR-005**: When observer work is disabled, the integration MUST record no new observer-requested work and MUST preserve previously recorded freshness and promotion work.
- **FR-006**: When automatic reload is disabled, eligible freshness work MAY proceed, but pending promotion work MUST remain unattempted and pending until a later enabled eligible delivery can retry it.
- **FR-007**: A later retry of pending promotion work MUST reuse successfully refreshed catalog state when no newer source change requires another refresh.
- **FR-008**: Shell-generation catalog freshness work MUST remain independent of current observer settings and MUST be consumed before a candidate reads the catalog.
- **FR-009**: An undefined refresh policy MUST produce an actionable configuration error before any epoch change or shell-registry access. Options-monitor and options-validation failures MUST propagate before those side effects without requiring a new wrapping exception.
- **FR-010**: Standard options configuration, configuration binding, and dependency-aware configuration MUST remain supported. Replacing only the `IOptions<NuplaneIntegrationOptions>` service MUST be explicitly documented as unsupported.
- **FR-011**: The integration MUST retain its existing callback eligibility, refresh trigger, reload-result, alias, and provider-ownership behavior apart from observing the current captured options for each eligible delivery.
- **FR-012**: The change MUST remain confined to the optional CShells.Nuplane adapter and its tests/documentation; it MUST NOT add a CShells-core or Nuplane-core options dependency, listener, timer, public flag, or second coordinator.

### Key Entities

- **Eligible reconciliation delivery**: A delivered package-reconciliation completion with at least one successfully applied package or one committed removal, unless canceled before admission.
- **Captured integration policy**: The four supported setting values copied for one admitted delivery and used consistently until that operation finishes.
- **Pending freshness and promotion work**: Existing coordinator epochs representing catalog refresh or shell reload work not yet successfully acknowledged. They remain authoritative and are not replaced by new persistent state.

## Assumptions

- Supported runtime changes arrive through the standard options monitor and its configured providers. The adapter does not provide a new settings source or dual-source precedence.
- Individual policy fields are copied synchronously from the current options object. If a caller mutates one object concurrently during that copy, cross-property atomicity is outside this contract; deterministic tests change options after capture.
- Existing callback eligibility, epoch sequencing, catalog commit participation, shell-registry access, and reload outcome semantics remain as defined by the current optional adapter.
- Final stable package adoption and Foundation host qualification are separate downstream gates; this feature does not change Foundation pins, locks, or host composition.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A supported options change is reflected by the first later eligible completion, with no service-provider recreation.
- **SC-002**: A deterministic operation-barrier test proves that a setting or callback update during an admitted operation does not alter that operation and is observed by the following delivery.
- **SC-003**: Tests prove that disabling observer work or automatic reload preserves already-pending work and that a later enabled delivery resumes it without unnecessary catalog refresh.
- **SC-004**: Tests prove that canceled and empty deliveries do not read current settings or touch coordinator epochs, registry, or catalog.
- **SC-005**: Undefined refresh-trigger values fail with an actionable diagnostic before epoch mutation or registry access; monitor and options-validation exceptions propagate before those side effects.
- **SC-006**: Existing adapter tests and all required source-package, public-package, and runtime gates pass without adding a package dependency or changing core hosting behavior.
