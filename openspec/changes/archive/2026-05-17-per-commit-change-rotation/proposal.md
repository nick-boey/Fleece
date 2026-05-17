## Why

The event-sourced storage shipped in PR #124 has a latent merge-conflict bug that the test suite hides. When the same machine creates a change file on `main`, commits it, then branches twice and edits on each branch, the gitignored `.active-change` pointer carries the same GUID across both branches. Both branches end up appending different events to the *same* `change_{guid}.jsonl` file. On merge-back, git produces a content conflict on that file — a conflict that no human can sensibly resolve because JSONL events are not line-mergeable.

Today's tests sidestep this by manually deleting the pointer at every branch switch (`EventSourcedLifecycleTests.cs:35-36` is candid: *"Pretend we're a fresh machine: drop the pointer so a new change file rotates."*). Real users have no such discipline, and there is no `post-checkout` hook installed to do it for them.

A second, narrower bug surfaces when an integration branch with parallel chains (multiple feature branches regular-merged in) is then **squash-merged** into the branch where `fleece project` runs. The squash collapses the distinct introducing commits into one, defeating the commit-ordinal tiebreak and leaving ordering to GUID alphabetical — a non-semantic outcome.

This change closes both bugs by making change files **immutable once committed** (rotate on the first edit after the file lands in HEAD) and by writing **merge-marker files** that linearize parallel chains at the moment the merge happens, so the ordering signal rides along with the squash.

## What Changes

- **Rotation rule** in `EventStore.ResolveActiveOrRotateAsync` gains a third trigger: if the active pointer references a file that has been committed at HEAD (per `IEventGitContext.IsFileCommittedAtHead`), rotate to a fresh GUID. The existing "no pointer" and "pointer references missing file" triggers stay. Net effect: each Fleece-touching commit seals its change file; subsequent edits land in a new file whose `follows` points to the just-sealed one.
- **`follows` becomes a list-or-scalar** in the meta event schema. A single-parent chain still writes `"follows":"<guid>"` for minimal diffs; merge-marker files write `"follows":["<our-leaf>","<their-leaf>"]`. The replay engine treats the meta event as having N parents, generalising the existing topological sort.
- **New `fleece link --merge` command** detects `.git/MERGE_HEAD`, computes the DAG leaves on each side, and writes a tiny marker change file whose meta event has multi-parent `follows` and no body events. The marker is a normal staged file, so it is committed as part of the merge commit (no follow-up commit).
- **`fleece install` extends to write two new hooks**: `pre-merge-commit` (clean auto-merges) and `pre-commit` (conflict-resolution merges), both invoking `fleece link --merge` and staging `.fleece/changes/`. The existing pre-commit responsibility (staging) is folded into the same block. `prepare-commit-msg` is *not* used; the two explicit merge hooks cover every commit-creating merge path.
- **Replay warning**: when the topological sort falls through to GUID alphabetical (i.e., two parallel-root files share the same commit ordinal, indicating a squash of an integration branch without a marker), replay emits a single-line warning to stderr identifying the affected files. This is a guardrail: it surfaces situations where a marker should have existed but did not (older history, hook bypass, etc.).
- **Tests** cover: the previously-hidden conflict scenario, per-commit rotation behaviour, multi-parent follows replay, marker generation on real `git merge`, squash-of-integration-branch with markers preserved, rebase with stale follows compensated by commit-ordinal, cherry-pick with dangling follows, fast-forward merges (no marker needed), and the `pre-merge-commit` / `pre-commit` hook installation surface.
- **Documentation**: the `event-sourced-storage` spec gains an ASCII diagram of the DAG/marker/rotation relationship; `CLAUDE.md` gains a "Change files are commit-scoped and immutable" section to anchor reader mental model.

## Capabilities

### Modified Capabilities

- `event-sourced-storage`: rotation rule extended; `follows` becomes list-or-scalar; marker-event semantics added; replay-warning requirement added.
- `git-integration`: `fleece install` writes `pre-merge-commit` (new) and updates the existing `pre-commit` hook to invoke `fleece link --merge`; new `fleece link` command added.

### New Capabilities

None — both capabilities already exist, this change extends them.

## Impact

**Behavioural changes (user-visible):**
- More files in `.fleece/changes/` between projection runs: roughly one per commit instead of one per session. Bounded by the daily `fleece project` cadence; typical projects see tens, not thousands.
- `fleece install` now writes two hooks instead of one. Existing pre-commit hook blocks are updated idempotently via the existing marker-comments mechanism.
- Stderr warning when squash-of-integration-branch happens without prior marker files (only triggers on legacy history; new commits emit markers automatically).

**Migration:**
- Existing single-string `follows` values continue to parse. New files MAY emit list form; readers MUST accept both. No data migration required.
- Repositories with the previously-hidden conflict bug currently in-progress need a one-shot reconciliation: project-and-commit on main flattens any in-flight conflict.

**Code:**
- `src/Fleece.Core/EventSourcing/Events/MetaEvent.cs` — `Follows` becomes `IReadOnlyList<string>` (with a custom converter accepting both shapes; serialiser emits scalar when length==1).
- `src/Fleece.Core/EventSourcing/Services/EventStore.cs` — extend `ResolveActiveOrRotateAsync` rotation trigger; inject `IEventGitContext`.
- `src/Fleece.Core/EventSourcing/Services/ReplayEngine.cs` — `FileNode.Follows` becomes a list; in-degree counted per parent; warning emitted on GUID-only tiebreak.
- `src/Fleece.Core/Services/InstallService.cs` (or equivalent) — write the `pre-merge-commit` hook; update pre-commit block content.
- `src/Fleece.Cli/Commands/LinkCommand.cs` — new command.
- `tests/Fleece.Cli.Integration.Tests/EventSourcedLifecycleTests.cs` — add the scenarios listed in `tasks.md`.
- `openspec/specs/event-sourced-storage/spec.md` and `openspec/specs/git-integration/spec.md` — apply the modified requirements from this change's delta specs.
- `CLAUDE.md` — update the "Event-sourced storage" section.

**Dependencies:** Builds on `event-sourced-issues` (PR #124) and `add-event-sourced-test-coverage` (whose `GitEventContext` becomes load-bearing in the rotation rule rather than only the replay tiebreak).

**Out of scope:**
- Removing the commit-ordinal tiebreak. It remains as a defence in depth and to handle the warning-but-correct case where markers are absent.
- Rewriting the snapshot format.
- Changing the `fleece project` schedule or main-branch protection.
- Per-event commit attribution (option C from exploration). Could be a future change.
- A custom git merge driver. The hook-based approach explicitly avoids that.
