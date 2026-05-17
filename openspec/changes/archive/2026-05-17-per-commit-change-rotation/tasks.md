## 1. Baseline Failing Tests (prove the bugs exist)

- [x] 1.1 Add `Same_worktree_two_branches_from_main_edits_conflict_on_merge` to `EventSourcedLifecycleTests`. Create issue on main → commit → branch feature/a → edit + commit → switch back to main → branch feature/b → edit + commit → merge feature/a to main (clean) → merge feature/b to main (asserts CONFLICT today). Mark `[Explicit]` initially so CI is green; flip to standard once the fix lands. This is the proof of Bug 1.
- [x] 1.2 Add `Squash_of_integration_branch_with_parallel_chains_loses_order` to `EventSourcedLifecycleTests`. Create issue on main → branch develop → branch feature/a from develop → edit title="A" + commit → switch to develop → branch feature/b → edit title="B" + commit → regular merge feature/a into develop → regular merge feature/b into develop → switch to main → `git merge --squash develop` + commit. Assert that with the *current* tiebreak, ordering is GUID-alphabetical (i.e., assert the WRONG answer to document the bug); flip to assert the correct "B wins" after the fix. Proof of Bug 2.

## 2. Per-Commit Rotation

- [x] 2.1 Inject `IEventGitContext` into `EventStore` via constructor; update DI registration in `ServiceCollectionExtensions.AddFleeceCore`.
- [x] 2.2 Modify `EventStore.ResolveActiveOrRotateAsync` to also rotate when `gitContext.IsFileCommittedAtHead(activePath)` returns true. Preserve existing triggers.
- [x] 2.3 Add unit test `EventStore_rotates_when_active_file_is_committed_at_head` using `MockFileSystem` + a stub `IEventGitContext` that returns true.
- [x] 2.4 Add unit test `EventStore_appends_to_active_file_when_not_yet_committed` (regression for the in-flight-edits case).
- [x] 2.5 Add unit test `EventStore_with_NullEventGitContext_never_rotates_for_commit_reason` (graceful degradation outside git).
- [x] 2.6 Add integration test `Multiple_edits_within_one_commit_accumulate_into_single_file` to `EventSourcedLifecycleTests`. Three `fleece edit` calls, then one `git commit`. Assert exactly one new change file with three events.
- [x] 2.7 Add integration test `First_edit_after_commit_rotates_into_new_file_with_follows_to_previous`. Edit + commit, then edit again. Assert two change files, second's `follows` equals first's GUID.

## 3. Multi-Parent `follows`

- [x] 3.1 Change `MetaEvent.Follows` type from `string?` to `IReadOnlyList<string>` (empty list = root, len 1 = single parent, len >= 2 = merge marker).
- [x] 3.2 Write `FollowsJsonConverter` accepting `null`, scalar string, and string array on read; emitting `null` for empty, scalar for length-1, array for length >= 2.
- [x] 3.3 Register the converter on `MetaEvent.Follows` in `EventSourcingJsonContext`. Verify source-gen still compiles.
- [x] 3.4 Refactor `ReplayEngine.FileNode.Follows` to `IReadOnlyList<string>`. Update in-degree counting to sum per parent; update children-map population to add child to every parent's child list.
- [x] 3.5 Update `FindDagLeafAsync` in `EventStore` so `hasDescendant` includes every GUID referenced by any `follows` entry, not just the scalar value.
- [x] 3.6 Add unit test `ReplayEngine_multi_parent_node_waits_for_all_parents`. Three files: A (root), B (root), C (follows=[A,B]). Assert order: A, B, C (with A < B by tiebreak) or B, A, C. C MUST come after both.
- [x] 3.7 Add unit test `MetaEvent_parses_scalar_and_array_follows_forms`. Round-trip both shapes.
- [x] 3.8 Add unit test `MetaEvent_serialises_length_one_follows_as_scalar` (diff minimisation).

## 4. `fleece link --merge` Command

