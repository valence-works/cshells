# Quickstart: Settled Shell Activation Results

The deterministic integration suite is `tests/CShells.Tests/Integration/Lifecycle/ShellRegistryActivationSettlementTests.cs`.

Run the focused suite:

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter FullyQualifiedName~ShellRegistryActivationSettlementTests
```

Expected outcomes:

1. A caller started after candidate publication stays pending while participant `Commit` is blocked.
2. Successful commit returns the new candidate to both activation callers.
3. Failed commit restores the old committed generation or performs a later unique activation when the first activation failed.
4. During reload composition, the old committed generation remains a valid fast-path result; after publication, the waiter waits for settlement.
5. Cancelling the waiter does not cancel the blocked activation.
6. Candidate removal before the final commit eligibility check does not set the committed marker.
7. Same-name concurrent requests do not create duplicate generations; different names remain independent.

Run relevant existing activation, reload, and routing tests plus the multi-target build:

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~ShellRegistryActivateTests|FullyQualifiedName~ShellRegistryReloadTests|FullyQualifiedName~WebRoutingShellResolverTests"
dotnet build src/CShells/CShells.csproj --no-restore
```

Root owns the full `CShells.Tests` regression. Mutation proof restores the unconditional fast-path return; the test with blocked commit must then fail. Restore the committed-marker check and rerun that focused test to pass.
