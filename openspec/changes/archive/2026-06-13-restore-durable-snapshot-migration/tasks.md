## 1. Revive read-only snapshot + change replay

- [x] 1.1 Recover the deleted `SnapshotStore` read logic from commit `198a0a0` and re-introduce a read-only parser for `.fleece/issues.jsonl` (durable snapshot → v3 issue list)
- [x] 1.2 Recover the change-file layering removed from `ReplayEngine` in `198a0a0`: load `.fleece/changes/change_{guid}.jsonl`, order by `follows`, apply events over the snapshot to compute each issue's current state
- [x] 1.3 Keep this strictly read-only — no snapshot regeneration, no `.active-change`/`.replay-cache` writes, no `project`/`merge` revival
- [x] 1.4 Unit tests: snapshot-only, snapshot + single change file, snapshot + multiple out-of-order `follows` change files (verify ordering + no dropped edits)

## 2. Durable-layout conversion in MigrationService

- [x] 2.1 Add durable-layout detection (`.fleece/issues.jsonl` present) distinct from the hashed-file `issues_*.jsonl` detection
- [x] 2.2 Project replayed durable issues to the lean `Issue` shape and write one `CreateEvent` per issue via `EventStore.AppendEventsAsync` (reuse the existing `BuildCreateEvent`/`ToLeanIssue` pattern)
- [x] 2.3 On success, delete `.fleece/issues.jsonl` and the `.fleece/changes/` directory
- [x] 2.4 Make idempotent: no durable snapshot → no-op clean exit
- [x] 2.5 Unit tests: conversion writes correct per-issue logs, sources deleted, second run is a clean no-op

## 3. No flag; explicit migrate detects, interceptor stays warn-only

- [x] 3.1 Make the explicit `fleece migrate` command detect and convert both layouts (hashed + durable) with no new flag — auto-detect whichever is present
- [x] 3.2 Separate the explicit-command detection from the interceptor's auto-trigger: keep `MigrationService.IsMigrationNeededAsync` (used by `AutoMigrateInterceptor`) Layout-A-only so the durable layout is never auto-converted
- [x] 3.3 Confirm `AutoMigrateInterceptor` still only *warns* on `.fleece/issues.jsonl` presence
- [x] 3.4 Tests: durable snapshot present → interceptor warns but does not convert; bare `fleece migrate` converts it

## 4. Fix the prime v4-migration guide

- [x] 4.1 Update `V4MigrationContent` in `PrimeCommand.cs` to add the conversion as step 0 (run the durable conversion before `fleece list --all`)
- [x] 4.2 Regenerate/adjust any affected `prime` E2E snapshot(s)

## 5. Refresh stale v3 agent guidance

- [x] 5.1 Update the `fleece install` SessionStart hook / agent-guidance content that still describes the removed v3 model (`fleece project`, `fleece merge`, `.fleece/changes/`, `.active-change`, `.replay-cache`) to reflect the v4 ephemeral model
- [x] 5.2 Adjust any tests/snapshots covering the install hook content

## 6. Verify

- [x] 6.1 `dotnet build` and `dotnet test` green across all test projects
- [x] 6.2 `openspec validate restore-durable-snapshot-migration` passes
- [x] 6.3 Manual end-to-end check on a Layout B fixture: warning → convert → `list --all` shows issues → `promote`/`seal` flow works
