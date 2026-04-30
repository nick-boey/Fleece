## Context

Fleece currently persists issues in `.fleece/issues_{hash}.jsonl` files where `{hash}` is derived per machine. Every commit that touches issue data causes the entire file to appear modified (or for a new machine, an entire file to be added). Reviewers cannot see *what* changed — only *that* a file changed. Branches accumulate large opaque diffs, and the property-level merge that runs on `fleece merge` (in `IssueMerger`) is itself a source of churn because it rewrites every `*LastUpdate` and `*ModifiedBy` field on every reconciliation.

The existing per-property timestamp/modifier metadata (`TitleLastUpdate`, `TitleModifiedBy`, `StatusLastUpdate`, ...) was introduced to power last-write-wins merges across hashed files. With an event-sourced model, that metadata moves into events and the persisted issue shape becomes lean. The `IssueMerger` becomes obsolete.

Constraints:
- Solo developers and small teams using rebase-style or squash-merge workflows on GitHub.
- Multi-machine usage on the same branch is realistic (laptop + desktop, fresh clones).
- Squash-merge must produce identical replay results to pre-squash on the branch — the cost of a wrong-order replay is silent data corruption.
- Pre-commit hooks are the only reliable place to mutate the working tree before a commit lands; they fire before the new commit's SHA is known.

Stakeholders: every existing Fleece user. Migration is a one-shot breaking change.

## Goals / Non-Goals

**Goals:**

- Per-commit diffs that show exactly which events were appended.
- Identical projection results pre- and post-squash-merge.
- Read paths that work uniformly across all branches by replaying snapshot + change files in memory.
- A `fleece project` command that compacts events into the snapshot, runs only on `main`, and is wired to a daily GitHub Action.
- A migration command that converts existing data and is safe to run idempotently.
- No CI mutation step required for correctness — the design is local-only.

**Non-Goals:**

- Preserving full per-property modification history across migration. (Lossy: git history is the historical record.)
- A pre-merge GitHub Action that flattens change files before squash. The DAG-based ordering removes the need.
- Auto-cleanup for terminal statuses other than `Deleted`. (Out of scope; future enhancement.)
- Any change to the user-facing CLI verb surface beyond adding `fleece project` and deprecating `fleece merge`.
- Concurrent multi-process writes from the same working tree. Fleece is a developer tool; one writer at a time per working tree is assumed.

## Decisions

### Decision 1: Single snapshot file plus per-session change files

We adopt the canonical event-sourcing split: `.fleece/issues.jsonl` is a projected snapshot, and `.fleece/changes/change_{guid}.jsonl` files are append-only event logs.

**Why over alternatives:**
- *Single growing file* (no snapshot): unbounded growth, every read replays everything from epoch.
- *One file per issue* (Git-native CRDT-ish): minimizes diff size per change but explodes file count and makes "list all issues" expensive to read on cold cache.
- *Per-machine hashed files* (status quo): the diff problem we're solving.

The snapshot bounds replay cost; the per-session change files isolate concurrent work into separate files that don't textually conflict.

### Decision 2: One change file per branch-session-on-machine; GUID identifies the session

A "session" is a unique combination of (working tree, branch, machine). Each session writes to one `change_{guid}.jsonl` file across however many commits it produces.

A gitignored pointer `.fleece/.active-change` records the current GUID. On every fleece write, the pointer is read; if it is missing or refers to a file that doesn't exist on disk, a fresh GUID is generated.

**Why one file per session, not one file per commit:**
- Per-commit files (with sequence numbers) require ordering by sequence number under squash, which only orders *within* a session — multi-machine squash still needs a separate signal.
- Per-session files unify the ordering story: every file has an explicit `follows` pointer, so squash, rebase, and merge-commit workflows all produce identical replay results.
- Diff visibility is identical (appended lines render the same regardless of whether the file is new or modified).
- Fewer files; simpler rotation logic.

**Why GUID, not branch+commit hash or branch name:**
- Branch+parent-commit hash collides when two branches fork from the same commit.
- Branch-name-based hashes collide when the same branch is worked on from two machines.
- Random GUID is collision-free in both dimensions and pairs with a recovery mechanism (the "file doesn't exist" rotation trigger) that gracefully handles a lost pointer.

### Decision 3: First line of each change file is a `meta` event with a `follows` pointer

The first line of every `change_{guid}.jsonl` is `{"kind":"meta","follows":"<predecessor-guid|null>"}`. When a fresh GUID is generated, the writer scans existing change files in `.fleece/changes/`, reads each first line to build a follows-DAG, finds a leaf (no descendants), and uses that as `follows`.

