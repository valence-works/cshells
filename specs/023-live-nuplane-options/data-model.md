# Data Model: Apply Live Nuplane Integration Options

## Captured integration policy

An ephemeral value created once for an eligible, non-canceled package-reconciliation completion. It contains copies of:

| Value | Source | Lifetime and use |
|---|---|---|
| Enabled | Current `NuplaneIntegrationOptions.Enabled` | Determines whether the delivery requests observer work. Disabled delivery adds no new epochs and leaves previous epochs unchanged. |
| Refresh trigger | Current `NuplaneIntegrationOptions.RefreshTrigger` | Selects the existing refresh behavior. Undefined enum values fail before coordinator side effects. |
| Automatic reload | Current `NuplaneIntegrationOptions.AutoReload` | Determines whether eligible work requests/attempts shell promotion. When false, already-pending promotion remains pending. |
| Reload-results callback | Current `NuplaneIntegrationOptions.OnReloadResults` delegate reference | The same captured callback receives results for this delivery. A later replacement applies to a later delivery. |

The copy is made after null, cancellation, and empty-eligibility checks, and before epoch mutation, registry access, or awaits. Reading the current options once and copying each property does not promise atomicity against arbitrary concurrent mutation of the same options object during the copy.

## Existing coordinator state

The coordinator already tracks requested and committed catalog-freshness epochs and requested and committed reload epochs, including the catalog epoch associated with a reload request. Those counters, their synchronization, retry rules, and commit points remain unchanged. The captured policy is operation-local and is not retained as a second state machine.

## Relationships

- An eligible observer delivery may advance the existing catalog epoch according to its captured policy and package change set.
- A successful catalog refresh acknowledges only the freshness epoch captured by that refresh.
- Automatic reload is gated by the captured policy and current pending epochs; a successful reload acknowledges only work handled by that attempt.
- A shell-generation build participant consumes previously recorded catalog freshness without reading observer options or requesting activation/reporting.
- A settings change after one delivery captures its policy has no relationship to that in-flight value; it is observed by the next eligible delivery.
