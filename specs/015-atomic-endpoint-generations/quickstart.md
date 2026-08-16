# Verification

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~DynamicShellEndpointDataSource|FullyQualifiedName~ShellEndpointRegistrationHandler|FullyQualifiedName~ShellMiddleware"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~CollectibleFeatureGenerations_UnloadAfterReplacementAndRemoval"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~InvokeAsync_ReloadDrain_BindsInFlightRequestAndWaitsForCompletion"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~Reload_LaterSubscriberRejectsCandidate_RestoresPriorGeneration|FullyQualifiedName~Reload_MiddlewareCompositionFails_PreservesPriorGeneration|FullyQualifiedName~MethodsConflict_MethodCaseDiffers_ReturnsTrue|FullyQualifiedName~CompositionFailure_RejectsActivationWithoutPipeline"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~ActiveTransition_PublishesPipelineBeforeEndpointsBecomeVisible|FullyQualifiedName~ActiveTransition_EndpointConflict_RemovesStagedPipeline|FullyQualifiedName~PublishGeneration_EquivalentTemplates_PreservesPreviousSnapshot"
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

The reload/drain integration test activates a real registry generation and sends a matched
old-generation request into `ShellMiddleware` before `ReloadAsync` promotes the replacement. It
then sends a new-generation request and verifies that each uses its exact pipeline. The old
request's own scope is the only in-flight scope: drain does not complete until that response's
`OnCompleted` callback releases it.

The rollback integration tests use the real registry and lifecycle subscriber chain. One rejects
generation two after its endpoints and pipeline have been provisionally published, proving the
generation-one endpoint snapshot and pipeline are restored. The other fails generation-two
middleware composition before publication, proving activation is rejected without disturbing the
prior generation. A focused route test preserves ordinal case-insensitive method overlap semantics.

The endpoint/pipeline ordering tests subscribe to the routing change token, which fires at the
first externally visible publication point, and assert the exact generation pipeline is already
available. A rejected-candidate companion test proves a staged pipeline is removed. Conflict tests
assert shell identifier, generation, and feature ownership in both the message and structured data.

## Recorded evidence

On 2026-08-16:

- `CollectibleFeatureGenerations_UnloadAfterReplacementAndRemoval`: 1 passed per invocation,
  repeated across 5 invocations; each invocation performs 5 load/replace/drain/unload cycles.
- `InvokeAsync_ReloadDrain_BindsInFlightRequestAndWaitsForCompletion`: 1 passed per invocation,
  repeated across 3 invocations.
- Transactional rollback, middleware rejection, method-case overlap, endpoint/pipeline ordering,
  ownership diagnostics, combined drain, and collectible-context regressions: 9 passed per
  invocation, repeated across 3 invocations.
- Focused routing, lifecycle-handler, middleware, and activation regressions: 74 passed.
- `dotnet test tests/CShells.Tests/CShells.Tests.csproj`: 612 passed.
- `dotnet test CShells.sln`: 612 CShells tests and 31 end-to-end tests passed.
- `dotnet build CShells.sln`: succeeded with 0 warnings and 0 errors.
