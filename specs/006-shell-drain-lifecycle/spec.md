# Feature Specification: Shell Generations, Reload & Disposal Lifecycle

**Feature Branch**: `006-shell-drain-lifecycle`
**Created**: 2026-04-22
**Status**: Draft (revised 2026-04-22 — clean overhaul)
**Input**: User description: "CShell Shell Draining and Disposal Lifecycle — with automatic generation bumping on reload, delivered as a clean overhaul replacing the legacy hosting / management / settings-provider surface."

## Overview

A shell is a named service container composed from a **blueprint** — the set of enabled
features plus feature/shell configuration, expressed either through the fluent `ShellBuilder`
API or loaded from configuration (e.g. `Shells/*.json`). The host registers one blueprint per
shell name. The library owns the lifecycle: first activation produces **generation 1**, and
each subsequent `ReloadAsync` re-composes the blueprint and produces the next generation
(2, 3, …), promoting the new generation to `Active` and draining the previous one
cooperatively in the background. Host code never authors or tracks generation numbers.

The lifecycle states (`Initializing → Active → Deactivating → Draining → Drained → Disposed`),
activation hooks, drain handlers, drain policies, request-scope tracking, and observable
state-change events are orthogonal concerns that apply to every generation uniformly.

**This feature is a clean overhaul.** The legacy `IShellHost` / `IShellManager` /
`ShellContext` / `IShellSettingsProvider` / `IShellActivatedHandler` /
`IShellDeactivatingHandler` surface is removed entirely and replaced by `IShellRegistry` +
`IShell` + `IShellBlueprint` + `IShellInitializer` + `IDrainHandler`. Downstream integrations
(`CShells.AspNetCore`, `CShells.FastEndpoints`) are migrated in-place; there is no parallel
legacy path left to maintain.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Activate a shell from a registered blueprint (Priority: P1)

A host developer registers a shell blueprint at startup — for example,
`builder.Services.AddCShells(c => c.AddShell("payments", s => s.WithFeature<PaymentsFeature>().WithConfiguration("stripe:key", ...)))`
— and the library activates it as generation 1 at host startup without the developer
specifying a version.

**Why this priority**: This is the entry point for every other story. Without blueprint-driven
activation, the reload flow has nothing to reload.

**Independent Test**: Register a blueprint for name `payments`, start the host, assert
`registry.GetActive("payments")` returns a shell whose `Descriptor.Generation == 1` and whose
`State == Active`.

**Acceptance Scenarios**:

1. **Given** a host with a blueprint registered for `payments`, **When** the host starts,
   **Then** the registry contains exactly one shell for `payments` with `Generation == 1` and
   state `Active`.
2. **Given** two blueprints registered for different names, **When** the host starts, **Then**
   both shells are activated independently as their respective generation 1.
3. **Given** a blueprint that throws during composition, **When** host startup runs, **Then**
   the exception propagates, host startup fails, no shell is registered for that name, and
   other shells are unaffected.

---

### User Story 2 - Reload a shell to pick up configuration changes (Priority: P1)

A host developer updates a shell's blueprint — adding a feature, changing a setting, or editing
the backing `Shells/payments.json` — and calls `ReloadAsync("payments")`. The library
automatically composes a fresh `ShellSettings` from the blueprint, builds the next generation,
promotes it, and drains the previous generation.

**Why this priority**: This is the headline UX of the feature. Reloading must be a single call
that the host never has to annotate with a version or manually orchestrate.

**Independent Test**: Register a blueprint whose features can be toggled via a mutable source;
activate (gen 1); mutate the source to add a feature; call `ReloadAsync("payments")`; assert
`GetActive("payments").Descriptor.Generation == 2`, the new shell resolves the newly-added
feature's services, and the previous generation is in `Draining` or `Drained`.

**Acceptance Scenarios**:

1. **Given** an active generation N of a shell, **When** `ReloadAsync(name)` is called,
   **Then** the registry composes a fresh `ShellSettings` from the blueprint, creates
   generation N+1, promotes it to `Active`, and initiates drain on generation N — all in a
   single call.
2. **Given** a shell whose blueprint now enables an additional feature, **When** reload
   completes, **Then** the new generation's service provider resolves services contributed by
   the new feature, and the old generation's provider is unchanged.
3. **Given** an in-flight reload, **When** a second concurrent `ReloadAsync(name)` is called,
   **Then** the second call is serialized behind the first; both succeed in arrival order and
   the last-completing reload's shell becomes Active.
4. **Given** a blueprint that throws during composition on reload, **When** reload runs,
   **Then** the exception propagates to the caller, the current active generation is
   unaffected, and no partial generation is registered.
