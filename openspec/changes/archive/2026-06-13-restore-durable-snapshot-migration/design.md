## Context

v4 (commit `198a0a0`) reframed Fleece as ephemeral branch memory and replaced the v3 storage model — a durable snapshot (`.fleece/issues.jsonl`) layered with per-session change files (`.fleece/changes/change_{guid}.jsonl`, ordered by a `follows` DAG) — with one append-only log per issue under `.fleece/issues/`. That rewrite **deleted the reader** for the old model: `SnapshotStore.cs`, `ProjectionService.cs`, `ReplayCache.cs`, and the change-file layering inside `ReplayEngine.cs` (370 → small) are all gone.

Two distinct "legacy" layouts therefore exist, and only one is reachable today:

- **Layout A — hashed files** (`.fleece/issues_{hash}.jsonl` + `tombstones_{hash}.jsonl`): converted by `fleece migrate` / `MigrationService`, auto-run via `AutoMigrateInterceptor`. Reader exists (`issues_*.jsonl` glob). Works.
- **Layout B — durable snapshot + changes** (`.fleece/issues.jsonl` + `.fleece/changes/`): only triggers a warning routing to `fleece prime v4-migration`. The reader was deleted. The guide then says run `fleece list --all`, which reads the (empty) `.fleece/issues/` directory and shows nothing.

`AutoMigrateInterceptor.WarnIfLegacyDurableSnapshotPresent` already detects Layout B by the presence of `.fleece/issues.jsonl`. `MigrationService.IsMigrationNeededAsync` deliberately excludes it (`issues_*.jsonl` does not match the unhashed `issues.jsonl`). The constraint from the v4 design was intentional: durable issues should not silently become ephemeral branch memory — a human must decide which ones escape to GitHub.

## Goals / Non-Goals

**Goals:**
- Make Layout B issues readable and actionable so the documented promote → seal flow works.
- Reuse the existing `MigrationService` → `EventStore.AppendEventsAsync` write path (already used for Layout A) so converted issues become ordinary v4 per-issue logs.
- Preserve the "human consciously runs the migration" intent by keeping durable conversion off the automatic interceptor path — `fleece migrate` auto-detects the layout (no flag), but nothing converts it behind the user's back.
- Land the whole conversion in a single PR.

**Non-Goals:**
- Reviving the full v3 *write* model (snapshot regeneration, `fleece project`, `fleece merge`, replay cache, `.active-change`). We revive only the **read/replay** needed to compute current state once.
- Preserving v3 per-event history into v4. Each converted issue collapses to a single `create` event capturing its final replayed state — identical to how Layout A migration already projects issues.
- Any change to the v4 storage model, read path, or CI gate.

## Decisions

### Decision 1: Convert to per-issue logs (not a read-only lister)

Convert Layout B into `.fleece/issues/<id>.jsonl` rather than adding a display-only `fleece list --legacy` path.

- **Why:** `promote`, `seal`, `show`, and `edit` all sit on top of v4 storage. A read-only lister would let users *see* durable issues but give them no supported way to act on them — we'd have to thread legacy reads through every GitHub command. Converting means the entire existing toolchain works unchanged.
- **Alternative considered (read-only lister):** rejected — strands the user after the listing step and creates a second permanent read path that bypasses v4 storage invariants.

### Decision 2: `fleece migrate` auto-detects both layouts (no flag); the interceptor stays warn-only

Add Layout B handling to `MigrationService` so that running `fleece migrate` detects and converts whichever legacy layout is present (hashed **or** durable) with no new flag. Crucially, **separate two detection paths**:

- The **explicit `fleece migrate` command** detects both Layout A and Layout B and converts.
- The **interceptor's auto-trigger** (`IsMigrationNeededAsync`, run on arbitrary commands) stays **Layout A only**, so it never silently converts the durable layout. The interceptor continues to *warn* on Layout B and routes the user to `fleece prime v4-migration`.

- **Why:** Layout A auto-migration is safe because hashed files are already ephemeral-equivalent. Durable issues are not; auto-converting them on an unrelated command would re-create the exact "silent durable → ephemeral" footgun v4 set out to avoid (and would surprise the user by tripping the CI gate). Requiring a deliberate `fleece migrate` keeps the human in the loop, while dropping the flag keeps the UX simple — there is nothing to remember beyond the verb the warning already names.
- **Alternative considered (a `--durable` flag):** rejected — the user must already discover and run `migrate`; an extra flag adds friction with no safety benefit.
- **Alternative considered (fold Layout B into the interceptor's auto path):** rejected — silent conversion on any command is the footgun above.

### Decision 3: Revive a trimmed read-only replay of snapshot + changes

Re-introduce only the read side of `SnapshotStore` (parse `.fleece/issues.jsonl`) and the change-file layering removed from `ReplayEngine` (apply `.fleece/changes/change_{guid}.jsonl` in `follows` order) to compute each issue's current state. Project that state to the lean `Issue` shape and emit one `CreateEvent` per issue via `EventStore.AppendEventsAsync` — mirroring `MigrationService.BuildCreateEvent`/`ToLeanIssue` for Layout A.

- **Why:** Reuses the established projection/write machinery; the only genuinely new code is the transient replay. Recover it from `198a0a0` rather than re-deriving the `follows` ordering.
- **Alternative considered (snapshot-only, ignore change files):** rejected — would silently drop edits made after the last projection, losing data.

### Decision 4: Delete sources after successful conversion

On success, delete `.fleece/issues.jsonl` and the `.fleece/changes/` directory, matching Layout A (which deletes its hashed sources). Idempotent: a second run finds no Layout B and exits cleanly; the warning stops once `.fleece/issues.jsonl` is gone.

- **Why:** Leaving the snapshot in place would keep the migration warning firing forever and risk a future double-read. Symmetry with existing migration behaviour.

## Risks / Trade-offs

- **Replaying the `follows` DAG incorrectly reorders or drops events** → Recover the layering logic directly from the deleted `ReplayEngine`/`SnapshotStore` in `198a0a0` and cover snapshot-only, snapshot+single-change, and snapshot+multi-change(out-of-order `follows`) cases in `Fleece.Core.Tests`.
- **Destroying durable data on conversion** → Conversion runs only on an explicit `fleece migrate` and writes the v4 logs *before* deleting sources; the converted issues then follow the human-reviewed promote → seal flow. Single-PR scope keeps the before/after diff reviewable.
- **Accidentally auto-converting on an unrelated command** → Assert in tests that `IsMigrationNeededAsync` (the interceptor's auto-trigger) stays false for Layout B, that `AutoMigrateInterceptor` only warns when `.fleece/issues.jsonl` is present, and that the explicit `fleece migrate` command path is the only one that converts Layout B.
- **Stale agent guidance lingers** → The `fleece install` hook content still teaches the removed v3 model; refresh it in the same PR so agents stop being told to run `fleece project`/`fleece merge`.
