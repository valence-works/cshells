# Verification

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~DynamicShellEndpointDataSource|FullyQualifiedName~ShellEndpointRegistrationHandler|FullyQualifiedName~ShellMiddleware"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~CollectibleFeatureGenerations_UnloadAfterReplacementAndRemoval"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~InvokeAsync_ReloadDrain_BindsInFlightRequestAndWaitsForCompletion"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~InvokeAsync_MatchedBeforeReload_UsesRoutingLeaseAcrossMiddlewareGap|FullyQualifiedName~MatcherPolicy_ReenteredThenShortCircuited_ReleasesSingleScopeOnCompletion"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~Reload_LaterSubscriberRejectsCandidate_RestoresPriorGeneration|FullyQualifiedName~Reload_MiddlewareCompositionFails_PreservesPriorGeneration|FullyQualifiedName~MethodsConflict_MethodCaseDiffers_ReturnsTrue|FullyQualifiedName~CompositionFailure_RejectsActivationWithoutPipeline"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~Reload_SlowLaterSubscriber_KeepsPriorGenerationRoutableUntilCommit|FullyQualifiedName~Reload_LaterParticipantRejectsCommit_RestoresPriorRoutesAndPipeline|FullyQualifiedName~Reload_CommitConflict_RestoresPriorActiveAndAllEntries|FullyQualifiedName~Reload_ParticipantCommitFailure_RestoresPriorRegistryStateAndRollsBackInReverse"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~PrepareGeneration_OverlappingCommitAndRollback_DoesNotResurrectRoutes|FullyQualifiedName~PrepareGeneration_ConcurrentConflict_SecondCommitIsRejected|FullyQualifiedName~GetChangeToken_"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~PrepareGeneration_Complete_AllowsNextCommit|FullyQualifiedName~PrepareGeneration_PendingCommit_GuardsSameShellMutations|FullyQualifiedName~PrepareGeneration_PendingCommit_GuardsCrossShellPublication|FullyQualifiedName~ColdStart_ReMatch_ConcurrentReload_KeepsMatchedGenerationLeased"
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~PendingCommit_OtherShellDrain_RemovesEndpointsAndReleasesReferences|FullyQualifiedName~PendingCommit_SameShellCandidateDrain_RestoresPriorGenerationWithoutStaleRoutes"
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
This is production route-builder/data-source retention evidence; it does not claim to exercise an
attached server's matcher cache.

The reload/drain integration tests activate real registry generations. One sends an old-generation
request into `ShellMiddleware` before replacement; another acquires the endpoint generation in the
matcher policy, pauses before middleware, completes the reload attempt, and then enters middleware.
They verify old and new requests use their exact pipelines and the old request's own lease is the
only in-flight scope: drain does not complete until that response's `OnCompleted` callback releases
it. Policy re-entry and downstream short-circuiting retain the same single-lease guarantee.

The rollback integration tests use the real registry and two-phase activation chain. A slow later
subscriber proves its provisional candidate remains invisible. A participant registered after the
endpoint handler rejects after endpoints commit, proving reverse rollback restores generation-one
registry identity, routes, and pipeline. Another participant introduces a late host conflict and
proves failed endpoint commit restores `Active`/`All`. Independent participants record reverse
rollback ordering. Middleware composition fails during prepare without disturbing the prior
generation, and a focused route test preserves ordinal case-insensitive method overlap semantics.

Overlapping-publication tests commit several prepared same-shell transactions in adversarial order
and prove stale rollback cannot resurrect an intermediate generation. Cross-shell and host-route
tests prove the global collision inventory cannot mutate while rollback evidence is live, while
explicit completion and rollback both release the next prepared commit. Change-token tests race
acquisition and publication repeatedly, proving returned tokens are never backed by an eagerly
disposed source. A blocking endpoint-feature fixture pauses cold rematch exactly at endpoint
exposure, performs a real concurrent reload, and proves the exact-generation lease holds drain until
response completion.

Lifecycle-cleanup regressions hold one shell's publication transaction inside a later activation
participant. A different real registry shell drains and disposes during that window, proving its
endpoint object becomes collectible. The pending candidate itself is also drained and disposed
before a later rejection, proving deferred generation cleanup neither corrupts rollback identity nor
leaves candidate routes behind.

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
- Global transaction, lifecycle cleanup, cold-rematch, matched-before-middleware, and
  collectible-context regressions: 8 passed per invocation across 5 consecutive invocations.
- Focused routing, lifecycle-handler, middleware, and activation regressions: 90 passed.
- `dotnet test tests/CShells.Tests/CShells.Tests.csproj`: 628 passed after the lifecycle-cleanup
  follow-up.
- `dotnet test CShells.sln`: 628 CShells tests and 31 end-to-end tests passed.
- `dotnet build CShells.sln`: succeeded with 0 warnings and 0 errors.