5. **Given** a registry with no blueprint for a name, **When** `ReloadAsync(name)` is called,
   **Then** it throws a descriptive exception indicating no blueprint is registered.

---

### User Story 3 - Reload every shell in one call (Priority: P2)

An operator wants to pick up a broad configuration change across many shells (for example, a
shared logging setting change) without iterating manually. Calling `ReloadAllAsync` triggers a
reload on every registered blueprint.

**Why this priority**: Reduces operator burden and avoids drift when multiple shells need to
roll over together.

**Independent Test**: Register three blueprints and activate; call `ReloadAllAsync()`; assert
all three names now have a new generation as their active shell, with each previous generation
draining independently.

**Acceptance Scenarios**:

1. **Given** N registered blueprints, each with an active generation, **When**
   `ReloadAllAsync()` is called, **Then** each shell is reloaded to its next generation and the
   returned collection contains one `ReloadResult` per name.
2. **Given** one blueprint among many throws during composition, **When** `ReloadAllAsync()`
   runs, **Then** the other shells still reload; the result indicates which names failed and
   carries the underlying exception.
3. **Given** a shell with no active generation (never activated), **When** `ReloadAllAsync()`
   runs, **Then** that shell is activated as generation 1 (no prior generation to drain).

---

### User Story 4 - Register initializers to run once when a shell activates (Priority: P2)

A host developer contributes one or more initializers on a shell's service collection (via a
feature's `ConfigureServices`). When that shell transitions from `Initializing` to `Active`,
the library resolves and runs every initializer so the host can warm caches, register
schedules, or perform one-time per-shell setup before the shell serves requests.

**Why this priority**: Initializers replace the legacy `IShellActivatedHandler` pattern;
without them, hosts would need to wire one-shot setup work through ad-hoc constructors or
hosted services. Keeping the hook preserves a clean activation seam.

**Independent Test**: Register a feature that contributes an `IShellInitializer` that records
its invocation. Activate; assert the initializer ran exactly once, before the shell became
`Active`, and before any drain handler for the same shell could possibly run.

**Acceptance Scenarios**:

1. **Given** a shell whose feature registers one or more `IShellInitializer` services, **When**
   the shell is activated, **Then** every initializer's `InitializeAsync` is awaited once, in
   deterministic DI-registration order, before the shell transitions to `Active`.
2. **Given** an initializer that throws, **When** activation runs, **Then** the exception
   propagates, the shell never reaches `Active`, its service provider is disposed, and no
   partial shell entry is retained.
3. **Given** a shell with no initializers, **When** activation runs, **Then** activation
   completes immediately with no delay.

---

### User Story 5 - Register drain handlers to complete in-flight work (Priority: P1)

A host developer registers one or more drain handlers on a shell's service collection (via a
feature's `ConfigureServices`). When that shell enters `Draining`, the handlers are invoked so
the host can finish in-flight work before the service provider is disposed.

**Why this priority**: Without drain handlers, the generation-rollover flow above cannot be
cooperative — the point of the feature.

**Independent Test**: Register a feature that contributes an `IDrainHandler` that records when
it was called and awaits a short delay. Activate, then reload. Assert the handler ran on the
old generation and was given a cancellation token.

**Acceptance Scenarios**:

1. **Given** a shell with a registered drain handler, **When** drain is initiated (via reload
   or direct `DrainAsync`), **Then** the handler's `DrainAsync` method is invoked before the
   shell transitions to `Drained`.
2. **Given** multiple drain handlers registered on a shell, **When** drain is initiated,
   **Then** all handlers are invoked in parallel.
3. **Given** a drain handler that throws an exception, **When** drain completes, **Then** the
   drain result records the failure and the shell still transitions to `Drained`.
4. **Given** a drain handler, **When** the drain timeout elapses before the handler completes,
   **Then** the handler's cancellation token is cancelled and the shell transitions to
   `Drained`.

---

### User Story 6 - In-flight request scopes complete before the old generation disposes (Priority: P1)

A host serving requests out of a shell triggers a reload. In-flight request scopes against the
previous generation must finish cleanly — without the old service provider being disposed out
from under them — before drain handlers run and the provider is released.

**Why this priority**: Without this guarantee, any web host using shells will see
`ObjectDisposedException` on every reload under load. This is the operational reason cooperative
drain exists; it must be built-in, not opt-in.

**Independent Test**: Activate a shell, acquire one or more `IShellScope` handles from it, and
trigger drain. Assert that `Drained` is not reached until every scope handle has been disposed
(or the drain deadline has elapsed), and that services resolved through the scopes' providers
continue to work during drain.