**Why this shape:**
- Self-describing: each file carries the metadata needed to position itself in the DAG.
- Conflict-free: the meta event is written once at file creation and never modified, so there's no append-conflict risk on a marker file.
- Solves the multi-machine squash case by carrying causality across what would otherwise be parallel sessions.

**Alternatives considered:**
- Single index file `.order` listing GUIDs in order: subject to merge conflicts; the very problem we're avoiding.
- Per-GUID sidecar `.meta` file: equivalent semantics but doubles file count.
- Sequence numbers in filenames (`change_{guid}_{n}.jsonl`): doesn't solve multi-machine squash; chosen against (see Decision 2).

### Decision 4: Replay algorithm — DAG topo sort, with commit-order tiebreaks

Replay order is determined by:

1. Build the follows-DAG from all change files' meta events.
2. Topologically sort the DAG.
3. When the topo sort is ambiguous (multiple root nodes, parallel siblings), tiebreak by the first commit on the current branch's `git log` that introduced the file.
4. When commit order is also ambiguous (post-squash, multiple files at the same commit with no follows relationship), tiebreak by GUID alphabetical.
5. Within a file, events are applied in line order.
6. The active uncommitted file is just another DAG node, sorted naturally by its `follows` pointer (typically last).

Cherry-picked files with a `follows` target that doesn't exist locally treat the pointer as null (root), with commit-order placement as a tiebreaker.

**Why this layered approach:**
- DAG topo handles the within-branch causality (the structurally important case).
- Commit order on main matches the user-expected "PR-merge-order wins" semantic for between-branch conflicts on the same property.
- GUID alphabetical is a deterministic last-resort tiebreaker.

**Alternatives:**
- Pure timestamp ordering: vulnerable to clock skew; can produce events targeting issues that don't yet exist in the global timeline (causality violation).
- Pure git topology: collapses under squash.
- Sequence-number-only: doesn't solve multi-machine squash.

### Decision 5: `fleece project` is main-only and writes a single commit

`fleece project` reads `issues.jsonl`, replays all change files using the same algorithm as reads, writes a new `issues.jsonl` and updated `tombstones.jsonl`, and deletes every file in `.fleece/changes/`. It refuses to run on any branch other than the configured default branch (typically `main`), exiting non-zero with a clear message.

The same step runs auto-cleanup: any issue with `status=Deleted` whose status was set more than 30 days ago is hard-deleted (a tombstone is written, the issue is dropped from the snapshot, and a `hard-delete` event is implicitly absorbed into the projection).

**Why main-only:**
- Projection mutates the snapshot; only one writer can be allowed for that to keep the projected history linear.
- Running on a feature branch would diverge the snapshot from main and require manual reconciliation.
- The protection is enforced via `git symbolic-ref HEAD` + a configurable default branch name.

**Why a single commit:**
- The full effect (snapshot rewrite + change-file deletions + tombstone writes) is atomic from a reviewer's perspective.
- Daily cadence keeps the change-file directory bounded.

### Decision 6: Pre-commit hook only; no post-commit hook

`fleece install` writes a `.git/hooks/pre-commit` that runs `git add .fleece/changes/` (and the snapshot path on main) so the developer can't accidentally commit code without their fleece edits. There is no post-commit hook.

**Why no post-commit:**
- The original temptation was to rotate the active-change pointer post-commit, but the rotation rule ("file doesn't exist on disk → fresh GUID") doesn't need it. The same file accumulates events across commits on a session.
- Fewer hooks = fewer ways to be broken on cross-platform git installs.

### Decision 7: Replay cache keyed by HEAD sha

`.fleece/.replay-cache` (gitignored) holds the projected state after applying all *committed* change files at a given HEAD sha. Reads load this cache and replay only the active uncommitted file on top.

**Cache invalidation:**
- HEAD sha changes (commit, checkout, rebase, pull) → recompute.
- Otherwise the cache is stable across reads.

**Why cache only the committed slice:**
- The active file is by far the most volatile and is cheap to replay.
- The committed slice is invariant within a HEAD sha, making caching trivially correct.

### Decision 8: Lossy migration; legacy DTOs in a `Legacy` namespace

