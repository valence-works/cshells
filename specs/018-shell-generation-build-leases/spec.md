# Feature Specification: Shell Generation Build Leases

**Feature Branch**: `018-shell-generation-build-leases`
**Created**: 2026-10-08
**Status**: Draft
**Input**: User description: "Protect each shell generation from before runtime feature catalog selection through completed provider teardown using optional root-owned build participants and leases."

## User Scenarios & Testing

### User Story 1 - Participate in a precisely identified shell build (Priority: P1)

A host integration can associate temporary protection with one attempted shell generation before CShells reads the feature catalog, then refine that protection from the exact detailed catalog snapshot selected for the build. A failed composition or invalid shell name does not acquire protection.

**Why this priority**: Integrations need a stable generation identity and the exact selected assemblies before CShells constructs feature services.

**Independent Test**: Register two recording participants and a recording blueprint/feature; verify immutable identity reservation, composition/name validation, participant order, initial catalog selection, exact snapshot identity, and callback-before-feature construction order.

**Acceptance Scenarios**:

1. **Given** a valid blueprint, **When** a generation is requested, **Then** CShells reserves a unique monotonic descriptor and matching `ShellId` from blueprint identity and immutable metadata before composition.
2. **Given** composition succeeds and the composed shell name matches the blueprint, **When** the build proceeds, **Then** participants begin in root registration order before any catalog initialization or read.
3. **Given** the selected detailed catalog snapshot, **When** the provider is built, **Then** every acquired lease receives that exact snapshot instance once, before feature construction.
4. **Given** composition fails or the composed name differs, **When** the request ends, **Then** no participant begins and no lease is acquired.
5. **Given** a participant attempts activation or reload reentry for the same shell name, **When** its callback runs, **Then** the limitation is documented; callbacks for distinct shell names can proceed concurrently.

---

### User Story 2 - Hold protection through the full generation lifetime (Priority: P1)

A successful candidate owns all acquired leases before initializers run. The leases remain active through initialization, activation, overlap with prior generations, draining, lifecycle notification and complete shell-provider disposal; only confirmed teardown permits release.

**Why this priority**: A shell provider may continue using assemblies and services after it is marked `Disposed`, while its container is still unwinding.

**Independent Test**: Use two overlapping shell generations and a gated asynchronous disposable in the old provider. Verify both generation leases stay held while the old provider teardown is blocked and each is released only after its own provider teardown completes.

**Acceptance Scenarios**:

1. **Given** a provider has been built, **When** CShells resolves initializers, **Then** the lease set already has one owner on the candidate shell.
2. **Given** a prior generation is draining while a new generation is active, **When** the old provider is disposed, **Then** the old lease stays held until disposal completes and releasing it does not release the new generation lease.
3. **Given** the lifecycle reaches `Disposed`, **When** provider teardown is still in progress, **Then** no lease is released yet.
4. **Given** provider teardown and the `Disposed` notification both complete successfully, **When** leases are released, **Then** every lease is attempted once in reverse acquisition order.
5. **Given** no build participants are registered, **When** a shell activates and drains, **Then** existing shell behavior and service lifetimes remain unchanged.

---

### User Story 3 - Keep unresolved leases rooted when cleanup is uncertain (Priority: P1)

If candidate construction or teardown fails, CShells attempts safe cleanup without masking the original failure. Any lease whose release or generation teardown cannot be confirmed remains strongly rooted by the root-lifetime registry, independently of shell-slot history or shell/provider reachability.

**Why this priority**: Releasing protection after an uncertain teardown can leave a cooperating host without a trustworthy record of a still-used generation.

**Independent Test**: Inject faults at begin, snapshot callback, feature configuration, initialization, lifecycle notification, provider disposal and lease disposal. Assert exact unwind order, preserved primary errors, attempted releases, and root retention of unresolved leases after shell references are dropped and garbage collection is forced.

**Acceptance Scenarios**:

1. **Given** a later `BeginAsync` call fails, **When** earlier participants have acquired leases, **Then** prior leases are released in reverse order, catalog/provider work does not start, and the participant failure remains primary.
2. **Given** snapshot selection or feature/provider construction fails, **When** cleanup runs, **Then** all acquired leases are attempted in reverse order and cleanup faults do not mask the build failure.
3. **Given** an initializer fails before the candidate is published, **When** partial provider disposal succeeds, **Then** leases release afterward with no extra lifecycle transitions or second provider disposal.
4. **Given** initializer cleanup, lifecycle notification, or provider disposal fails, **When** teardown cannot be confirmed, **Then** the registry roots unresolved leases for the host lifetime and does not release them.
5. **Given** normal teardown succeeds but several lease releases fail, **When** cleanup completes, **Then** every lease was attempted in reverse order, only failed leases remain rooted, and failures are surfaced deterministically.
6. **Given** activation fails and shell disposal also fails, **When** activation rollback returns, **Then** the original activation failure remains primary and unresolved leases are rooted.
7. **Given** cancellation occurs after acquisition, **When** cleanup begins, **Then** cleanup ignores the cancelled build token and still attempts each release.