- [x] 4.1 Add `LinkCommand` in `src/Fleece.Cli/Commands/`. Settings: `--merge` flag. Other flags reserved for future use.
- [x] 4.2 Add `ILinkService` in `Fleece.Core.Services.Interfaces`. Method: `Task<LinkResult> CreateMergeMarkerAsync(CancellationToken ct)`. Returns the new marker file's GUID or a "not-a-merge" sentinel.
- [x] 4.3 Implement `LinkService`. Steps: detect `.git/MERGE_HEAD` via `IGitService`; on each side compute leaves via the same logic as `EventStore.FindDagLeafAsync`; assemble the parents list (dedupe, omit nulls); generate fresh GUID via the same factory `EventStore` uses; write `change_{guid}.jsonl` with the marker meta event and zero body events; stage via `git add`.
- [x] 4.4 Wire the command into the CLI Spectre.Console.Cli app registration.
- [x] 4.5 Add unit tests for `LinkService` covering: no MERGE_HEAD (no-op), one MERGE_HEAD, octopus (multiple MERGE_HEADs).
- [x] 4.6 Add CLI smoke test verifying `fleece link --merge` runs cleanly when there is no merge in progress (exits 0, writes nothing).

## 5. Git Hooks

- [x] 5.1 Update `InstallService` (or equivalent) to write `.git/hooks/pre-merge-commit` with a marker-comment block containing: `if [ -f .git/MERGE_HEAD ]; then fleece link --merge || exit 1; fi; git add .fleece/changes/ 2>/dev/null || true`. Set the file executable.
- [x] 5.2 Update the existing `.git/hooks/pre-commit` block to invoke `fleece link --merge` (no-op outside a merge) before the existing `git add`.
- [x] 5.3 Ensure both hook updates remain idempotent via the existing marker-comment delimiters.
- [x] 5.4 Update `InstallScenarios` E2E tests to verify both hooks are written and contain the expected `fleece link --merge` invocation.
- [x] 5.5 Update `InstallScenarios` to verify re-running `fleece install` does not duplicate either hook block.

## 6. Replay Warning on GUID-Only Tiebreak

- [x] 6.1 Introduce `IWarningSink` (or extend an existing diagnostics interface) so warnings have an injectable destination. Default implementation writes one line to `Console.Error`.
- [x] 6.2 Modify `FileNodeReadyComparer` (or the surrounding code in `ReplayEngine.SortChangeFilesAsync`) to detect when two simultaneously-ready nodes share a non-null commit ordinal and emit a warning identifying both file paths.
- [x] 6.3 Suppress the warning if the affected files have any common descendant in the DAG that lists both as parents (i.e., a marker already covers them).
- [x] 6.4 Add unit test `ReplayEngine_warns_when_parallel_files_share_commit_ordinal`. Plant two files with the same ordinal, no marker, assert warning fires.
- [x] 6.5 Add unit test `ReplayEngine_does_not_warn_when_marker_links_parallel_files`. Same setup plus a marker referencing both; assert silence.

## 7. Integration Tests — Merge Scenarios