**Acceptance Scenarios**:

1. **Given** a shell with one or more active `IShellScope` handles outstanding, **When**
   drain is initiated, **Then** the shell transitions to `Draining`, handler invocation is
   deferred, and the shell waits for every outstanding scope handle to be disposed before
   running registered `IDrainHandler` services.
2. **Given** all outstanding scope handles are disposed, **When** the scope wait completes,
   **Then** the drain proceeds to invoke `IDrainHandler` services within the remaining
   deadline budget.
3. **Given** outstanding scope handles that are not released before the drain deadline,
   **When** the deadline elapses, **Then** the handles are abandoned, drain handlers still
   run under the cancelled token, and the shell transitions to `Drained` within the
   configured grace period.
4. **Given** a shell with no outstanding scopes, **When** drain is initiated, **Then** the
   scope-wait phase completes immediately with no delay.

---

### User Story 7 - Await drain completion and inspect results (Priority: P2)

A host developer who triggers a reload or a direct drain wants to await its completion and
inspect which handlers completed, which timed out, and how long each took — for structured logs
or operator dashboards.

**Why this priority**: Observability into drain outcomes is necessary in production.

**Independent Test**: Reload a shell whose old generation has multiple handlers; await the
returned drain operation; assert the result contains one entry per handler with the correct
status and elapsed time.

**Acceptance Scenarios**:

1. **Given** a drain in progress, **When** the drain completion is awaited, **Then** it
   resolves only after all handlers have completed or the timeout has elapsed.
2. **Given** a completed drain, **When** the result is inspected, **Then** it contains one
   entry per registered handler, each with a completed flag, optional error, and elapsed
   duration.
3. **Given** a drain in progress, **When** force-complete is called, **Then** all handler
   cancellation tokens are cancelled, the shell transitions to `Drained` within the grace
   period, and the result status is `Forced`.

---

### User Story 8 - Observe shell state transitions (Priority: P2)

A host application developer needs to know when a shell transitions between lifecycle states so
they can react (e.g., pause intake before triggering a drain, emit metrics on generation
rollover).

**Why this priority**: Observability is table stakes for production but not required to exercise
the core reload flow.

**Independent Test**: Subscribe an `IShellLifecycleSubscriber` to the registry; activate then
reload a shell; assert the subscriber sees the full transition sequence on both generations in
order with correct old/new state values and descriptor metadata (including generation number).

**Acceptance Scenarios**:

1. **Given** a shell in `Active` state, **When** reload is initiated, **Then** the old
   generation transitions through `Deactivating → Draining → Drained → Disposed` in order, each
   transition firing a state-changed event.
2. **Given** a global subscriber registered on the registry, **When** any shell transitions to
   a new state, **Then** the subscriber is invoked with the shell, old state, and new state.
3. **Given** a shell that has reached `Disposed`, **When** any further state transition is
   attempted, **Then** no transition occurs and no event fires.

---

### User Story 9 - Configure drain timeout policy (Priority: P3)

A host developer wants to control how long a drain waits for handlers to complete, and whether
extension requests from handlers are honoured. They can set a fixed timeout, an extensible cap,
or an unbounded wait for dev/test purposes.

**Why this priority**: Different environments require different timeout behaviour.

**Independent Test**: Configure `FixedTimeoutDrainPolicy(1s)`, register a handler that waits
indefinitely, reload, assert the drain completes after approximately 1 second with a
`TimedOut` status.

**Acceptance Scenarios**:

1. **Given** a fixed-timeout policy with a 5-second limit, **When** handlers run longer than
   5 seconds, **Then** drain is forced after 5 seconds and the result status is `TimedOut`.
2. **Given** an extensible-timeout policy, **When** a handler requests an extension, **Then**
   the deadline is extended up to the configured cap.
3. **Given** an unbounded policy, **When** drain starts, **Then** a warning is logged and drain
   waits indefinitely until all handlers complete.

---

### Edge Cases

- What happens when `ReloadAsync` is called on a name with no registered blueprint? Throws a
  descriptive exception indicating the blueprint is missing; the registry is unchanged.
- What happens when a blueprint throws during composition on reload? The exception propagates
  to the caller; the current active generation is unaffected; no partial generation is
  registered.
- What happens when an initializer throws during activation? The exception propagates; the
  shell never reaches `Active`; its service provider is disposed; no partial generation is
  retained.
- What happens when two concurrent `ReloadAsync` calls target the same name? Serialized; both
  succeed in arrival order; the last one to complete becomes the active generation. Generation
  numbers are assigned monotonically in the order reloads are serialized.