## Edge Cases

- Generation exhaustion is rejected before the `long` counter can be narrowed to the descriptor's `int` generation.
- Blueprint metadata is copied into an immutable snapshot before composition can mutate its source dictionary.
- `BeginAsync` must clean up a partial acquisition if it throws before returning its lease; a null lease result fails clearly.
- A failed lease release must not prevent attempts to release earlier leases, and successful releases are not retained.
- A provider/lifecycle teardown fault prevents all lease release; a registry-owned retained object must not itself retain the `Shell`, provider, or full catalog snapshot.
- Retaining the lease object cannot reverse participant side effects. A participant that cannot prove release must preserve its external protection while throwing.
- Cleanup never adds lifecycle transitions to an unpublished failed candidate and never disposes the same provider twice.
- `Disposed` can be reported before provider disposal finishes; it is not the lease-release boundary.
- A late drain must join shared provider teardown without resolving services from a disposed provider. Completed operations are released, public `Drain` remains null after `Disposed`, and a repeated drain preserves teardown failures.
- A no-op participant lease is the way a participant with no work participates; CShells does not invent fake participants.

## Requirements

### Functional Requirements

- **FR-001**: The framework MUST define an optional build participant, immutable build context and asynchronous generation lease in the abstractions package.
- **FR-002**: The framework MUST reserve an unused monotonic generation and immutable descriptor metadata from blueprint identity before composition, including across unregister/recreate within a registry lifetime, and reject exhaustion before narrowing the generation type.
- **FR-003**: The framework MUST preserve composed-name validation and MUST begin participants in root registration order only after successful composition and validation.
- **FR-004**: Participants MUST begin before catalog initialization or any catalog read; no CShells global, catalog-refresh or service-collection lock may surround participant callbacks.
- **FR-005**: Each acquired lease MUST receive the exact detailed snapshot selected for feature building, once and before feature construction.
- **FR-006**: Root participants MUST be resolved in registration order from the root provider and excluded from all child shell providers.
- **FR-007**: The build pipeline MUST have exactly one owner for every acquired lease set as ownership passes from registry to builder to candidate shell.
- **FR-008**: Leases MUST remain held through initializer execution, activation, draining, lifecycle notification and complete provider teardown; release MUST happen only after successful teardown confirmation.
- **FR-009**: Lease cleanup MUST attempt all acquired leases once in reverse acquisition order, retaining only failed lease objects after partial release failure.
- **FR-010**: If provider teardown or the `Disposed` lifecycle notification fails, CShells MUST retain all unresolved leases in a synchronized root-lifetime registry collection that does not retain the shell, provider or full snapshot.
- **FR-011**: Build and initializer failures MUST remain the primary failures when cleanup also fails; otherwise-successful teardown MUST surface lease-release failures deterministically.
- **FR-012**: Cleanup MUST proceed without the cancelled build token, and no-participant hosts MUST preserve existing behavior.
- **FR-013**: Same-name activation/reload reentry from participant callbacks MUST be documented as unsupported; different shell names MUST be able to run callbacks concurrently.
- **FR-014**: Public contract types and members MUST have XML documentation describing callback ordering, ownership/lifetime semantics, exception behavior and reentry constraints.

### Key Entities

- **Build context**: Immutable identity for one attempted shell generation, including its reserved descriptor and `ShellId`.
- **Build participant**: Root-owned callback implementation that begins generation-specific protection before catalog selection.
- **Generation lease**: Participant-owned object that receives the selected detailed snapshot and releases external protection when teardown is confirmed.
- **Lease-set owner**: Framework-owned idempotent owner that tracks acquired and unresolved leases as ownership moves between build stages.
- **Retained lease set**: Root-registry-owned collection entry preserving unresolved leases when teardown or release cannot be confirmed.

## Success Criteria

### Measurable Outcomes

- **SC-001**: For each attempted generation, descriptor reservation, participant acquisition, snapshot selection, feature construction and initialization occur in the specified order.
- **SC-002**: Every successfully torn-down generation attempts each lease release exactly once in reverse acquisition order, only after provider disposal completes.
- **SC-003**: Any uncertain teardown or failed release remains strongly reachable from the root registry after the shell and provider are no longer reachable.
- **SC-004**: A gated provider-disposal test proves the generation lease remains held throughout `Disposed` notification and blocked provider teardown, and releases only after the gate opens.
- **SC-005**: Injected cleanup faults never mask the original build/activation/initializer failure and do not skip attempts for other leases.
- **SC-006**: Hosts with no participant registrations retain the existing activation, reload, failure and disposal behavior.
