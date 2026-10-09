# Quickstart: Returned Generation Diagnostics

## Expected result

After implementation, a completed successful activation attempt exposes the generation on the exact shell object it returned. The value is visible on both the callback attempt and the target snapshot, for built-in and custom registries. Existing `VerifiedGeneration` values and all activation status transitions remain unchanged.

## Focused verification

Run the activation runner tests from the repository root:

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter FullyQualifiedName~ShellActivationRunnerTests
```

The suite must retain the existing custom-registry `VerifiedGeneration == null` assertion and verify returned-generation independently. Added deterministic cases should cover successful built-in/custom returns, stale return, call failure, call cancellation, external satisfaction (including the already-in-flight return race), constructor compatibility, and later registry changes after a successful return.

Build both affected production projects for every library target framework:

```bash
dotnet build src/CShells/CShells.csproj
```

The `CShells` project references `CShells.Abstractions`; the unfiltered build must report successful outputs for `net8.0`, `net9.0`, and `net10.0`. Also run the full library and end-to-end suites:

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj
dotnet test tests/CShells.Tests.EndToEnd/CShells.Tests.EndToEnd.csproj
```

Before publication, verify the old constructor ABI using a consumer binary compiled against the pre-change abstractions assembly and executed with the candidate abstractions assembly. After the normal organization PR is merged and the standard workflow publishes the preview family, audit every family archive and run a fresh-cache, PackageReference-only consumer on .NET 8, 9, and 10. The public consumer must cover successful built-in/custom returns, stale/failed/canceled outcomes, external-only and in-flight external satisfaction, later-generation stability, and unchanged verified-generation behavior. Record loaded-assembly and archive identity, package source, cache metadata, exact commands, and all results under the session-owned artifact workspace at `artifacts/modular-hosting-2500/cshells-167/`.

For the causal proof, temporarily remove only the returned-generation assignment from the successful attempt path, rebuild, and require the returned-generation regression test to fail. Restore the exact source bytes, rebuild, and rerun the focused suite successfully. Keep both logs and the before/after source hashes with the qualification record.