- What happens when `ReloadAllAsync` encounters a failing blueprint for one name? The other
  shells still reload; the result carries per-name outcomes, with the failing name surfacing
  the composition exception.
- What happens when reload is called on a name whose blueprint's composed settings are
  identical to the previous generation's? A new generation is still created. Skipping
  identical reloads is out of scope; hosts can decide externally whether to call reload.
- What happens when a drain handler resolves services from the shell being drained? It
  succeeds, because disposal only occurs after drain completes.
- What happens when no drain handlers are registered on the outgoing generation? After the
  scope-wait phase completes, drain transitions `Draining → Drained` with no further delay.
- What happens when `IShellScope` handles are still outstanding at drain start? The scope-wait
  phase holds the drain open until every handle is disposed or the drain deadline elapses,
  whichever comes first. Handler invocation does not begin until this phase ends.
- What happens when `ActivateAsync` is called on a name that already has an `Active`
  generation? Throws `InvalidOperationException`; callers use `ReloadAsync` to roll over.
- What happens when a blueprint is registered a second time for a name that already has one?
  Throws a descriptive exception indicating the name is already registered; duplicate
  registration is a programming error.
- What happens when host shutdown exceeds `HostOptions.ShutdownTimeout` while shells are
  still draining? The registry emergency-disposes the still-draining shells (transitioning
  them directly to `Disposed`) so the host can actually stop. This is the only path that
  bypasses `Drained`. Outstanding scopes at this point are abandoned; any service resolution
  they attempt afterwards may throw `ObjectDisposedException`.
- What happens when the host application shuts down? The registry drains every active shell in
  parallel using the default drain policy, then disposes them.

---

## Requirements *(mandatory)*

### Functional Requirements

**Blueprints & registration**

- **FR-001**: The library MUST allow a host to register exactly one **shell blueprint** per
  shell name. A blueprint is the source-of-truth for how to compose a shell's `ShellSettings`
  (enabled features + feature/shell configuration) and MUST be re-composable on demand.
- **FR-002**: Blueprints MUST be registrable via the fluent builder API
  (`AddShell(name, builder => ...)` on the `CShellsBuilder`) and via configuration sources
  (e.g. `Shells/*.json` files bound through the existing configuration pipeline).
- **FR-003**: Registering a second blueprint for an already-registered name MUST throw a
  descriptive exception. Duplicate registration is a programming error.

**Identity & generations**

- **FR-004**: Every shell MUST have a `ShellDescriptor` carrying its name, generation number,
  creation timestamp, and an opaque metadata dictionary.
- **FR-005**: Generation numbers are library-owned and follow these semantics:
  - Monotonically increasing `int` starting at 1 for the first activation of a name;
  - Assigned in the serialization order of reloads for that name (FR-013);
  - Never reused within the process lifetime, even when a generation fails to build or
    is disposed;
  - Independent per name — generations for different names MUST NOT share or interfere
    with each other's counters.
- **FR-007**: Host code MUST NOT be required to supply or inspect generation numbers to
  perform any lifecycle operation. Generation numbers are observable for diagnostics only.
- **FR-008**: `ShellId` MUST remain a name-only value type. Generation is exposed exclusively
  on `ShellDescriptor.Generation`, not as part of `ShellId`. `ShellId` equality is
  case-insensitive on `Name`.

**Activation, initialization & reload**

- **FR-009**: The registry MUST expose `ActivateAsync(name)` that composes `ShellSettings`
  from the registered blueprint, builds the shell's service provider, registers the shell as
  generation 1 in `Initializing`, runs all registered `IShellInitializer` services from the
  shell's provider, and promotes it to `Active`. Calling `ActivateAsync` on a name whose
  blueprint already has an `Active` generation MUST throw `InvalidOperationException`
  indicating that the caller should use `ReloadAsync` to roll over. The built-in startup
  hosted service (FR-035) invokes `ActivateAsync` at host start; hosts that register
  additional blueprints after startup invoke it themselves.
- **FR-010**: The registry MUST expose `ReloadAsync(name)` that composes a fresh
  `ShellSettings` from the registered blueprint, builds the next generation, runs its
  initializers, promotes it to `Active`, and initiates cooperative drain on the previously
  active generation — all in a single call.
- **FR-011**: `ReloadAsync` called on a name with no currently active generation MUST behave
  equivalently to `ActivateAsync` (activate generation 1; no prior generation to drain).
- **FR-012**: The registry MUST expose `ReloadAllAsync()` that reloads every registered
  blueprint and returns a per-name result indicating success (with drain operation, if any)
  or failure (with the composition exception).
