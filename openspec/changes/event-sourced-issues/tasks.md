## 1. Foundation: Event DTOs and Models

- [ ] 1.1 Move existing `Fleece.Core.Models.Issue` and related DTOs (`IssueDto`, `IssueShowDto`, `IssueSummaryDto`, `IssueSyncDto`, `Tombstone`) into a new `Fleece.Core.Models.Legacy` namespace, renaming with a `Legacy` prefix where ambiguity would arise (e.g., `LegacyIssue`) — **deferred to PR 2**: moving the canonical `Issue` namespace breaks every consumer in the codebase, contradicting the "PR 1 is additive" goal. The new lean type lives at `Fleece.Core.EventSourcing.Issue` for now; PR 2 promotes it to `Fleece.Core.Models.Issue` and pushes the legacy shape down at the same time.
- [x] 1.2 Create the new lean `Fleece.Core.Models.Issue` record with: `Id`, `Title`, `Description`, `Status`, `Type`, `LinkedPR`, `LinkedIssues`, `ParentIssues`, `Priority`, `AssignedTo`, `Tags`, `WorkingBranchId`, `ExecutionMode`, `CreatedBy`, `CreatedAt`, `LastUpdate` (no `*LastUpdate`/`*ModifiedBy` per-property fields) — landed at `Fleece.Core.EventSourcing.Issue`; will be promoted to `Fleece.Core.Models.Issue` in PR 2.
- [x] 1.3 Create event DTOs under `Fleece.Core.Models.Events/`: `IssueEvent` (base/discriminated union), `MetaEvent`, `CreateEvent`, `SetEvent`, `AddEvent`, `RemoveEvent`, `HardDeleteEvent` — landed at `Fleece.Core.EventSourcing.Events`.
- [x] 1.4 Configure `System.Text.Json` polymorphic deserialization for `IssueEvent` keyed by the `kind` discriminator, with explicit handling for unknown kinds (throw with file/line context)
- [x] 1.5 Add unit tests for event serialization round-trips (each kind, including `parentIssues` element shape and null scalars)

## 2. Event Store: Active File and Rotation

- [x] 2.1 Create `IEventStore` interface in `Fleece.Core/Services/Interfaces/` with methods to read all change files, read the active file, append events, and rotate — landed at `Fleece.Core.EventSourcing.Services.Interfaces.IEventStore`.
- [x] 2.2 Implement `EventStore` in `Fleece.Core/Services/`: reads `.fleece/.active-change`, applies the rotation rule (pointer missing OR file missing on disk → fresh GUID), scans existing change files to build the follows-DAG, finds the leaf, writes meta event, writes pointer
- [x] 2.3 Implement append-event behavior: write a single JSON line per event, ensure trailing newline, fsync-or-equivalent for crash safety (best-effort `Stream.FlushAsync` — `IFileSystem` does not expose true fsync)
- [x] 2.4 Add unit tests for rotation triggers: missing pointer, missing file, file present (reuse), file in HEAD (still reuse), branch switch (file vanishes from working tree → rotation)
- [x] 2.5 Add unit tests for follows-DAG leaf selection (single chain, multiple roots, multiple leaves, GUID-alphabetical tiebreak)

## 3. Replay Engine

- [x] 3.1 Create `IReplayEngine` interface returning the in-memory issue dictionary given a snapshot path and a change-files directory — interface takes initial state + change-file paths so it stays pure of file I/O concerns.
- [x] 3.2 Implement `ReplayEngine`: load snapshot, list change files, build follows-DAG from first lines, topologically sort, apply tiebreaks (commit order via `IGitService`, then GUID alphabetical), apply events in line order — commit-order tiebreaks come from `IChangeFileCommitOrder`; PR 2 will plug in the git-backed implementation.
- [x] 3.3 Implement event application: `create` inserts; `set` overwrites scalar (handles null); `add` appends to array idempotently; `remove` drops from array idempotently (with `parentIssues` matched by `parentIssue` key only); `hard-delete` drops the row
- [x] 3.4 Handle dangling `follows` pointers: treat as `null` (root) with a debug log (degraded silently to `null` for now; `ILogger` integration deferred)
- [x] 3.5 Surface a clear diagnostic on unknown `kind` values (file path + line number) — `UnknownEventKindException` carries `FilePath` + `LineNumber`
- [x] 3.6 Add unit tests covering: simple linear replay, squash equivalence (same events ordered correctly post-squash), multi-machine squash with follows pointers, parallel-branch tiebreak via commit order, dangling follows, idempotency of `add`/`remove`, `parentIssues` match-by-key