- [x] 7.1 Add `Merge_marker_written_by_pre_merge_commit_hook_includes_both_leaves`. Real `git merge` on two clean branches; assert the merge commit contains a `change_*.jsonl` whose meta `follows` is the two-element list of the two branch leaves.
- [x] 7.2 Add `Conflict_merge_pre_commit_hook_also_writes_marker`. Set up a synthetic merge conflict (e.g., both branches edit the same issue's title); resolve manually; run `git commit`; assert a marker was written by the pre-commit path.
- [x] 7.3 Add `Fast_forward_merge_writes_no_marker`. `git merge --ff-only` ahead branch; assert no new change file appears.
- [x] 7.4 Convert task 1.1's `[Explicit]` test to a standard test and flip the assertion to expect NO conflict.
- [x] 7.5 Convert task 1.2's bug-documenting test to assert the correct "B wins" outcome and verify marker files survive the squash.
- [x] 7.6 Add `Squash_merge_of_integration_branch_with_markers_preserves_order`. Build the develop-with-two-features scenario, ensure markers are written on the regular merges into develop, then `git merge --squash develop` into main → `git commit`. Assert order is preserved.

## 8. Integration Tests — Rebase and Cherry-Pick

- [x] 8.1 Add `Rebase_feature_onto_main_with_stale_follows_replays_via_commit_ordinal`. Build feature with two commits, rebase onto a main that has advanced. Assert: replay produces the correct semantic state with feature's events ordered after main's, even though `follows` pointers are stale post-rebase.
- [x] 8.2 Add `Cherry_pick_commit_with_dangling_follows_treats_file_as_root`. Cherry-pick a single feature commit onto an orphan branch where the `follows` target doesn't exist. Assert replay treats the file as a root (existing behaviour, but exercise it end-to-end via real git).
- [x] 8.3 Add `Interactive_rebase_drop_commit_leaves_dangling_followers_treated_as_roots`. Drop a commit that introduced a file other later commits' `follows` point at. Assert the followers become roots and replay still succeeds.

## 9. Integration Tests — Round Trips

- [x] 9.1 Add `Project_after_merge_marker_collapses_marker_into_snapshot`. Run the full create → branch → edit → merge → project flow; assert the snapshot reflects the merged state and the marker file is deleted (project deletes all change files).
- [x] 9.2 Update `Migrate_then_create_then_project_round_trip_stays_consistent` to also exercise a merge in the middle. Migrate legacy data → create on main → branch → edit → merge back → project. Assert end-state correctness.
- [x] 9.3 Verify `Real_squash_merge_produces_equivalent_state_after_project` (existing test) still passes unmodified.
- [x] 9.4 Verify `Multi_machine_squash_with_chained_follows_pointers_replays_correctly` (existing test) still passes after the `follows`-as-list refactor.

## 10. Documentation

- [x] 10.1 Apply the delta in `specs/event-sourced-storage/spec.md` to `openspec/specs/event-sourced-storage/spec.md` (this happens at archive time).
- [x] 10.2 Apply the delta in `specs/git-integration/spec.md` to `openspec/specs/git-integration/spec.md` (this happens at archive time).
- [x] 10.3 Update the "Event-sourced storage (current)" section in `CLAUDE.md` to reflect per-commit rotation and merge markers. Add the ASCII diagram from `design.md`.
- [x] 10.4 Add a short "Change files are commit-scoped and immutable" subsection to the `CLAUDE.md` event-sourced section explaining the invariant.
- [x] 10.5 Update the description in `src/Fleece.Core/EventSourcing/Services/Interfaces/IEventGitContext.cs` to remove any "PR 1 ships NullEventGitContext" remarks and reflect that `IsFileCommittedAtHead` is load-bearing for rotation.

## 11. Verification

- [x] 11.1 `dotnet build` is clean.
- [x] 11.2 `dotnet test` passes — all existing tests AND new tests.
- [x] 11.3 `dotnet test --filter "FullyQualifiedName~Integration"` passes specifically; spot-check that the two Bug-reproduction tests from tasks 1.1 and 1.2 transition from RED → GREEN across the fix commits (preserve this in commit history for the change archive).
- [x] 11.4 Manual end-to-end smoke: in a scratch repo, run `fleece install`; verify both hooks exist and contain marker blocks; run a `git merge`; verify a marker file appears in the merge commit.
- [x] 11.5 Run `openspec validate per-commit-change-rotation --strict` and resolve any complaints.

## Summary

- **New:** `fleece link --merge` command + `LinkService`; `pre-merge-commit` hook; `IWarningSink`-based diagnostics for tiebreak fallback.
- **Modified:** `MetaEvent.Follows` becomes list-or-scalar; `EventStore` rotation gains commit-aware trigger and depends on `IEventGitContext`; `ReplayEngine` handles multi-parent nodes; existing `pre-commit` hook block invokes `fleece link --merge`; `InstallService` writes both hooks.
- **New tests:** ~18 unit + integration tests covering rotation, multi-parent replay, marker generation, all merge variants, rebase, cherry-pick, and the two pre-existing bugs.
- **Docs:** Spec deltas in `event-sourced-storage` and `git-integration`; CLAUDE.md update with diagram.