- **FR-013**: Concurrent `ReloadAsync` calls for the same name MUST be serialized. Generation
  numbers MUST be assigned in serialization order; both calls MUST succeed, and the last one
  to complete becomes the active generation.
- **FR-014**: If blueprint composition, provider construction, or initializer invocation
  throws during `ActivateAsync` / `ReloadAsync`, the exception MUST propagate, the current
  active generation (if any) MUST remain unaffected, the partial shell's provider MUST be
  disposed, and no partial shell entry MUST be retained in the registry.

**Initializers**

- **FR-015**: Host applications MUST be able to register per-shell initializers by
  contributing `IShellInitializer` services through a feature's `ConfigureServices`.
- **FR-016**: All registered `IShellInitializer` services on a shell MUST be invoked
  sequentially in DI-registration order, inside the shell's transition from `Initializing`
  to `Active`, and MUST complete before the shell is observable as `Active`. "Registration
  order" is the order in which features' `ConfigureServices` calls add each initializer
  via `AddTransient<IShellInitializer, …>`. Features MUST NOT use `IServiceCollection.Replace`
  on `IShellInitializer` registrations — the registry treats initializer registration as
  additive only; the initializer set is the union across features in activation order.

**Lifecycle states & transitions**

- **FR-017**: The library MUST define the shell lifecycle states: `Initializing`, `Active`,
  `Deactivating`, `Draining`, `Drained`, `Disposed`.
- **FR-018**: Shell state transitions MUST be monotonic; a shell MUST NOT move backward
  through states.
- **FR-019**: Consumers MUST be able to subscribe to global shell lifecycle events via the
  registry, receiving every state transition for every shell regardless of generation.

**Scope tracking**

- **FR-020**: `IShell` MUST expose a `BeginScope` method that returns an `IShellScope`
  handle. The handle MUST carry an `IServiceProvider` (a DI scope built from the shell's
  provider), reference the owning `IShell`, and be `IAsyncDisposable`.
- **FR-021**: Each outstanding `IShellScope` MUST increment an active-scope counter on the
  shell; disposal MUST decrement it. Counter operations MUST be thread-safe.
- **FR-022**: When drain begins on a shell, the drain operation MUST first await the
  active-scope counter reaching zero before invoking any registered `IDrainHandler`. This
  scope-wait phase MUST observe the overall drain deadline; if the deadline elapses first,
  outstanding scopes are abandoned (not forcibly disposed) and drain proceeds to handler
  invocation with the cancelled token.

**Drain handlers & operations**

- **FR-023**: Host applications MUST be able to register drain handlers by contributing
  `IDrainHandler` services through a feature's `ConfigureServices`.
- **FR-024**: All registered drain handlers on a shell MUST be invoked in parallel after the
  scope-wait phase completes.
- **FR-025**: Each drain handler MUST receive a cancellation token that is cancelled when the
  drain deadline is reached or drain is forced.
- **FR-026**: Drain handlers MUST be able to request a deadline extension via a handle; the
  configured drain policy MUST decide whether to grant it.
- **FR-027**: The library MUST support configurable drain timeout policies, with
  `FixedTimeoutDrainPolicy(30s)` as the default.
- **FR-028**: Concurrent drain calls for the same shell MUST return the same in-flight
  operation (idempotent).
- **FR-029**: Callers MUST be able to await drain completion and receive a structured
  `DrainResult` including overall status, scope-wait elapsed time, the number of scope
  handles still outstanding at the end of phase 1 (abandoned by deadline, if any), and
  per-handler status, error, and elapsed time.
- **FR-030**: Callers MUST be able to force-complete a drain at any time, cancelling
  outstanding handler tokens and transitioning the shell to `Drained` within the configured
  grace period (default 3 seconds).

**Queries & observability**

- **FR-031**: The registry MUST provide a way to retrieve the single `Active` shell for a
  given name (`GetActive(name)`).
- **FR-032**: The registry MUST provide a way to retrieve all generations currently *retained*
  for a given name (`GetAll(name)`): the active generation and any earlier generations still
  draining. A generation is released once it reaches `Disposed` (its teardown complete), so
  fully torn-down generations are NOT returned and callers MUST NOT depend on historical
  generations remaining available after teardown. The returned set MUST always contain the shell
  returned by `GetActive(name)` — a disposed active generation is released from `GetActive` and
  `GetAll` together.
- **FR-033**: Shell descriptor metadata MUST be opaque to the library and surfaced unchanged
  in events and queries. Metadata on every generation's descriptor MUST be sourced from the
  blueprint's `Metadata` property at generation time.
