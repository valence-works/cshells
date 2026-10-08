# Feature Specification: Share Host Singletons with Shells

**Feature Branch**: `016-share-host-singletons`
**Created**: 2026-10-08
**Status**: Draft
**Input**: User description: "Share selected disposable host singletons safely with shell service providers."

## User Scenarios & Testing

### User Story 1 - Share a selected host singleton safely (Priority: P1)

A host developer selects a singleton service for shell access. Every shell generation resolves the same host-owned instance for that registration, while the host remains responsible for disposing it.

**Why this priority**: Services that own process-wide resources need to be consumed by shells without being recreated or disposed once per shell generation.

**Independent Test**: Configure a selected host singleton and two shell generations, resolve it from each shell, and verify instance identity and host-owned disposal.

**Acceptance Scenarios**:

1. **Given** one selected host singleton registration, **When** two shells resolve it, **Then** both receive the same instance and disposing either shell does not dispose it.
2. **Given** the root host is disposed after shell use, **When** the selected singleton implements synchronous or asynchronous disposal, **Then** the host disposes it exactly once.
3. **Given** several unkeyed singleton registrations for the selected service type, **When** a shell resolves the service or its enumerable, **Then** the shell observes the same registration order and instances as the host.
4. **Given** an unselected host singleton, **When** separate shells resolve it, **Then** each shell retains the existing independent-copy behavior.

### User Story 2 - Reject invalid sharing requests clearly (Priority: P2)

A host developer receives a clear configuration error when selecting a service type that has no eligible unkeyed singleton registrations, includes a non-singleton registration, is open generic, or is excluded from shell copying.

**Why this priority**: Early, specific feedback prevents accidental lifetime changes and confusion around root-only services.

**Independent Test**: Configure each invalid selection and verify the corresponding diagnostic identifies the service type and explains the supported selection.

**Acceptance Scenarios**:

1. **Given** a selected type with no unkeyed registration, **When** shell provider construction validates sharing, **Then** configuration fails with an actionable message.
2. **Given** a selected type with mixed or non-singleton unkeyed registrations, **When** shell provider construction validates sharing, **Then** configuration fails without partially copying the registrations.
3. **Given** a selected type excluded from shells, **When** shell provider construction validates sharing, **Then** configuration fails and identifies the exclusion conflict.

## Edge Cases

- Keyed registrations of the selected service type are left unchanged and are not included in the shared unkeyed registration set.
- An explicit unkeyed `IEnumerable<TService>` or open generic `IEnumerable<>` registration is rejected for a selected service type because it overrides DI's generated enumerable of selected descriptors; keyed enumerable registrations remain independent.
- Repeated `AddCShells` calls before root provider construction contribute to one builder/registration and do not install duplicate CShells infrastructure.
- Aliases are distinct service types; selecting one service type does not implicitly share an implementation registered through another alias.
- Repeated host registration of the same service type preserves all unkeyed registrations and their descriptor order, including duplicate implementation types.
- A shell feature may register its own service after host services are copied; normal later-registration precedence remains in effect.
- A selected service cannot be silently omitted because it is on the root-only exclusion list.
- A closed generic selection cannot also match an unkeyed open-generic registration; a selected factory must return a non-null instance.
- A later shell core or feature registration retains ordinary registration precedence; sharing does not force the root instance to win single-service resolution.
- During reload, old and new shell generations may overlap; both continue to use the host-owned shared singleton until the host is disposed.

## Requirements

### Functional Requirements

- **FR-001**: The host MUST be able to opt in a service type for singleton sharing with shell providers.
- **FR-002**: Sharing MUST apply to the complete set of matching unkeyed registrations for the selected service type.
- **FR-003**: Each shell MUST observe shared registration instances in the same order as the root host's unkeyed registrations.
- **FR-004**: Keyed registrations MUST retain their existing registration and lifetime behavior.
- **FR-005**: Shared instances MUST remain owned by the root host; shell disposal MUST NOT dispose them.
- **FR-006**: Root-host disposal MUST dispose host-created shared singleton instances according to their synchronous or asynchronous disposal contract.
- **FR-007**: Existing behavior for service types not selected for sharing MUST remain unchanged.
- **FR-008**: Aliased service types MUST be selected independently.
- **FR-009**: Invalid requests, including missing registrations, non-singleton unkeyed descriptors, open generic types, and root-only exclusions, MUST fail with an actionable diagnostic.
- **FR-010**: A feature-specific later registration MUST retain existing shell override behavior.
- **FR-011**: The system MUST reject selected service types whose unkeyed `IEnumerable<TService>` resolution is overridden by an exact or open-generic enumerable registration.
- **FR-012**: Repeated `AddCShells` calls before root provider construction MUST reuse the same builder state and MUST NOT duplicate CShells infrastructure registrations.

## Key Entities

- **Shared service selection**: A host configuration choice identifying one closed service type whose unkeyed singleton registrations are available in shell providers.
- **Host singleton registration**: One unkeyed service registration and the singleton instance created or supplied for it by the host container.
- **Shell generation**: One active or retiring shell service provider that may resolve a shared host singleton.

## Success Criteria

### Measurable Outcomes

- **SC-001**: For every valid selected registration, all shell generations resolve the same instance as the root host.
- **SC-002**: Shell disposal causes zero disposal calls on shared instances, and root disposal causes exactly one disposal call per host-created singleton registration.
- **SC-003**: All invalid selections fail before shell service-provider construction completes and include an actionable diagnostic.
- **SC-004**: Existing tests for unselected singleton copying and feature registration precedence continue to pass.
