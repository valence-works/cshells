# Validation: Settled Active Observation

Use the repository .NET SDK through the shared build-slot wrapper. The focused scenario uses real DI/registry composition and gated participants; no elapsed-time sleeps determine correctness.

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release --filter FullyQualifiedName~ShellRegistrySettledObservationTests
dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release --no-restore
dotnet test tests/CShells.Tests.EndToEnd/CShells.Tests.EndToEnd.csproj -c Release --no-restore
dotnet build src/CShells/CShells.csproj -c Release --no-restore
```

Expected: cold observations do no provider/build work; gated initial and replacement commit/completion remain absent; successful settlement exposes the exact current instance; commit rejection restores the prior settled generation; draining/removed shells disappear. Invalid names and case-insensitive matching retain existing behavior.

For consumption, resolve `IShellRegistry` and cast to the optional contract documented in [contracts/ISettledShellRegistry.md](contracts/ISettledShellRegistry.md). The host decides what unsupported capability means for its own readiness policy.

Root acceptance additionally records a valid compiled marker-bypass mutation that fails the pending-commit test, its restored pass, hosted exact-head/main checks, and an external consumer restored from the actual public preview on net8/net9/net10. Inspect nuspec repository commit, archive hash, and loaded DLL bytes. Local source references and privately packed previews do not satisfy that public-package gate. Foundation's complete-host regression/E2E proof remains separate.