- **FR-034**: The library MUST automatically register a structured-logging subscriber backed
  by `ILogger` when CShells is added to the host's service collection. This subscriber MUST
  emit a structured log entry for every shell lifecycle transition, including shell
  descriptor metadata (name + generation). No host configuration is required to activate it.
- **FR-034a**: The structured log schema emitted by the default subscriber MUST include, at
  minimum, the following properties on every entry: `ShellName` (string), `Generation`
  (int), `PreviousState` (string; null for the initial transition into `Initializing`),
  `CurrentState` (string). Drain-completion entries MUST additionally include
  `ScopeWaitElapsedMs` (long) and `HandlerCount` (int). Routine state transitions MUST be
  logged at `LogLevel.Information`; drain timeouts, forced drains, and abandoned scopes
  MUST be logged at `LogLevel.Warning`. Event IDs MUST be stable across releases; the range
  `1000–1099` is reserved for shell lifecycle events (1000–1009 for transitions, 1010–1019
  for drain warnings).

**Startup & teardown**

- **FR-035**: On host startup, the library MUST automatically activate every registered
  blueprint in parallel. Any blueprint whose composition, provider build, or initializers
  throw MUST cause host startup to fail and propagate the exception.
- **FR-036**: On host shutdown, the library MUST drain every active shell in parallel using
  the configured drain policy, then dispose each shell's service provider. If a drain
  exceeds the host's shutdown timeout (the duration bounded by
  `IHostApplicationLifetime.ApplicationStopping` and `HostOptions.ShutdownTimeout`),
  providers MUST be disposed regardless so shutdown completes. This "emergency disposal"
  is the only code path permitted to transition a shell to `Disposed` without first
  reaching `Drained`.
- **FR-037**: Shell disposal MUST be entirely owned by `IShellRegistry`. `IShell` MUST NOT
  implement `IAsyncDisposable` or `IDisposable` on its public surface; hosts observe
  disposal via the `Drained → Disposed` transition event. Together with FR-036 this
  preserves Principle VII's disposal-ordering rule: a provider is never disposed while
  services resolved from it are in active use, except under the bounded emergency-shutdown
  path of FR-036.

**Scope of replacement**

- **FR-038**: The following legacy types MUST be removed entirely and have no public-surface
  replacement beyond the new abstractions: `IShellHost`, `DefaultShellHost`, `IShellManager`,
  `DefaultShellManager`, `ShellContext`, `IShellContextScope`, `IShellContextScopeFactory`,
  `DefaultShellContextScopeFactory`, `ShellContextScopeHandle`, `ShellCandidateBuildResult`,
  `IShellHostInitializer`, `IShellRuntimeStateAccessor`, `ShellRuntimeRecord`,
  `ShellRuntimeStateStore`, `ShellReconciliationOutcome`, `ShellRuntimeStatus`,
  `IShellActivatedHandler`, `IShellDeactivatingHandler`, `ShellHandlerOrderAttribute`,
  `IShellSettingsProvider`, `CompositeShellSettingsProvider`,
  `ConfigurationShellSettingsProvider`, `ConfiguringShellSettingsProvider`,
  `InMemoryShellSettingsProvider`, `MutableInMemoryShellSettingsProvider`,
  `IShellSettingsCache`, `ShellSettingsCache`, `ShellSettingsCacheInitializer`,
  `ShellFeatureInitializationHostedService`, `ShellStartupHostedService`,
  `ShellSettingsFactory`.
- **FR-039**: All downstream integrations (`CShells.AspNetCore`, `CShells.FastEndpoints`,
  `CShells.AspNetCore.Testing`, `CShells.Providers.FluentStorage`, sample and test projects)
  MUST be migrated in-place to the new `IShellRegistry` / `IShell` surface. No legacy types
  MAY remain referenced in any project after this feature ships.

### Key Entities

- **Shell Blueprint**: The registered source-of-truth for how to compose a shell of a given
  name. Backed by fluent builder code or by configuration; re-invocable on every reload to
  produce fresh `ShellSettings`. Holds no runtime state. Carries optional static metadata
  that flows onto every generation's descriptor.
- **ShellSettings**: Existing type — the composed definition of a shell (name + enabled
  features + feature/shell configuration data + code-first feature configurators). Produced
  by a blueprint; consumed by the registry to build a generation. Generation-unaware.
- **Shell**: A named, generation-stamped service container with an identity, lifecycle state,
  built service provider, and active-scope counter. Terminal when `Disposed`. Disposal is
  registry-owned; hosts never dispose shells directly (FR-037). Multiple generations for the
  same name may coexist (exactly one `Active` + any number `Deactivating`/`Draining`/`Drained`).
