# Returned-generation result contract

## Public members

The existing public types remain in `CShells.Hosting`:

- `ShellActivationAttempt` is the transient result passed to retry-policy and observer callbacks.
- `ShellActivationAttemptState` is the safe immutable snapshot returned for a run target.

Each type gains a nullable `long` `ReturnedGeneration` property with an additive init accessor. Existing constructors, including the full positional constructor of `ShellActivationAttemptState`, remain unchanged. The value is a scalar widened from the returned shell's `ShellDescriptor.Generation` (`int`); it retains no shell, provider, or context.

`VerifiedGeneration` keeps its existing meaning and type. Consumers must not treat the two properties as aliases:

| Result case | `ReturnedGeneration` | `VerifiedGeneration` |
|---|---:|---:|
| Built-in registry call returns the settled current shell | Returned descriptor generation | Existing verified generation |
| Custom registry call returns the current shell successfully | Returned descriptor generation | Null, as today |
| Call returns a shell that is no longer current | Null | Null for that attempt |
| Call throws or is canceled before successful return | Null | No new verification value |
| Target is satisfied externally, with no successful own return | Null | Existing externally verified generation |
| External satisfaction happens during an in-flight call that later returns current shell successfully | That successful call's returned descriptor generation | Existing external verified generation; external status remains terminal |

## Capture and snapshot rules

1. The implementation derives the value from the exact `IShell` returned by the attempt's `GetOrActivateAsync` call, after the existing current-result check accepts that shell.
2. No extra registry lookup is performed to populate the value; this avoids attributing a later generation to an earlier call.
3. `NotCurrent`, activation failure, and cancellation before successful return do not produce a value.
4. External reconciliation by itself never produces `ReturnedGeneration`. A later successful completion of an already in-flight owned call remains associated with that call, without changing the existing external status or verified-generation.
5. The attempt value is projected into the target snapshot by the existing attempt-recording path. The scalar associated with a completed successful call is historical and is not replaced by a later reload or active generation.
6. The existing `ShellActivationAttempt` constructor and the existing `ShellActivationAttemptState` constructor signatures are preserved for binary/source compatibility.

## Compatibility

- This is an additive API change; no existing property is renamed or reinterpreted.
- Callers that continue using current constructors observe `ReturnedGeneration == null` unless they explicitly initialize the additive property.
- Activation outcome, settlement rules, retry scheduling, external reconciliation, routing, and readiness are unchanged.
- Consumers that want a concrete registry-verified generation continue to use `VerifiedGeneration`; custom registry results may only expose the returned generation.
