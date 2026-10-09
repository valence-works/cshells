# Data Model: Returned Generation Diagnostics

## Activation attempt

The existing `ShellActivationAttempt` represents one completed call made by the activation run.

| Field | Type | Meaning |
|---|---|---|
| `Outcome` | `ShellActivationAttemptOutcome` | Existing outcome after current-result validation. |
| `VerifiedGeneration` | `long?` | Existing built-in concrete-shell verification value; unchanged. |
| `ReturnedGeneration` | `long?` | New scalar descriptor generation from this call's returned shell, present only when the call succeeded and the runner accepted that return as current. |

The existing public constructor remains unchanged. The new property defaults to `null` when callers use that constructor and do not set the additive property.

## Target snapshot

The existing `ShellActivationAttemptState` represents the latest state for one target in one run.

| Field | Type | Meaning |
|---|---|---|
| `Status` | `ShellActivationTargetStatus` | Existing scheduling or terminal state. |
| `VerifiedGeneration` | `long?` | Existing verified built-in generation, including eligible external settlement. |
| `ReturnedGeneration` | `long?` | New historical value associated with a successful own activation return; it is absent for external-only satisfaction. |

The new value is a scalar, not a reference to an `IShell`, provider, or generation context. Its association is preserved when terminal state is copied into later immutable snapshots.

## Relationships and invariants

- `ReturnedGeneration` is derived from the exact shell object returned by the attempt's registry call, after that same object passes existing current-result validation.
- `ReturnedGeneration` does not imply built-in committed/settled verification. Custom registry success can have a returned value while `VerifiedGeneration` remains null.
- `VerifiedGeneration` does not imply an own successful return: external satisfaction alone can populate it while `ReturnedGeneration` remains null. If an already in-flight own attempt later succeeds, its returned generation is also recorded; external status and verified-generation remain unchanged.
- No terminal snapshot update may substitute a subsequently active generation for the generation originally returned.