- **Shell Descriptor**: Immutable identity and metadata snapshot created at shell creation
  time; includes name, generation, creation timestamp, and opaque metadata.
- **ShellId**: Value type of `Name`. Used wherever a shell is referenced by name. Never
  carries generation.
- **Shell Scope**: An `IAsyncDisposable` handle wrapping a DI scope built from the shell's
  provider. Outstanding scopes delay drain handler invocation until released or abandoned at
  deadline.
- **Shell Initializer**: Per-shell service resolved from the shell's provider during
  activation. Runs setup work before the shell transitions to `Active`.
- **Shell Registry**: The authoritative collection of all shells and their blueprints;
  provides `ActivateAsync`, `ReloadAsync`, `ReloadAllAsync`, `DrainAsync`, enumeration, and
  global event subscription.
- **Drain Operation**: A handle representing an in-progress or completed drain; exposes
  status, deadline, progress, a completion awaitable, and force capability.
- **Drain Handler**: A host-registered callback resolved from the draining shell's service
  provider; performs graceful-shutdown work and returns when done.
- **Drain Policy**: A strategy that governs the initial timeout, extension decisions, and
  deadline-breach behaviour for a drain operation.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A host application can register a blueprint for a shell name, activate, and
  then call `ReloadAsync(name)` without ever supplying, inspecting, or comparing a generation
  number; each reload produces a monotonically higher generation stamped automatically on the
  descriptor.
- **SC-002**: A host can update a blueprint's feature set (e.g., add a feature) and call
  `ReloadAsync(name)` to end up with an `Active` shell whose service provider resolves
  services contributed by the newly-enabled feature, while the previous generation continues
  to serve in-flight request scopes until they release and drain handlers complete.
- **SC-003**: `ReloadAllAsync()` reloads every registered blueprint and returns a per-name
  result that distinguishes success from composition failure on individual names without
  aborting the batch.
- **SC-004**: Drain handler registration, invocation, and result collection work correctly
  for shells with 0 to 50 registered handlers without special configuration.
- **SC-005**: Concurrent `ReloadAsync` calls for the same name always complete in arrival
  order; generation numbers are strictly monotonic across serialized reloads; no duplicate
  generation numbers are ever assigned.
- **SC-006**: Concurrent drain calls for the same shell always return the same operation
  handle; no duplicate drains are ever started.
- **SC-007**: Configuring a drain timeout of T seconds results in drain completing within
  T + G seconds, where G is the configurable cancellation grace period (default 3 seconds),
  under all built-in policy types. After force or timeout, the shell MUST reach `Drained`
  within G seconds regardless of handler behaviour.
- **SC-008**: All shell lifecycle transitions are observable via structured events carrying
  the shell descriptor (including generation number), enabling downstream logging and
  diagnostics without polling. The library emits these as structured log entries by default
  without any host configuration.
- **SC-009**: Shells with no registered drain handlers and no outstanding scopes complete
  drain immediately and transition to `Drained` without delay, error, or special
  configuration.
- **SC-010**: Under a reload triggered while a web request is executing inside the outgoing
  generation, the request completes normally (no `ObjectDisposedException`), and the old
  generation's provider is not disposed until every active scope handle has been released
  or the drain deadline has elapsed.
- **SC-011**: After this feature ships, no project in the repository — library, sample, or
  test — references any of the legacy types listed in FR-038. `git grep` for any of those
  names returns zero hits outside of historical git history.

---

## Clarifications

### Session 2026-04-22 (initial)

- Q: What happens when `CreateAsync` is called with a `ShellId` that already exists in the
  registry? → A: (superseded — `CreateAsync` is no longer part of the public surface; see
  revised clarifications below.)
- Q: What happens when `CreateAsync` fails during shell construction? → A: Exception
  propagates to caller; shell is never added to the registry. (Preserved as FR-014.)
- Q: Does the library ship a default structured-logging subscriber or does the host wire its
  own? → A: Library registers a default `ILogger`-backed subscriber automatically; no host
  configuration required. (Preserved as FR-034.)
- Q: What is the maximum time allowed between force/timeout and the shell reaching Drained?
  → A: Configurable grace period, default 3 seconds. (Preserved as SC-007.)

### Session 2026-04-22 (revision)

- Q: Should the unit of shell identity be a `(name, version)` pair where the host authors the
  version, or a `(name, generation)` pair where the library owns the generation? → A:
  `(name, generation)`. The library assigns generations monotonically; hosts never author
  them. Rationale: the runtime semantics are successor-based (each reload supersedes the
  prior), and matching Kubernetes / GC `generation` convention keeps the meaning unambiguous.
