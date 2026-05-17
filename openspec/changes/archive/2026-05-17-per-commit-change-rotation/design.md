## Context

PR #124 (`event-sourced-issues`) introduced per-session change files keyed by GUID. The session is "branch-on-machine-on-working-tree." The `.fleece/.active-change` pointer is gitignored so it doesn't conflict across branches. Replay uses a three-tier ordering:

```
1. follows-DAG topological sort     (within-branch causality)
2. Commit-ordinal of introducing commit (parallel branches via git log)
3. GUID alphabetical                 (final tiebreak)
```

Investigation in conversation with the user identified two bugs:

**Bug 1 — merge conflicts on shared change files.** Same machine, same worktree: create issue on main, commit → pointer P references file F (committed on main). Branch to feature/a, edit, commit → F is appended on feature/a with new events. Switch back to main, branch to feature/b, edit, commit → F is appended on feature/b with *different* new events. On merge-back, git produces a content conflict in F because both branches diverged from the same ancestor. The test suite hides this by manually deleting the pointer at branch switches.

**Bug 2 — squash of integration branch loses ordering.** On `develop`, two feature branches are regular-merged in, producing parallel chains under `develop`'s commit-ordinal regime (commits D1, D2 introducing the two chains' roots). When `develop` is then squash-merged into `main`, both root files land in the *same* squash commit. Their introducing-commit ordinals on main collapse to one value; the tiebreak falls through to GUID alphabetical.

Both bugs are real, neither is detected by the current test suite, and the architectural decisions are best made together because the fixes interact.

## Goals / Non-Goals

**Goals:**
- Eliminate merge-conflict failure mode on `.fleece/changes/*.jsonl` files by making them immutable once committed.
- Preserve ordering across squash-merge of integration branches by writing merge-marker change files that capture the merge topology at the moment it happens.
- Distribute the merge-marker mechanism through standard git hooks installed by `fleece install` — no custom merge driver.
- Add an explicit warning when replay falls through to GUID alphabetical tiebreak so the failure mode is loud, not silent.
- Cover all of: per-commit rotation, multi-parent follows, merge markers, rebase, cherry-pick, squash of feature branch, squash of integration branch, fast-forward merge.

**Non-Goals:**
- Removing commit-ordinal tiebreak. It remains as a guardrail and degrades gracefully when markers are missing.
- Per-event commit attribution. Each event still lives in a change file rather than carrying its own commit SHA.
- Changing the snapshot format, the projection cadence, or main-branch protection of `fleece project`.
- Supporting arbitrary user-authored edits to committed change files. The invariant is "files immutable after commit" — violations are user error.
- Backporting migration for repositories with in-progress instances of Bug 1. The fix is "run `fleece project` on main once" — manual.

## Decisions

### Decision 1: Rotate when the active file is committed at HEAD

Add one trigger to `EventStore.ResolveActiveOrRotateAsync`:

```
existing: pointer missing → rotate
existing: pointer references missing file → rotate
NEW:      pointer references file that is committed at HEAD → rotate
otherwise: reuse (append to existing)
```

