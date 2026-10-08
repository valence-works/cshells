# Quickstart: Runtime Feature Catalog Commit Notifications

1. Resolve `IRuntimeFeatureCatalog` from the host's root provider.
2. Capability-test the resolved instance for `IRuntimeFeatureCatalogCommitSource`; do not resolve the optional interface through a separate DI registration.
3. Subscribe before the first `EnsureInitializedAsync` call. The handler receives the full `RuntimeFeatureCatalogSnapshot`, including generation.
4. Call `EnsureInitializedAsync` once, then repeat it and read `CurrentSnapshot`. The event count should remain one.
5. Call `RefreshAsync` again and verify the next event carries exactly the returned snapshot and a greater generation.
6. Add two handlers, make the first throw, and verify the second still receives the committed snapshot.
7. Hold the first handler at a test barrier, commit a second refresh, and verify that the second refresh completes while its event waits in queue. Release the first handler and verify FIFO delivery with no missing generation.
8. Trigger one refresh from within a handler and verify it completes without deadlock and its event arrives after the current callback.
9. Subscribe after initialization: verify there is no replay, then reconcile by reading `CurrentSnapshot` after registration.
10. Fail or cancel discovery before commit and verify there is no event and `CurrentSnapshot` remains the prior committed generation.