## 4. Replay Cache

- [x] 4.1 Define cache file format at `.fleece/.replay-cache`: HEAD SHA + serialized committed-state dictionary
- [x] 4.2 Implement cache read/write in `EventStore` (or a sibling `IReplayCache` service) — sibling `IReplayCache` / `ReplayCache`.
- [x] 4.3 Wire the replay engine to: load cache → if HEAD matches, replay only the active uncommitted file on top → otherwise full replay + cache rewrite — wiring lives in `EventSourcedStorageService.GetIssuesAsync`; the `IReplayEngine` itself remains pure.
- [ ] 4.4 Add `.fleece/.replay-cache` to the bootstrapped `.gitignore` template — **deferred to PR 3** (requires extending `InstallCommand` / migration).
- [x] 4.5 Add unit tests for cache hit (HEAD unchanged) and cache miss (HEAD advanced)

## 5. Snapshot I/O

- [x] 5.1 Implement snapshot reader: parse `.fleece/issues.jsonl` into a dictionary using the new lean `Issue` model
- [x] 5.2 Implement snapshot writer: serialize the in-memory dictionary to `.fleece/issues.jsonl` (sorted by `id` for stable diffs); also write `.fleece/tombstones.jsonl` from a tombstone list
- [x] 5.3 Implement tombstones reader/writer
- [x] 5.4 Add unit tests for round-trip snapshot serialization and stable ordering

## 6. New Storage Service

- [x] 6.1 Create `IEventSourcedStorageService` (or repurpose the existing `IStorageService` interface) exposing the read/write surface that today's `IFleeceService` consumes (load issues, save issue, etc.) — new sibling interface; `IFleeceService` adapter comes in PR 2.
- [x] 6.2 Implement `EventSourcedStorageService`: reads via snapshot+replay; writes by appending events
- [ ] 6.3 Replace `JsonlStorageService` and `SingleFileStorageService` registrations in DI with the new service. Delete the old implementations once no callers remain. — **PR 2** (this is the wire-up step that flips the system).
- [x] 6.4 Add integration tests: create-then-read on a fresh repo, edit-then-read across a simulated commit boundary, round-trip every event kind — covered by `EventSourcedStorageServiceTests` (in-process, in-memory file system).

## 7. Wire Up Write Paths

- [ ] 7.1 `CreateCommand`: emit a single `create` event with full property bag from CLI flags
- [ ] 7.2 `EditCommand`: diff the requested updates against the current in-memory state and emit one `set` per scalar change, plus `add`/`remove` per array delta
- [ ] 7.3 `DeleteCommand` (soft-delete): emit `set status="Deleted"`
- [ ] 7.4 `CleanCommand` (hard-delete): emit `hard-delete` per issue removed; tombstones written via the projection path, not directly here on non-main branches (clean still operates locally for now; document the rebase expectation)
- [ ] 7.5 `MoveCommand` and `DependencyCommand`: emit `add`/`remove` events for `parentIssues`
- [ ] 7.6 `StatusCommands` (the various status-change shortcuts): emit `set status=<value>` events
- [ ] 7.7 Verify that no write command bypasses the event store and writes directly to `.fleece/issues.jsonl`

## 8. Wire Up Read Paths

- [ ] 8.1 `ListCommand`, `ShowCommand`, `NextCommand`, `SearchCommand`, `DiffCommand`, `ValidateCommand`, `tree`/`status`/`graph` views: ensure all reads go through `IEventSourcedStorageService` (which replays internally)
- [ ] 8.2 Verify that no read command reads `.fleece/issues_*.jsonl` legacy files
- [ ] 8.3 Update `FleeceService` and `FleeceInMemoryService` to use the new storage service

## 9. `fleece project` Command

- [ ] 9.1 Create `ProjectCommand` and `ProjectSettings` in `src/Fleece.Cli/Commands/`
- [ ] 9.2 Implement default-branch detection (from git config or settings) and refuse to run on any other branch with a clear error
- [ ] 9.3 Implement projection: replay → write new snapshot → write tombstones (including new auto-cleanup tombstones) → delete all change files in `.fleece/changes/`
- [ ] 9.4 Implement 30-day auto-cleanup for soft-deleted issues, with `cleanedAt` and `cleanedBy` recorded in `tombstones.jsonl`
- [ ] 9.5 Stage all changes (`git add`) but do not commit (let CI or the user commit)
- [ ] 9.6 Ensure idempotency: repeated runs with no events between produce no diff
- [ ] 9.7 Register the command in DI and wire `Spectre.Console.Cli` routing
- [ ] 9.8 Add E2E tests covering: refuses on non-main, compacts events on main, idempotent on repeat, auto-cleans soft-deleted >30d, leaves <30d soft-deleted intact

