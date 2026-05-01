## Why

The current per-machine hashed `.fleece/issues_{hash}.jsonl` storage produces large, opaque PR diffs whenever issues change: an entire file is added or deleted per branch-machine, and the diff carries no semantic indication of which issue properties actually changed. Reviewers cannot see "Alice set the title and added a tag"; they only see "the whole file moved." This makes Fleece data hostile to code review and erodes the value of tracking it in git.

Migrating to an event-sourced model — a single projected `issues.jsonl` snapshot plus per-session `change_{guid}.jsonl` event files — produces minimal, human-readable per-commit diffs (each commit shows the events that were appended), eliminates whole-file churn, and gives us a clean foundation for periodic compaction via a `fleece project` command on the main branch.

## What Changes

- **BREAKING**: Replace `.fleece/issues_{hash}.jsonl` files with a single `.fleece/issues.jsonl` snapshot.
- **BREAKING**: Replace `.fleece/tombstones_{hash}.jsonl` files with a single `.fleece/tombstones.jsonl` sidecar.
- **BREAKING**: Remove per-property `*LastUpdate` and `*ModifiedBy` fields from the persisted issue shape; per-event timestamps and authors live in change files instead.
- Add `.fleece/changes/change_{guid}.jsonl` event files. One file per branch-session-on-machine; each file's first line is a `meta` event carrying a `follows` pointer to its predecessor GUID (or null), forming a DAG that orders events under squash-merges.
- Add `.fleece/.active-change` (gitignored) pointer file tracking the current active GUID.
- Add `.fleece/.replay-cache` (gitignored) cache of the projected state at HEAD, so reads only need to replay the active uncommitted file.
- Add `fleece project` command. Refuses to run anywhere except the main branch. Replays all change files into `issues.jsonl`, deletes the change files, and runs auto-cleanup for soft-deleted issues older than 30 days (writing tombstones).
- Rewrite all read paths (`list`, `show`, `next`, `tree`, etc.) to load the snapshot, replay all change files in topo order, and answer queries against the in-memory result. The snapshot is no longer the source of truth — the snapshot + events are.
- Rewrite all write paths (`create`, `edit`, `delete`, `move`, `dependency`, etc.) to append events to the active change file rather than rewriting issue files.
- Extend `fleece install` to install a pre-commit git hook that stages the active change file, and to write a daily GitHub Action template that runs `fleece project`.
- Extend the existing `fleece migrate` command to convert legacy `issues_{hash}.jsonl` + `tombstones_{hash}.jsonl` into the new `issues.jsonl` + `tombstones.jsonl` as the final stage of a pipeline that *first* runs the pre-3.0.0 intra-shape fixups (timestamp backfill, `LinkedPR` → `Tags` fold-in, parent-ref `LastUpdated` backfill, unknown-property strip) on the legacy data before cross-file merging and projection to the lean shape. `fleece migrate` remains the single user-facing command for "bring my data up to the current schema." Migration is lossy: per-property timestamps and modifier history are dropped during projection to the lean shape (they remain in git history if anyone needs them).
- **BREAKING (interface)**: Remove `MigrateAsync` and `IsMigrationNeededAsync` from `IFleeceService`. After this change `IFleeceService` no longer participates in migration; the migration concern lives entirely on the migration service consumed by `MigrateCommand`.
- **BREAKING**: Move today's `Issue` model (with all `*LastUpdate`/`*ModifiedBy` fields) into a `Fleece.Core.Models.Legacy` namespace, used only by the migration command. The new `Fleece.Core.Models.Issue` is the lean form.
- Deprecate `fleece merge` (the property-level timestamp-based merger). It emits a notice pointing at `fleece project`. `IssueMerger` is removed once migration is rolled out.

## Capabilities

### New Capabilities

- `event-sourced-storage`: the event-sourced persistence layer — snapshot file, change files, active-pointer rotation, replay engine, replay cache, and the `fleece project` command (including main-branch protection and 30-day soft-delete auto-cleanup).
- `legacy-migration`: one-time migration capability that converts legacy hashed `issues_{hash}.jsonl` and `tombstones_{hash}.jsonl` files into the new event-sourced layout.
- `git-integration`: pre-commit hook installation (auto-stages active change file) and daily GitHub Action template (runs `fleece project` on `main`), both wired through `fleece install`.

### Modified Capabilities

- `issue-authoring`: write paths now emit events to the active change file rather than mutating a per-machine issues file. Reads of just-written state must reflect those events via in-memory replay, but the user-facing CLI behavior of `create` and `edit` is unchanged.

## Impact

**Code:**
- `src/Fleece.Core/Models/Issue.cs` — slimmed down (timestamps removed); old shape moves to `Fleece.Core.Models.Legacy`.
- `src/Fleece.Core/Models/` — new event DTOs (`MetaEvent`, `CreateEvent`, `SetEvent`, `AddEvent`, `RemoveEvent`, `HardDeleteEvent`, plus a discriminated base or union).
- `src/Fleece.Core/Services/JsonlStorageService.cs` and `SingleFileStorageService.cs` — rewritten or replaced by a new event-aware storage service.
- `src/Fleece.Core/Services/IssueMerger.cs` — deleted post-migration.
- `src/Fleece.Core/Services/FleeceService.cs` and `FleeceInMemoryService.cs` — updated to drive event emission on writes and replay on reads.
- `src/Fleece.Cli/Commands/InstallCommand.cs` — extended with git hook + GH Action template installation.
- `src/Fleece.Cli/Commands/MergeCommand.cs` — becomes a deprecation shim pointing at `fleece project`.
- `src/Fleece.Cli/Commands/MigrateCommand.cs` — extended to drive the unified migration pipeline (pre-3.0.0 fixups → cross-file merge → projection → event-sourced write). No new sibling command.
- New `src/Fleece.Cli/Commands/ProjectCommand.cs` for `fleece project`.
- `src/Fleece.Core/Services/Interfaces/IFleeceService.cs` — `MigrateAsync` and `IsMigrationNeededAsync` removed. `FleeceService` and `FleeceInMemoryService` lose their no-op stubs.

**On-disk format (BREAKING):**
- All existing `.fleece/issues_{hash}.jsonl` and `.fleece/tombstones_{hash}.jsonl` files are deleted by the migration; new `.fleece/issues.jsonl`, `.fleece/tombstones.jsonl`, and `.fleece/changes/` directory take their place.
- `.gitignore` additions: `.fleece/.active-change`, `.fleece/.replay-cache`.

**Workflow:**
- Teams using Fleece must adopt rebase-style merging (or accept that PRs may need to rebase onto the daily projection commit on main). Squash-merging works correctly thanks to the `follows`-pointer DAG.
- Direct pushes to main remain supported; the daily projection action will fold them in.

**Out of scope (follow-ups):**
- Pre-merge GitHub Action that flattens branch change files before squash. The `follows`-pointer design makes this unnecessary for correctness; it would only be a polish step.
- Auto-cleanup for `Closed`/`Archived` statuses (only `Deleted` is auto-cleaned at 30 days in this change).
