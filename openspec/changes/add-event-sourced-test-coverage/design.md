## Context

The `event-sourced-issues` change (PR #124) introduced an event-sourced storage layer with a replay engine that supports three-tier ordering:

```
1. follows-DAG topo sort        → handles within-branch causality
2. Commit order (git log)       → "last-commit-wins" for parallel branches
3. GUID alphabetical            → deterministic fallback
```

Tier 2 was designed but never connected. The DI container registers `NullEventGitContext.Instance` (`ServiceCollectionExtensions.cs:41`), which returns `null` for every commit-ordinal query. The comment on `IEventGitContext` acknowledges this ("PR 1 ships NullEventGitContext; the CLI will plug in a real git-backed implementation in PR 2") but no real implementation was ever created.

Meanwhile, the integration test suite tests lifecycle and squash scenarios but:
- No test performs a real `git merge` of two branches editing the same issue
- No test uses the real legacy fixtures in `tests/examples/`
- No test verifies commit-ordinal tiebreak behavior because all tests pass `NullEventGitContext`

## Goals / Non-Goals

**Goals:**
- Implement a git-backed `IEventGitContext` that resolves commit ordinals from the git log.
- Register it in DI so the replay engine's commit-order tiebreak becomes operational.
- Add real-git integration tests: create two branches, edit the same issue on both, merge, verify properties.
- Add integration tests using `tests/examples/diff-issues/` and `tests/examples/nested-issues/` fixtures through `fleece migrate`.
- Verify that commit-ordinal tiebreak orders parallel-root change files correctly (earlier commit replayed first, later commit's events win).

**Non-Goals:**
- Changing the replay algorithm itself. It already supports commit-ordinal ordering; we're just making the data source real.
- Changing the `IEventGitContext` interface. The existing interface is sufficient.
- Adding a git-backed `GetHeadSha()` and `IsFileCommittedAtHead()` that work differently from `NullEventGitContext`. The git-backed impl provides real values for all three methods.
- Modifying the `fleece migrate` pipeline. We're adding tests for the existing migration, not changing it.
- Fixing task 15.5 (GitHub Action template validation). That's a separate follow-up.

## Decisions

### Decision 1: New `GitEventContext` class implementing `IEventGitContext`

A single class `GitEventContext` implements all three methods:

- **`GetHeadSha()`** — calls `git rev-parse HEAD`.
- **`IsFileCommittedAtHead(string filePath)`** — calls `git ls-files --error-unmatch <path>` (exit code 0 = tracked).
- **`GetFirstCommitOrdinal(string filePath)`** — resolves the 0-based position of the commit that first introduced the file, using a two-step git query:
  1. `git rev-list --reverse HEAD` — full ordered commit list (oldest first).
  2. `git log --diff-filter=A --format='%H' -- <filePath>` — the commit that added this file.
  3. The ordinal is the index of that SHA in the ordered list (or `null` if the file doesn't exist in history, e.g., uncommitted).

The full ordered list is memoized per instance for the lifetime of the DI scope (singleton). A cache invalidation on `GetHeadSha` would be ideal, but in practice the git log is immutable within a process lifetime. The list is recomputed lazily on first use.

**Why this approach:**
- Two git commands per file + one for the full list is simple and deterministic.
- `git log --diff-filter=A` finds the commit that *added* the file, not its most recent modification. This is correct for the ordinal-based tiebreak because the "first appearance" of a change file determines its position in the commit-order layer.
- Memoizing the full list avoids O(n*m) git calls where n = change files, m = commits.

**Alternatives considered:**
- *`git merge-base` + branch topology*: Complex, fragile across squash merges.
- *Timestamp-based ordering*: Already rejected in the event-sourced-issues design (Decision 4) — vulnerable to clock skew.
- *Sequence numbers in filenames*: Also rejected in original design (Decision 2).

### Decision 2: DI registration: use `GitEventContext` when in a git repo, `NullEventGitContext` otherwise

The DI registration in `ServiceCollectionExtensions` changes from:
```csharp
services.AddSingleton<IEventGitContext>(_ => NullEventGitContext.Instance);
```
to:
```csharp
services.AddSingleton<IEventGitContext>(sp => {
    var git = sp.GetRequiredService<IGitService>();
    return git.IsGitRepository() ? new GitEventContext(git) : NullEventGitContext.Instance;
});
```

`GitEventContext` depends on `IGitService` to execute git commands. The `IGitService` is already registered earlier in the pipeline.

**Why not always use `GitEventContext` with graceful degradation:** The `NullEventGitContext` path avoids spawning git processes when not in a repo. The performance difference is negligible, but the intent is cleaner: no-git-repo → no commit ordering.

### Decision 3: Integration tests are the right layer for merge scenarios

Real git merge tests use `GitTempRepoFixture` (real file system, real git). The E2E test harness uses `MockFileSystem` and cannot run actual git merges.

| Test layer | Git support | Fixture support | Use for |
|------------|------------|-----------------|---------|
| Core unit | None | Synthetic | Replay engine logic |
| CLI E2E | `MockFileSystem` | `WriteLegacyIssuesFile` helper | Migration command behavior |
| Integration | Real git | Real filesystem | Merge, commit-order, fixtures |

The new merge and fixture tests go in `EventSourcedLifecycleTests` (integration), and the fixture-based migration tests go in `MigrateScenarios` (E2E, using `MockFileSystem` — the fixtures are read from real disk but the `.fleece` dir is in the virtual FS).

Actually, `MigrateScenarios` uses `CliScenarioTestBase` with `MockFileSystem`. The existing tests write synthetic JSON strings. For the new tests, we'll copy fixture files from real disk into the virtual FS's `.fleece/` directory using `Fs.File.ReadAllText` → `Fs.File.WriteAllText`.

### Decision 4: Test structure for merge scenarios

Each merge test follows this pattern:

```
1. Seed: create issue on main, commit
2. Branch A: edit issue (e.g., set title="A"), commit
3. Branch B (from same base): edit same issue (e.g., set title="B"), commit
4. Merge: git merge branch-b into branch-a (or vice versa)
5. Replay: read issues via replay engine
6. Assert: verify which branch's edit won
```

The tests use the `ReadIssuesAsync()` helper in `EventSourcedLifecycleTests` which bypasses DI. For commit-order tests, this helper should use `GitEventContext` instead of `NullEventGitContext.Instance`.

## Risks / Trade-offs

- **Risk:** `git log --diff-filter=A` might not find the introducing commit for a file that was created in an orphan branch or via `git cherry-pick`.
  **Mitigation:** The method catches git failures and returns `null` for the ordinal. `FileNodeReadyComparer` treats `null` as `int.MaxValue` (sorts last). This is safe — it degrades to the existing GUID-alphabetical fallback.

- **Risk:** `GitEventContext` caches the commit list for the lifetime of the DI scope. If a new commit is created during the process lifetime (e.g., a long-running server using Fleece Core), the cache would be stale.
  **Mitigation:** Fleece is a CLI tool; the process lifetime is a single command invocation. No in-process commit creation. The cache is valid for the entire lifetime. If this assumption changes in the future, the cache can be invalidated by monitoring HEAD.

- **Risk:** `git rev-list --reverse HEAD` on a large repository is expensive (tens of thousands of commits).
  **Mitigation:** The list is computed once per process. Fleece repos are typically small (a few hundred commits). Even on large repos, `git rev-list` with `--reverse` is fast (git stores commit history efficiently).

- **Risk:** The new merge integration tests are `[NonParallelizable]` (already the case for all `GitTempRepoFixture` tests) and add to CI wall-clock time.
  **Mitigation:** The existing 16 integration tests run in ~5 seconds. Adding 3-4 more adds negligible time.
