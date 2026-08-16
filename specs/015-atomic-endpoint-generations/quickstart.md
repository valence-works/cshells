# Verification

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~DynamicShellEndpointDataSource|FullyQualifiedName~ShellEndpointRegistrationHandler|FullyQualifiedName~ShellMiddleware"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~CollectibleFeatureGenerations_UnloadAfterReplacementAndRemoval"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~InvokeAsync_ReloadDrain_BindsInFlightRequestAndWaitsForCompletion"
dotnet test tests/CShells.Tests/CShells.Tests.csproj
dotnet build CShells.sln
```

The collectible-load-context test uses `tests/CShells.DynamicFeatureFixture` as a real
feature assembly. It runs five cycles; each cycle loads the fixture into a collectible
`AssemblyLoadContext`, maps through `ShellEndpointRouteBuilder`, publishes through
`DynamicShellEndpointDataSource`, replaces the candidate, drives the replacement through
the lifecycle handler's draining removal, calls `Unload`, and forces bounded full-GC passes.
The assertion is on the `AssemblyLoadContext` `WeakReference`, so a published or retired
endpoint delegate that still roots the fixture assembly fails the test.

The reload/drain integration test activates a real registry generation, keeps a guard scope
open while `ReloadAsync` promotes the replacement, then sends a matched old-generation request
and a new-generation request. It verifies that the old request uses the old pipeline, the new
request uses the replacement pipeline, and drain does not complete until the old response's
`OnCompleted` callback releases its scope.

## Recorded evidence

On 2026-08-16:

- `CollectibleFeatureGenerations_UnloadAfterReplacementAndRemoval`: 1 passed per invocation,
  repeated across 5 invocations; each invocation performs 5 load/replace/drain/unload cycles.
- `InvokeAsync_ReloadDrain_BindsInFlightRequestAndWaitsForCompletion`: 1 passed per invocation,
  repeated across 3 invocations.
- `dotnet test tests/CShells.Tests/CShells.Tests.csproj`: 607 passed.
- `dotnet test CShells.sln`: 607 CShells tests and 31 end-to-end tests passed.
- `dotnet build CShells.sln`: succeeded with 0 warnings and 0 errors.
