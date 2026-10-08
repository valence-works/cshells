# Quickstart: Opt-in Shell Activation Runner

The deterministic runner suite is `tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs`.

Restore and run the focused runner suite:

```bash
dotnet restore tests/CShells.Tests/CShells.Tests.csproj
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter FullyQualifiedName~ShellActivationRunnerTests
```

Run existing activation/reload regressions and the multi-target runtime build:

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~ShellRegistryActivateTests|FullyQualifiedName~ShellRegistryReloadTests|FullyQualifiedName~ShellRegistryGetOrActivateTests"
dotnet build src/CShells/CShells.csproj --no-restore
```

Expected outcomes:

1. `AddCShells` alone does not register/start a runner. Explicit registration is TryAdd-style and the runner is unavailable inside child shell service providers.
2. Initial attempts are serial and ordered. Failures do not skip later targets. No retry timer or `NextAttemptAt` is armed before `InitialPass` completes.
3. Fake-time advancement causes each retry at the policy-selected deadline. Invalid policy decisions stop only that target.
4. Snapshot reentry is safe and snapshots expose safe error fields only. Retry policy and observer callbacks receive transient exception details where supplied.
5. Only a current committed concrete CShells Shell externally satisfies a target. A satisfied target is never reactivated after drain.
6. Stop prevents future scheduling, observes owned work, respects its caller's wait token, and retains cancellation ownership until stubborn activation completes.

Mutation proof changes the runner to accept a provisional active candidate or starts retries before the initial-pass boundary. The relevant committed-marker/InitialPass gated test must fail under that mutation and pass after restoration. Root owns the full combined CShells suite and external qualification.
