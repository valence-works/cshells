# Research: Returned Generation Diagnostics

## Source Findings

- `src/CShells.Abstractions/Hosting/ShellActivationAttempt.cs` defines the public transient attempt record. Its public constructor already accepts `long? verifiedGeneration` as the final optional parameter, and `VerifiedGeneration` is a get-only property. Adding a separately initialized property can preserve the constructor signature and current property meaning.
- `src/CShells.Abstractions/Hosting/ShellActivationAttemptState.cs` defines the public immutable target snapshot record. Its constructor has a long positional parameter list ending in `policyErrorCode`; the existing snapshot properties use `init`, and `VerifiedGeneration` is `long?`. Keep the constructor unchanged and add scalar metadata consistently.
- `src/CShells/Hosting/ShellActivationRun.cs`, `RunAttemptAsync`, receives the exact `IShell` from `IShellRegistry.GetOrActivateAsync`, then checks it with `IsSettledCurrent`. The built-in verified-generation value is taken from a concrete settled `Shell`, which intentionally excludes custom implementations.
- `RecordAttempt` copies successful attempt data into the target snapshot. `TryReconcileExternal` creates `SatisfiedExternally` state for a settled built-in shell without an own successful returned call. These existing boundaries allow returned and verified generation to remain separate.
- The current external reconciliation path preserves external status and verified-generation across a late activation result. `ExternalSatisfactionIsTerminalAgainstLateResultAndLaterDrain` covers both a current late return (`Succeeded`) and a stale late return (`NotCurrent`). Returned-generation must describe the own late return when successful, remain null when stale, and leave external status/verified-generation unchanged.
- `tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs` already contains `CustomRegistrySuccessRequiresCurrentIdentityAndHasNoDefaultGeneration`, the external-satisfaction late-return test, stale-return tests, cancellation tests, and terminal-state preservation tests. Extend these fixtures rather than duplicating their setup. Preserve their current `VerifiedGeneration` assertions.

## Decisions

- Use nullable `long` for the new scalar because it matches the existing public generation-valued property type. `ShellDescriptor.Generation` is an `int`; assignment widens exactly and no shell reference is needed.
- `ReturnedGeneration` represents only the descriptor generation from the shell returned by this attempt's successful activation call after current-result validation. The value is absent for `NotCurrent`, thrown failure, cancellation before successful return, and external settlement without a successful own return.
- Keep verified-generation untouched: it continues to express built-in verification and eligible external settlement. In particular, successful custom-registry returns keep `VerifiedGeneration == null` while gaining a returned-generation value.
- A later registry change cannot rewrite the value already captured for a successful attempt. External status and verified-generation stay as recorded even if an in-flight own attempt finishes afterward; returned-generation reflects that attempt only if it succeeds. No new reconciliation loop, observer, or readiness contract is needed.

## Risks and Controls

- **Risk**: Reusing or broadening `VerifiedGeneration` would break the documented custom-registry distinction. **Control**: Keep all old assertions and add separate assertions for both properties.
- **Risk**: Reading `GetActive` after return could associate the attempt with a different generation under a race. **Control**: Capture from the exact returned object already used by the existing currentness check.
- **Risk**: Changing a positional constructor would break consumers. **Control**: Add an init-only property and verify existing public constructors remain available.
- **Risk**: Treating run cancellation after successful return as an unsuccessful activation would alter behavior. **Control**: Assign returned-generation at the existing successful-return boundary only; leave cancellation and terminal-state logic unchanged.