## 10. Deprecate `fleece merge`

- [ ] 10.1 Update `MergeCommand` to print a deprecation notice on stderr pointing at `fleece project`
- [ ] 10.2 Keep existing merge behavior functional for one release cycle (still callable, still works, just deprecated)
- [ ] 10.3 Add a test asserting the deprecation notice is printed

## 11. `fleece install` Extensions

- [ ] 11.1 Extend `InstallCommand` to write `.git/hooks/pre-commit` (creating it if absent, appending a fleece block between marker comments if present), making it executable
- [ ] 11.2 The hook runs `git add .fleece/changes/` and (when on the default branch) `git add .fleece/issues.jsonl .fleece/tombstones.jsonl`
- [ ] 11.3 Make the install idempotent (single fleece block even on repeat runs)
- [ ] 11.4 Detect github.com remote; if present and `.github/workflows/` exists, write `.github/workflows/fleece-project.yml` with daily cron + workflow_dispatch + checkout + install fleece + `fleece project` + commit + push steps
- [ ] 11.5 Skip writing the workflow if it already exists; print a warning
- [ ] 11.6 Add `.fleece/.active-change` and `.fleece/.replay-cache` to `.gitignore` if missing
- [ ] 11.7 Add E2E tests: fresh install creates expected files, repeat install is idempotent, existing pre-commit hook is preserved

## 12. Migration Command

- [ ] 12.1 Decide: extend `MigrateCommand` or add a new `migrate-events` subcommand. Implement chosen approach.
- [ ] 12.2 Read all `.fleece/issues_*.jsonl` files using `Fleece.Core.Models.Legacy.LegacyIssue`
- [ ] 12.3 Reuse `IssueMerger` to reconcile per-issue conflicts across files
- [ ] 12.4 Project the merged set into the new lean `Issue` shape; write `.fleece/issues.jsonl`
- [ ] 12.5 Read all `.fleece/tombstones_*.jsonl` files; union by issue ID; write `.fleece/tombstones.jsonl`
- [ ] 12.6 Delete legacy `issues_*.jsonl` and `tombstones_*.jsonl` files
- [ ] 12.7 Create `.fleece/changes/` directory; ensure `.gitignore` entries are added
- [ ] 12.8 Make the migration idempotent: detect already-migrated repos and exit cleanly
- [ ] 12.9 Add E2E tests: legacy → new conversion, conflict reconciliation, tombstone union, idempotent on already-migrated, gitignore additions

## 13. Run Migration on This Repository

- [ ] 13.1 Execute the migration command against `.fleece/` in this repository
- [ ] 13.2 Verify the resulting `.fleece/issues.jsonl` reflects all current issues
- [ ] 13.3 Commit the migration result as part of the change PR
- [ ] 13.4 Verify CI passes on the post-migration tree

## 14. Documentation and Cleanup

- [ ] 14.1 Update `CLAUDE.md` to describe the event-sourced layout, replay model, and `fleece project` semantics
- [ ] 14.2 Update `fleece prime` topic content to reflect the new architecture (especially `merge` → `project` guidance)
- [ ] 14.3 Update README and any user-facing docs covering storage layout
- [ ] 14.4 Remove or archive references to `IssueMerger` and the legacy storage services from non-migration code paths
- [ ] 14.5 File a follow-up Fleece issue to delete `Fleece.Core.Models.Legacy`, `IssueMerger`, and `MergeCommand` after the migration is rolled out

## 15. Verification and Test Coverage

- [ ] 15.1 Verify all existing E2E snapshot tests pass post-migration (or update snapshots intentionally)
- [ ] 15.2 Add an integration test simulating the full lifecycle: migrate → create on feature branch → commit → switch branch → create different events → switch back → verify correct in-memory state
- [ ] 15.3 Add an integration test simulating squash-merge: write events on feature branch → squash to main → run `fleece project` → verify final state matches pre-squash projection
- [ ] 15.4 Add an integration test simulating multi-machine squash: two parallel "machines" with different active-change pointers writing to the same branch → squash → verify follows-DAG ordering produces correct result
- [ ] 15.5 Confirm the daily GitHub Action template runs cleanly on a test fork (or document the manual smoke test)
