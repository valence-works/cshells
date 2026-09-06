# Feature Specification: Map-Based Shell Configuration

**Feature Branch**: `011-map-shell-config`  
**Created**: 2026-05-07  
**Status**: Implemented (PR #99)  
**Input**: User description: "CShells: Map-Based Shell Configuration. Replace the current array under `CShells:Shells`, where each shell repeats a `Name` property, with map syntax where each child key is the shell name. Drop the shell `Name` property, do not support both formats, update documentation and examples, document environment variable override paths and shell naming conventions, and verify map loading, shell naming, environment overrides, and configuration merging across layers."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Configure Shells By Name (Priority: P1)

As a CShells configuration author, I want shells declared as named entries under `CShells:Shells` so each shell's identity is visible in the configuration path and does not need to be repeated inside the shell body.

**Why this priority**: This is the core format change. It makes shell configuration consistent with feature configuration and removes duplicate shell names from configuration files.

**Independent Test**: Can be fully tested by loading a configuration whose `CShells:Shells` value is a named object map and confirming each shell exists with the name from its map key and the expected configuration and features.

**Acceptance Scenarios**:

1. **Given** a configuration with `CShells:Shells:Default` and no inner `Name` value, **When** shell settings are loaded, **Then** the system creates a shell named `Default` with its configured routing and features.
2. **Given** a configuration with multiple named shell entries, **When** shell settings are loaded, **Then** each resulting shell uses its own map key as its name and keeps its own configuration and feature settings.
3. **Given** a shell entry that contains feature settings using the existing feature map syntax, **When** shell settings are loaded, **Then** the shell enables those features and preserves their configured values.

---

### User Story 2 - Override Shell Settings With Stable Paths (Priority: P2)

As an operator, I want environment variable overrides to include the shell name rather than an array index so overrides are self-documenting and remain stable when configuration files are reordered.

**Why this priority**: One of the main user-facing benefits is safer operational overrides. Named paths avoid brittle index-based overrides in deployment environments.

**Independent Test**: Can be fully tested by loading base configuration plus an environment override such as `CSHELLS__SHELLS__DEFAULT__FEATURES__IDENTITY__SIGNINGKEY=test` and confirming the override applies only to the `Default` shell's `Identity` feature.

**Acceptance Scenarios**:

1. **Given** a base configuration for shell `Default` with an `Identity` signing key, **When** an environment override targets `CSHELLS__SHELLS__DEFAULT__FEATURES__IDENTITY__SIGNINGKEY`, **Then** the `Default` shell receives the overridden signing key.
2. **Given** multiple shells with the same feature enabled, **When** an environment override targets one named shell, **Then** only that named shell's feature setting changes.
3. **Given** shell entries are reordered in a configuration file, **When** the same named environment override is applied, **Then** it still targets the same shell.

---

### User Story 3 - Merge Shell Configuration By Name (Priority: P3)

As an application maintainer, I want configuration layers to merge shell entries by shell name so app defaults, deployment configuration, and environment overrides combine predictably.

**Why this priority**: Named merging prevents accidental cross-shell configuration caused by array index differences across configuration layers.

**Independent Test**: Can be fully tested by loading layered configuration where each layer defines or overrides named shell entries in different orders and confirming the final shell set and settings are merged by shell name.

**Acceptance Scenarios**:

1. **Given** a base layer defines `Default` and a later layer overrides `Default:Configuration:Plan`, **When** configuration is loaded, **Then** the final `Default` shell contains the overridden plan and retains unaffected settings from the base layer.
2. **Given** a later layer adds a new named shell, **When** configuration is loaded, **Then** the final shell set contains both the original shells and the newly added shell.
3. **Given** two layers list shell entries in different orders, **When** configuration is loaded, **Then** shell settings are combined by shell name rather than by position.

---

### User Story 4 - Update Guidance And Samples (Priority: P4)

As a developer adopting CShells, I want documentation and sample configuration to show only the map-based shell format so new projects follow the supported shape from the start.

**Why this priority**: Documentation must match the only supported format; otherwise users may copy an obsolete array shape that no longer loads.

**Independent Test**: Can be fully tested by reviewing repository documentation and sample configuration and confirming all shell examples use named map entries without inner shell `Name` properties, including environment override guidance.

**Acceptance Scenarios**:

1. **Given** a README, docs page, or sample configuration that shows `CShells:Shells`, **When** it is reviewed, **Then** it uses map syntax with shell names as keys and no shell-level `Name` property.
2. **Given** environment variable guidance is documented, **When** a user reads it, **Then** it shows named paths such as `CSHELLS__SHELLS__DEFAULT__FEATURES__IDENTITY__SIGNINGKEY` and explains why named paths are stable.
3. **Given** shell naming guidance is documented, **When** a user defines a shell intended for environment overrides, **Then** they can choose a name that is clear and compatible with environment variable path conventions.

### Edge Cases

- Shell entries with blank or whitespace-only map keys are invalid and must fail before shell activation with a clear message.
- Shell entries that still rely on an inner shell `Name` value must not be accepted as an alternative source of shell identity.
- A shell map entry that contains an inner `Name` setting must not override the map key; shell identity comes only from the map key.
- Duplicate shell names after configuration provider normalization must resolve according to normal configuration precedence and must not produce two runtime shells with the same name.
- Layered configuration where a later source adds nested settings for an existing shell must merge into that named shell without removing unrelated shell settings.
- Environment variable shell-name casing may differ from display casing; matching must follow the configuration system's normal key matching behavior while preserving the configured shell name used by CShells.
- Empty `CShells:Shells` configuration is valid only if existing startup behavior already permits no configured shells; otherwise it must fail with the existing missing-shell behavior.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST accept `CShells:Shells` as a map whose child keys are shell names and whose values are shell definitions.
- **FR-002**: The system MUST assign each loaded shell's name from its `CShells:Shells` map key.
- **FR-003**: The system MUST no longer require or rely on a shell-level `Name` property inside each shell definition.
- **FR-004**: The system MUST reject the previous array-based `CShells:Shells` shape as unsupported.
- **FR-005**: The system MUST preserve existing shell definition content under each named shell, including `Configuration` values and feature map entries.
- **FR-006**: The system MUST ensure all shell-facing APIs, registries, and runtime descriptors expose the shell name populated from the map key.
- **FR-007**: The system MUST support named environment variable override paths for shell-specific feature settings, including `CSHELLS__SHELLS__DEFAULT__FEATURES__IDENTITY__SIGNINGKEY` targeting the `Default` shell's `Identity` feature signing key.
- **FR-008**: The system MUST merge layered shell configuration by shell name so later layers can override or extend a named shell without depending on shell order.
- **FR-009**: The system MUST fail before shell activation when a shell map key is blank or otherwise invalid for use as a shell name.
- **FR-010**: The system MUST provide actionable error feedback when unsupported array syntax is encountered, including the affected configuration path.
- **FR-011**: Documentation and sample configuration MUST use map syntax for all `CShells:Shells` examples and MUST remove shell-level `Name` properties from those examples.
- **FR-012**: Documentation MUST include environment variable override examples that demonstrate named shell paths and explain that named paths are stable across configuration reordering.
- **FR-013**: Documentation MUST define the shell naming convention for configuration keys: use PascalCase for display-oriented shell names, and use the same shell-name segment without adding underscores when writing environment variable paths, for example `MyShell` represented as `CSHELLS__SHELLS__MYSHELL__...`.

### Key Entities *(include if feature involves data)*

- **Shell Configuration Map**: The collection under `CShells:Shells` where each entry key is the shell name and each entry value is that shell's definition.
- **Shell Definition**: The per-shell configuration payload containing shell-specific configuration values and enabled feature definitions, identified by the parent map key rather than by an inner property.
- **Shell Name**: The stable identifier derived from a shell map key and exposed consistently through runtime shell settings, descriptors, registries, and management views.
- **Feature Configuration Entry**: A named feature entry within a shell definition, preserving the existing feature map behavior and feature-specific settings.
- **Configuration Layer**: A source of configuration values, such as base application settings, deployment-specific settings, or environment overrides, that contributes to the final named shell configuration.

### Assumptions

- Backward compatibility with the previous array-based shell format is intentionally out of scope.
- Feature configuration map syntax remains unchanged.
- Configuration key matching and environment variable normalization follow the host configuration system's existing behavior.
- `Default` is the canonical shell name used in examples unless a sample needs multiple shells.
- PascalCase is the preferred shell-name convention in configuration files because shell names are user-visible, while environment variable examples may use uppercase keys to match common deployment conventions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of map-format shell configuration tests load shells with names matching their `CShells:Shells` map keys.
- **SC-002**: 100% of tests for shell-specific feature settings confirm existing feature map settings remain available after the shell format change.
- **SC-003**: An environment override using `CSHELLS__SHELLS__DEFAULT__FEATURES__IDENTITY__SIGNINGKEY=test` changes only the `Default` shell's `Identity` signing key in the verified configuration.
- **SC-004**: Layered configuration tests demonstrate that at least three layers can override, extend, and add named shells without depending on array position.
- **SC-005**: 100% of unsupported array-format shell configuration tests fail before shell activation with an actionable error that names the `CShells:Shells` path.
- **SC-006**: Repository documentation and sample configuration contain no `CShells:Shells` array examples and no shell-level `Name` properties in shell examples.