- Q: How does the host tell the registry what to rebuild on reload? → A: Via a **blueprint**
  registered once at startup. The blueprint is re-invoked on each activation / reload to
  produce a fresh `ShellSettings`; the host updates the underlying source (fluent code,
  config file) between reloads.
- Q: Is reload conditional on detected blueprint changes, or unconditional? → A:
  Unconditional. Each `ReloadAsync` call produces a new generation regardless of whether the
  composed `ShellSettings` differs from the prior generation. Change detection (file watching,
  `IOptionsMonitor`) is out of scope; the host decides when to call reload.
- Q: Are `CreateAsync` / `PromoteAsync` / `ReplaceAsync` still part of the public surface?
  → A: No. They are replaced by blueprint registration + `ActivateAsync` / `ReloadAsync` /
  `ReloadAllAsync`. A host-facing `DrainAsync` remains for explicit cooperative drains (e.g.,
  on shutdown), and promote logic runs internally as part of activate / reload.
- Q: What happens to the previous generation's drain operation if `ReloadAllAsync` is called
  while a reload is still draining a prior generation? → A: The draining generation is
  unaffected; it continues to drain on its own schedule. The fresh reload produces yet
  another generation, which will similarly drain when superseded. Multiple generations of
  the same name can therefore be simultaneously `Active` (one), `Draining`, and `Drained`.

### Session 2026-04-22 (architecture overhaul)

- Q: Should `ShellId` carry the generation number, forcing a breaking change to the identity
  type, or should generation live exclusively on `ShellDescriptor`? → A: Generation lives on
  `ShellDescriptor` only. `ShellId` stays name-only. The `(Name, Generation)` pair is never
  needed as an equatable key — registry lookups are by name, per-generation state lives on
  the `IShell` reference, and diagnostics format as `Name#Generation`. This removes the
  breaking change, the sentinel-generation compat hack, and the identity ordering puzzle in
  `ShellSettings`.
- Q: Does the new lifecycle API coexist with the legacy `IShellHost` / `IShellManager` /
  `ShellContext` surface, or replaces it entirely? → A: Replaces entirely. All legacy types
  are deleted (FR-038), and every downstream integration is migrated in-place (FR-039).
  Coexistence would double the surface area and invite confusion.
- Q: What replaces `IShellActivatedHandler` / `IShellDeactivatingHandler`? → A:
  `IShellInitializer` (runs during `Initializing → Active`) and `IDrainHandler` (runs during
  `Draining`). Both are resolved from the shell's provider. `IShellInitializer` runs
  sequentially in DI-registration order; `IDrainHandler` runs in parallel. The legacy
  `ShellHandlerOrderAttribute` is not carried forward; ordering is via DI registration order
  for initializers and is irrelevant for drain handlers (which run concurrently).
- Q: How is the deferred-disposal semantic from `DefaultShellHost.AcquireContextScope`
  preserved in the new world? → A: `IShell.BeginScope()` returns an `IShellScope` that
  tracks an active-scope counter on the shell. Drain's **first phase** waits for the counter
  to reach zero (bounded by the drain deadline) before invoking `IDrainHandler` services.
  This is hard-coded as part of what "drain" means — hosts do not configure it and cannot
  remove it.
- Q: Is shutdown-drain behaviour opt-in or mandatory? → A: Mandatory. A built-in hosted
  service drains every active shell on host stop. If drain exceeds the host's shutdown
  timeout, providers are disposed anyway so the host actually stops (FR-036).

## Assumptions

- Blueprints are re-invocable — calling the blueprint's composition routine twice yields a
  fresh `ShellSettings` suitable for a new generation. Hosts are responsible for ensuring the
  routine is idempotent and side-effect-free with respect to any external state.
- Drain handlers that do not complete within the configured timeout are considered timed out;
  their `Completed` flag in the result is false.
- The cancellation grace period is configurable with a default of 3 seconds and is separate
  from the main drain timeout.
- Initializers and drain handlers are registered as transient services resolved at
  activation / drain time from the shell's provider.
- The registry holds shells until they are `Disposed`; callers are not responsible for
  generation lifecycle management beyond triggering reload and, on shutdown, letting the
  registry drain and dispose.
- The `UnboundedDrainPolicy` is intended for development and test environments only; using
  it in production should produce a log warning.
- Generation numbers are `int`; 2^31 reloads within a single process lifetime is not a
  practical concern. If it ever became one, the type could be widened without changing the
  observable behaviour.
- Hosts that need semantic "version" labels (for UI / telemetry / release tagging) can carry
  them in the blueprint's `Metadata`, which flows onto every generation's descriptor
  unchanged.
