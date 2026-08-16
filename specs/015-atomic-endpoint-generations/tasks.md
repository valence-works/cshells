# Tasks: Atomic Shell Endpoint Generations

## Phase 1 — tests and contracts

- [x] T001 Add route-owner metadata and conflict exception tests.
- [x] T002 Add exact/equivalent/multi-method/same-batch/host conflict tests.
- [x] T003 Add failed-candidate preservation and atomic replacement tests.
- [x] T004 Add a combined reload/drain test for exact-generation binding, replacement routing,
  and `OnCompleted` scope release.
- [x] T005 Add five-cycle collectible `AssemblyLoadContext` evidence through the production
  route-builder/data-source publication and replacement/removal path.

## Phase 2 — implementation

- [x] T006 Implement typed ownership metadata and deterministic diagnostics.
- [x] T007 Implement candidate normalization, validation, and immutable publication.
- [x] T008 Track feature ownership while mapping shell endpoints.
- [x] T009 Stage middleware pipeline composition and commit it after publication.
- [x] T010 Bind matched requests to exact endpoint generation.

## Phase 3 — validation

- [x] T011 Run focused ASP.NET Core tests.
- [x] T012 Run full CShells tests/build and diff review.
- [x] T013 Add transactional rollback when a later lifecycle subscriber rejects a provisionally
  published generation.
- [x] T014 Reject middleware-composition failures, compare HTTP methods case-insensitively, and
  prove the combined in-flight reload/drain sequence without an independent guard scope.
- [x] T015 Close the endpoint/pipeline visibility window and add structured shell, generation,
  and feature conflict ownership.
- [x] T016 Perform self-review/fix iterations and leave a clean committed worktree.
