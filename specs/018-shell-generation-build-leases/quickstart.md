# Quickstart: Shell Generation Build Leases

The regression scenarios are covered by `tests/CShells.Tests/Integration/Lifecycle/ShellGenerationBuildLeaseTests.cs` plus focused existing lifecycle tests.

From the repository root, run the focused suite:

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter FullyQualifiedName~ShellGenerationBuildLeaseTests
```

Expected outcomes:

1. Participant calls follow descriptor reservation → composition/name validation → `BeginAsync` in registration order → catalog snapshot selection → lease snapshot callbacks → feature construction.
2. A blocked old provider-disposal gate keeps its lease alive even after the shell reports `Disposed`; the lease releases only after the gate opens and disposal completes.
3. A reload can keep old and new generation leases concurrently; old teardown releases only the old generation's lease.
4. Begin, snapshot, feature build, initializer, lifecycle and provider failures preserve the designated primary error and either unwind or retain unresolved leases as specified.
5. GC proof shows the root registry retains an unresolved lease independently of the disposed shell and provider.
6. No participant registration follows existing behavior; participant services are root-only and do not resolve from a child shell provider.

Run the project-level multi-target build and lifecycle regression suites before integration review:

```bash
dotnet build src/CShells/CShells.csproj
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter FullyQualifiedName~Lifecycle
```

The required mutation/revert proof temporarily moves lease release to the `Disposed` transition callback. The slow-provider-disposal test must fail during the blocked provider teardown; restore the implementation and rerun the focused test to pass.

The full CShells test suite and external qualification are root-owned gates and are not replaced by this quickstart.