The migration command reads legacy `issues_{hash}.jsonl` files using `Fleece.Core.Models.Legacy.LegacyIssue` (the current `Issue` shape, relocated), runs the existing `IssueMerger` to produce a single merged set, writes `issues.jsonl` in the new format, copies tombstones into `tombstones.jsonl`, and deletes the legacy files. Per-property timestamps and modifier history are dropped on the floor.

**Why lossy:**
- Synthesizing one event per property to preserve the timestamps/modifiers would produce a giant initial change file in the migration commit. Reviewability suffers and there's no real consumer for the data.
- The information is preserved indirectly in git history of the legacy files, which is enough for the rare audit case.

**Why a `Legacy` namespace, not deletion:**
- The migration command needs to read the old shape for at least one release cycle.
- Once migration is rolled out across all consumers, the namespace can be deleted in a follow-up.

## Risks / Trade-offs

- **Risk:** Lost `.active-change` pointer mid-session creates two GUIDs per session.
  **Mitigation:** The follows-DAG handles this correctly — the second GUID's first event sets `follows` to the leaf, preserving order. The only cost is one extra file. Acceptable.

- **Risk:** Pre-commit hook bypassed via `--no-verify`; active change file goes uncommitted.
  **Mitigation:** Next fleece write detects the file is in the working tree but unstaged, and continues appending to it. The next commit naturally picks up everything. No corruption.

- **Risk:** Cherry-picked change file refers to a `follows` GUID not present locally.
  **Mitigation:** Treat dangling pointer as null; the file becomes a DAG root, ordered by commit position. Graceful degradation; correctness preserved.

- **Risk:** Daily projection commit lands while a long-running PR is in review; PR must rebase.
  **Mitigation:** Document the workflow expectation. The rebase only pulls in the projected snapshot and removes the now-obsolete change files; local change files in the PR survive cleanly. We accept this as a workflow cost in exchange for the diff-quality win.

- **Risk:** Two parallel processes writing fleece events from the same working tree (e.g., a script + manual CLI use).
  **Mitigation:** Out of scope. Fleece is a single-developer tool per working tree. No file locking is added.

- **Risk:** Clock skew across machines for the `at` timestamp on events.
  **Mitigation:** Replay order does not depend on `at` timestamps for correctness — it depends on the follows-DAG and commit order. `at` is informational. Skew is harmless.

- **Risk:** Squash collapses 10 commits into 1; the developer expects to see per-commit history of fleece changes.
  **Mitigation:** None — squash is squash. The events are still all there, replayable in order, just not grouped per-original-commit. This matches user expectations of squash.

- **Risk:** `IssueMerger` deletion breaks external consumers (e.g., Homespun).
  **Mitigation:** Coordinate the rollout. The `fleece merge` deprecation message points at `fleece project`. External consumers using Core directly need to switch to the new event-aware storage service. This is announced as a breaking change.

- **Trade-off:** Reading on a feature branch with many committed change files requires git-log lookups for the tiebreaker step.
  **Mitigation:** The replay cache caches the resolved committed state at HEAD; git-log is paid only on cache miss (commit/checkout). Steady-state reads are cheap.

- **Trade-off:** The pointer file `.active-change` is per-machine state outside git. Branch switches don't move it; only the "file doesn't exist" trigger handles divergence.
  **Mitigation:** This is a feature, not a bug — the rotation rule is robust under branch switches, fresh clones, and worktree changes.

## Migration Plan

1. **Land foundation in parallel-write mode** (preserve back-compat): new event DTOs, storage service, replay engine, all wired up but not yet primary.
2. **Add `fleece migrate-events`** that reads legacy files and writes the new format. The legacy files are deleted only after successful write of the new files.
3. **Switch read paths** to the new replay-based storage. Legacy files no longer read.
4. **Switch write paths** to event emission. Legacy files no longer written.
5. **Add `fleece project`** with main-branch protection.
6. **Update `fleece install`** to write the pre-commit hook and the GitHub Action template.
7. **Deprecate `fleece merge`** with a notice pointing at `fleece project`.
8. **Run `fleece migrate-events` on this repository** as part of the same change (commit the new files, delete the legacy ones).
9. **Follow-up release:** delete `Fleece.Core.Models.Legacy` and `IssueMerger` once all consumers have migrated.

**Rollback:** the migration commit is a single git commit. Reverting it restores the legacy files. Any change events produced after migration on a branch would be lost on revert; this is the standard cost of reverting a data-shape migration and is acceptable for a tool of this size.

## Open Questions

None — all design questions raised during exploration have been resolved. If implementation surfaces something new, it will be captured here as the change progresses.
