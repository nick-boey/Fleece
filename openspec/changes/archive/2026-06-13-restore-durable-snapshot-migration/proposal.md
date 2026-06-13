## Why

The v4 rewrite deleted the reader for the v3 "durable snapshot" layout (`.fleece/issues.jsonl` + `.fleece/changes/change_{guid}.jsonl`) but left the `fleece prime v4-migration` guide telling users to review those issues with `fleece list --all`. That command only enumerates `.fleece/issues/*.jsonl` (the v4 per-issue logs), so for exactly the repositories the migration warning fires on, it shows **nothing** — the durable issues are unreadable and the entire promote → seal flow collapses with no data to act on.

## What Changes

- Extend `fleece migrate` to **auto-detect** and convert the **legacy durable snapshot layout** (`.fleece/issues.jsonl` + `.fleece/changes/`) into v4 per-issue logs (`.fleece/issues/<id>.jsonl`), alongside the hashed-file layout it already handles. No new flag — running `fleece migrate` handles whichever legacy layout is present.
- Revive a read-only replay of the durable snapshot + change files (the deleted `SnapshotStore` + the change-layering removed from `ReplayEngine`) sufficient to compute each issue's current state, then write it as a single `create` event per issue via the existing `EventStore` write path.
- Keep durable conversion off the **automatic** `AutoMigrateInterceptor` path: the interceptor continues to only *warn* on a durable snapshot (never silently convert it on an unrelated command), preserving the deliberate "a human consciously runs the migration" intent. Conversion happens only when the user explicitly runs `fleece migrate`.
- Delete the consumed `.fleece/issues.jsonl` snapshot and `.fleece/changes/` directory after a successful conversion (symmetry with hashed-file migration, which deletes its sources). Conversion remains idempotent.
- Fix the `fleece prime v4-migration` guide to run the conversion as the first step, before `fleece list --all`.
- Refresh the stale `fleece install` SessionStart hook / agent guidance that still describes the removed v3 model (`fleece project`, `fleece merge`, `.fleece/changes/`, `.active-change`, `.replay-cache`) as current.

## Capabilities

### New Capabilities
<!-- None — this extends existing migration behaviour rather than introducing a new capability. -->

### Modified Capabilities
- `legacy-migration`: `fleece migrate` auto-detects and converts the legacy durable snapshot + change-file layout into v4 per-issue logs and removes the consumed sources; the durable-snapshot warning requirement is updated to state the interceptor warns only (never auto-converts).
- `prime-command`: the `v4-migration` topic SHALL instruct the user to convert the durable layout into per-issue logs first, then review/promote/seal.

## Impact

- **Code**: `src/Fleece.Core/EventSourcing/Services/Legacy/MigrationService.cs` (durable-layout detection + conversion, separating the explicit-`migrate` detection from the interceptor's narrower auto-trigger), revived read-only snapshot/change replay (re-introducing trimmed forms of the deleted `SnapshotStore` and `ReplayEngine` change-layering), `src/Fleece.Cli/Interceptors/AutoMigrateInterceptor.cs` (stays warn-only for the durable layout), `src/Fleece.Cli/Commands/PrimeCommand.cs` (`V4MigrationContent`), and `fleece install` hook/agent-guidance content.
- **Tests**: `tests/Fleece.Core.Tests` migration coverage for the durable layout (snapshot-only, snapshot+changes, idempotency, source deletion); E2E coverage that `prime v4-migration` reflects the conversion step.
- **Data**: one-shot, single-PR conversion of `.fleece/issues.jsonl` + `.fleece/changes/` → `.fleece/issues/<id>.jsonl`; sources deleted on success.
- **No new dependencies.**
