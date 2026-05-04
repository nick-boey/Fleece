## 1. Git-Backed IEventGitContext Implementation

- [x] 1.1 Create `GitEventContext` class in `src/Fleece.Core/EventSourcing/Services/` implementing `IEventGitContext`. Implement `GetHeadSha()` via `IGitService` (`git rev-parse HEAD`), `IsFileCommittedAtHead()` via `git ls-files --error-unmatch`, and `GetFirstCommitOrdinal()` via `git rev-list --reverse HEAD` + `git log --diff-filter=A`. Memoize the commit list on first use.
- [x] 1.2 Register `GitEventContext` in `ServiceCollectionExtensions.AddFleeceCore`. Replace the `NullEventGitContext.Instance` singleton with a lambda that checks `IGitService.IsGitRepository()` and returns `GitEventContext` when in a repo, `NullEventGitContext.Instance` otherwise.
- [x] 1.3 Add a unit test (Core) verifying `GitEventContext.GetFirstCommitOrdinal` returns correct ordinals for files introduced in different commits. Use `MockFileSystem` + stub `IGitService`.
- [x] 1.4 Add a unit test (Core) verifying `GitEventContext.GetFirstCommitOrdinal` returns `null` for uncommitted files.
- [x] 1.5 Add a unit test (Core) verifying DI resolves `NullEventGitContext` when not in a git repo.

## 2. Integration Tests: Real Git Merge Scenarios

- [x] 2.1 Add `Merge_two_branches_editing_different_properties_on_same_issue_replays_both_changes` to `EventSourcedLifecycleTests`. Create issue on main → branch A edits title → branch B edits status → git merge branch B into branch A → verify both title and status reflect branch edits.
- [x] 2.2 Add `Merge_two_branches_editing_same_property_on_same_issue_last_commit_wins` to `EventSourcedLifecycleTests`. Create issue on main → branch A sets title to "A" → branch B sets title to "B" → merge → verify title is "B" (later-committed branch wins, assuming B was committed after A).
- [x] 2.3 Add `Real_squash_merge_produces_equivalent_state_after_project` to `EventSourcedLifecycleTests`. Build a feature branch with chained change files → `git merge --squash` + `git commit` onto main → verify project produces same state as pre-squash.
- [x] 2.4 Update `ReadIssuesAsync` helper in `EventSourcedLifecycleTests` (or GitTempRepoFixture) to use `GitEventContext` instead of `NullEventGitContext.Instance` so commit-order tiebreaks are tested with real git data during integration tests.

## 3. Migration Tests Using Real Fixtures

- [x] 3.1 Add E2E test `Migrate_diff_issues_fixture_reconciles_overlapping_issues` to `MigrateScenarios`. Copy files from `tests/examples/diff-issues/` into the mock `.fleece/` directory, run `migrate`, and verify: correct count of deduplicated issues, no `*LastUpdate`/`*ModifiedBy` keys in output, property-level merge reflects more-recent timestamps.
- [x] 3.2 Add E2E test `Migrate_nested_issues_fixture_preserves_parent_issues` to `MigrateScenarios`. Copy `tests/examples/nested-issues/.fleece/issues_939e3c.jsonl` into mock FS, run `migrate`, verify all 13 issues appear in `issues.jsonl` and parent-child relationships survive migration.
- [x] 3.3 Add Core unit test or integration test that verifies the `diff-issues` fixture migration adds correct `.gitignore` entries and creates the `changes/` directory.

## 4. Commit-Order Tiebreak Tests

- [x] 4.1 Add `Parallel_branches_with_no_follows_ordered_by_commit_ordinal` to `EventSourcedLifecycleTests`. Create issue on main, project to clear change files, then manually plant two change files with `follows=null` introduced by different commits. Verify the later-committed file's events win.
- [x] 4.2 Add `Same_commit_parallel_roots_tiebreak_by_guid_alphabetical` to `EventSourcedLifecycleTests`. Plant two change files in the same commit with `follows=null`. Verify alphabetical GUID ordering.

## 5. Verification

- [x] 5.1 Run `dotnet build` to ensure the new `GitEventContext` class compiles without errors.
- [x] 5.2 Run `dotnet test` and verify all existing tests continue to pass alongside the new tests.
- [x] 5.3 Confirm no tests fail due to `GitEventContext` being wired into DI (it may affect existing test paths that previously relied on `NullEventGitContext`).
- [x] 5.4 Run `dotnet test --filter "FullyQualifiedName~Integration"` specifically to verify the integration test suite passes with the real git context.

## Summary

- **New:** `GitEventContext` — real git-backed `IEventGitContext` wired into DI
- **Core change:** Added `RunGitCommand` to `IGitService` interface
- **New tests:** 7 unit tests for `GitEventContext`, DI resolution test, 6 integration tests (merge scenarios, commit-order tiebreak, squash-merge), 3 E2E fixture-based migration tests
- **Changed:** `ReadIssuesAsync` helper now uses `GitEventContext` to exercise real commit-ordering during integration tests