The third trigger uses the `IsFileCommittedAtHead` method on `IEventGitContext` — already implemented in `GitEventContext` (PR #124's task 1.1). The DI scope needs to inject `IEventGitContext` into `EventStore`; today it is only injected into `ReplayEngine`.

**Why this works:**
- First edit after a commit always rotates → each commit's change file is the *only* change file added by that commit.
- Multiple edits within one commit accumulate into the same file (rotation only fires once per commit-boundary crossing).
- Branch switches that change HEAD also change the "is the file committed at HEAD" answer — feature/a sees the file as committed, edits trigger rotation into a fresh GUID. No more shared-GUID collisions.

**Why not "rotate on every edit":**
- Spammier `.fleece/changes/` directory; each `fleece edit` would produce a file.
- Per-commit granularity matches git's natural unit of change and matches what reviewers see.

**Why not "rotate on branch checkout via a post-checkout hook":**
- Requires a hook for correctness; if the hook is missing, the bug returns.
- The "rotate when committed" rule already covers branch checkout because the file appears as committed on whichever branch's history contains it.

### Decision 2: `follows` becomes list-or-scalar

The meta event today is `{"kind":"meta","follows":"<guid|null>"}`. Extend to:

```json
{"kind":"meta","follows":"<guid>"}            // single parent (most common)
{"kind":"meta","follows":["<g1>","<g2>"]}     // merge marker (two or more parents)
{"kind":"meta","follows":null}                // root
```

A custom JSON converter on the `Follows` property accepts both shapes on read and serialises:
- `null` if the list is empty
- the scalar form if length == 1 (smaller diffs, backward-compatible read by older clients during the rollout window)
- the list form if length >= 2

The replay engine's `FileNode` already conceptually has a single `Follows`; refactor to `IReadOnlyList<string>`, count in-degrees per parent, and the existing Kahn's-algorithm topo sort generalises naturally.

**Why list over a separate "link" event kind:**
- Keeps the topo sort in one place. Introducing a separate event kind would require the replay engine to special-case it before sorting.
- Marker files are still ordinary change files — they just happen to have no body events. No new pathway.
- Octopus merges (3+ parents) work without further schema change.

### Decision 3: `fleece link --merge` writes the marker

A new CLI command, expected to be invoked from git hooks:

```
fleece link --merge
   ↓
   1. Check `.git/MERGE_HEAD`. If absent, exit 0 (no-op).
   2. Compute DAG leaves on "our side" (HEAD) and "their side" (MERGE_HEAD).
   3. Generate a fresh GUID, write change_{guid}.jsonl whose meta is
      {"kind":"meta","follows":["<our-leaf>","<their-leaf>"]}
      with no body events.
   4. Stage the new file via `git add`.
```

If there are multiple MERGE_HEADs (octopus merge), include all sides in the follows list.

The command is also runnable manually (e.g., post-hoc reconciliation), but the expected entry point is via hooks.

### Decision 4: Two hooks, not one

| Merge variant | Hook that fires |
|---|---|
| Clean auto-merge | `pre-merge-commit` only |
| Conflict-resolution merge (user runs `git commit`) | `pre-commit` only |
| `git merge --ff-only` | Neither (no commit) — no marker needed; ff doesn't create parallel chains |
| `git merge --squash` | Neither built-in merge hook (it's not a merge commit) — `pre-commit` fires when user runs `git commit` next. The squash itself doesn't need a marker; markers ride along from the source branch's history. |
| `git merge --no-commit` then `git commit` | `pre-commit` only |

Both `pre-merge-commit` and `pre-commit` are installed by `fleece install`, both run the same body:

```sh
if [ -f .git/MERGE_HEAD ]; then
    fleece link --merge || exit 1
fi
git add .fleece/changes/ 2>/dev/null || true
```

**Why not `prepare-commit-msg`:**
- Designed for editing the commit message, not for staging side effects. Some implementations treat `git add` during prepare-commit-msg as staging for the *next* commit. Avoid the surprise.

**Why no `post-merge`:**
- Fires *after* the merge commit is created. The marker would need to live in a separate follow-up commit. The user explicitly asked that the marker ride along.

### Decision 5: Replay warning on GUID-only tiebreak

When `FileNodeReadyComparer` resolves two ready nodes by the GUID-alphabetical fallback (i.e., commit ordinals are equal *and* both non-null *and* in parallel chains), the replay engine writes a one-line warning to stderr identifying the file pair:

```
warning: change files change_aaa.jsonl and change_bbb.jsonl share commit ordinal 7
         and have no merge marker linearizing them. Ordering by GUID is non-semantic;
         consider running `fleece link --merge` against the responsible merge commit.
```

The warning only fires when:
- Two nodes are simultaneously "ready" (in-degree zero) in the topo sort
- Their commit ordinals are equal *and* non-null (the post-squash case)
- No marker file claims both as parents (the marker would have prevented the parallel-ready state by adding a downstream node that depends on both)

Implementation lives in `FileNodeReadyComparer.Compare`, gated by an injected `IWarningSink` so test runs can capture warnings without touching `Console.Error` globally.

### Decision 6: No changes to `fleece project`'s contract

`fleece project` still runs only on main, still collapses everything into the snapshot, still deletes change files. The only difference: it sees more, smaller change files. The existing tests pass without modification (the projection itself is oblivious to per-commit vs per-session rotation).

## Risks / Trade-offs

- **Risk:** Hook bypass — users who commit with `--no-verify` skip the marker creation. Squash of an integration branch built that way would trigger the GUID-fallback warning but not break replay correctness; just yields non-semantic ordering. Mitigation: the warning is loud.

- **Risk:** Old clients that haven't been upgraded write `follows` as a scalar only. They cannot produce markers; if the team has mixed versions during the rollout window, markers from upgraded clients are still readable by old clients (`follows` as list isn't readable — old clients will fail parse). Mitigation: bump the major version and require concurrent upgrade across collaborators, same as the PR #124 rollout.

- **Risk:** `pre-merge-commit` is git 2.24+ (Nov 2019). Older git lacks it. Mitigation: clean auto-merges in old git just won't write a marker; the warning fires. Document the minimum git version in the install command's output.

- **Risk:** More files → larger directory listings and slower `Directory.GetFiles` calls. Mitigation: `fleece project` cadence keeps the working set bounded. Even busy projects see < 200 change files between projections.

- **Risk:** Custom JSON converter on `Follows` complicates the source-generated `EventSourcingJsonContext`. Mitigation: hand-rolled converter for the single property; rest of MetaEvent stays source-generated.

- **Trade-off:** The system is now firmly dependent on `IGitService` not just for the read-path tiebreak but for the write-path rotation decision. Non-git invocations (e.g., Core consumed outside any repo) fall back to `NullEventGitContext`, where `IsFileCommittedAtHead` always returns false → rotation never triggers from this rule → behaviour reverts to the previous "one-file-per-session" model. Document this as expected.

- **Trade-off:** Markers add files for every merge commit, even fast-forward-able merges that the user happens to do as non-FF. Slight overhead; semantically harmless.

## ASCII model summary

```
Commit boundary makes change files immutable.
Merge marker captures topology.

         change_x ←─── follows=null
              │
         (M0 commits change_x)                main branch
              │
              ▼
         change_y ←─── follows="x"
              │
         (M1 commits change_y)
              │
              ▼

Two feature branches off main, both edited:

   main: ─── M1 ─────────────────── (merge a) ───── (merge b) ───
              \                    /              /
   feature/a:  change_a (follows=y) ────────── /
                                              /
   feature/b:  change_b (follows=y) ────────/

At the "merge a" commit, pre-merge-commit fires:
   → writes change_link_a (follows=["y","a"])  ←─ in same commit

At the "merge b" commit, pre-merge-commit fires:
   → writes change_link_b (follows=["link_a","b"])  ←─ in same commit

Replay DAG:
   y → a → link_a
            │
            └→ link_b → (next main commit)
   y → b → link_b

Even after squash of this whole branch into another:
all five files ride along; the link files force linear order. ✓
```
