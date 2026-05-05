# CLAUDE.md

This file provides guidance for AI assistants working on the Fleece codebase.

## Project Architecture

Fleece is a local-first issue tracking system with two main components:

### Fleece.Core (Library)
The core library (`src/Fleece.Core/`) contains all business logic and is designed for external consumption:
- **Services**: `IIssueService`, `IStorageService`, `ICleanService`, etc.
- **Models**: `Issue`, `Tombstone`, enums like `IssueStatus`, `IssueType`
- **Serialization**: JSON handling for issue storage

### Fleece.Cli (CLI Application)
The command-line interface (`src/Fleece.Cli/`) is a thin wrapper around Core APIs:
- **Commands**: Each command (e.g., `ListCommand`, `CreateCommand`) delegates to Core services
- **Settings**: Command option definitions (e.g., `ListSettings`)
- **Output**: Formatters for table and JSON output

## Key Design Principles

### CLI Commands Should Be Thin Wrappers

CLI commands should:
1. Parse and validate command-line arguments
2. Map arguments to Core API parameters
3. Call Core service methods
4. Format and display results

CLI commands should NOT:
- Contain business logic (put it in Core)
- Implement filtering/searching (use Core's `FilterAsync`/`SearchAsync`)
- Directly manipulate issue data

**Example**: The `list` command's filtering is implemented in `IssueService.FilterAsync`, not in the CLI.

### Core API Should Be Complete

When adding features:
1. First add the capability to Core services
2. Then expose it through CLI commands
3. External consumers (like Homespun) can use Core directly

## Common Tasks

### Adding a New Filter Option

1. Add parameter to `IIssueService.FilterAsync` interface
2. Implement filtering logic in `IssueService.FilterAsync`
3. Add CLI option to relevant Settings classes (e.g., `ListSettings`, `TreeSettings`)
4. Pass the option to `FilterAsync` in the command
5. Add unit tests in `IssueServiceTests`

### Testing

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Fleece.Core.Tests
```

### Test projects

| Project | Scope |
|---------|-------|
| `tests/Fleece.Core.Tests` | Unit tests for `Fleece.Core` services. |
| `tests/Fleece.Cli.Tests` | DI composition + command-resolution checks for the CLI. |
| `tests/Fleece.Cli.E2E.Tests` | In-process CLI scenarios against `MockFileSystem` + `TestConsole` (fast). |
| `tests/Fleece.Cli.Integration.Tests` | Real-disk + real-git scenarios (`commit`, `merge`). `[NonParallelizable]`. |

### Verify snapshots (CLI E2E suite)

`tests/Fleece.Cli.E2E.Tests` uses [`Verify.NUnit`](https://github.com/VerifyTests/Verify) for golden-file snapshots of human-readable CLI output. Snapshots live in `tests/Fleece.Cli.E2E.Tests/Snapshots/` and are committed to the repo.

- **Regenerating snapshots after an intentional output change**: delete the relevant `*.verified.txt` file(s) and rerun `dotnet test`. Verify will produce `*.received.txt`; rename to `*.verified.txt` to accept. Alternatively use the Verify CLI or IDE tooling.
- **Reviewing snapshot diffs in a PR**: treat `.verified.txt` diffs as user-facing output changes — they should be reviewed like any other UX change.
- **JSON output is not snapshotted**: tests parse `--json` output structurally. Only stable human-readable stdout uses snapshots.

### Event-sourced storage (current)

Fleece persists issues as a snapshot plus per-session change files:

- `.fleece/issues.jsonl` — projected snapshot of all issues at the most recent `fleece project` run. The lean `Issue` shape (no per-property `*LastUpdate`/`*ModifiedBy`).
- `.fleece/tombstones.jsonl` — sidecar of hard-deleted issues (`IssueId`, `OriginalTitle`, `CleanedAt`, `CleanedBy`).
- `.fleece/changes/change_{guid}.jsonl` — append-only event files. One file per branch-session-on-machine. First line is a `meta` event with a `follows` pointer (`null` for a root or a predecessor GUID); subsequent lines are `create`/`set`/`add`/`remove`/`hard-delete` events.
- `.fleece/.active-change` — gitignored pointer file naming the current session's change-file GUID.
- `.fleece/.replay-cache` — gitignored cache of the projected state at the current HEAD SHA, used to skip re-replaying committed change files.

Reads load the snapshot, replay all change files in topological order over the `follows`-DAG (with commit-order tiebreak then GUID-alphabetical tiebreak), and answer the query in-memory. Writes append events to the active change file.

#### `fleece project`

Compacts events into the snapshot. Refuses to run anywhere except the configured default branch (`fleece config --set defaultBranch=...`, default `main`). Replays everything, applies 30-day auto-cleanup for soft-deleted issues, writes a fresh snapshot/tombstones, deletes every file under `.fleece/changes/`, and stages the result. Wired to a daily GitHub Action template by `fleece install`.

#### `fleece merge` (deprecated)

Prints a deprecation notice on stderr pointing at `fleece project`. Existing behavior is preserved for one release cycle and will be removed.

#### `fleece migrate`

The canonical "bring my data up to the current schema" command. Runs a pipeline of schema migrations end-to-end:

1. **Pre-3.0.0 intra-shape fixups** on each parsed legacy issue (`LegacyMigration.Migrate`): timestamp backfill, `LinkedPR` scalar → `hsp-linked-pr=<n>` keyed-tag fold-in, parent-ref `LastUpdated` backfill, unknown-property strip.
2. **Cross-file merge** of legacy hashed files via `LegacyMerging` (uses the per-property timestamps populated in step 1 to resolve conflicts).
3. **Projection** to the lean `Issue` shape — drops `*LastUpdate`/`*ModifiedBy` metadata (it survives in git history if anyone needs it).
4. **Write event-sourced layout** — `.fleece/issues.jsonl`, `.fleece/tombstones.jsonl`, `.fleece/changes/`, gitignore entries; legacy `issues_{hash}.jsonl` and `tombstones_{hash}.jsonl` deleted.

Idempotent: a second run on an already-migrated repo exits cleanly with "no migration needed." Future schema migrations on the lean `Issue` extend this pipeline rather than introducing new commands.

### Clean Command and Tombstones

The `fleece clean` command soft-or-hard deletes issues. Soft-deletes go through the standard event path (`set status=Deleted`); hard-deletes emit `hard-delete` events and the tombstones land in `.fleece/tombstones.jsonl` after the next `fleece project`.

Key details:
- **Tombstone records** store `IssueId`, `OriginalTitle`, `CleanedAt`, and `CleanedBy`. The title is preserved for historical reference.
- **ID collision prevention**: IDs are random GUIDs (first 5 bytes, Base62-encoded to 6 chars). When `CreateAsync` generates an ID that matches an existing issue or tombstone, it retries with a new random ID, up to 10 attempts.
- **Reference stripping**: By default, `clean` removes dangling `LinkedIssues` and `ParentIssues` references from remaining issues. Use `--no-strip-refs` to skip this.
- **Optional flags**: `--include-complete`, `--include-closed`, `--include-archived` extend cleaning beyond just `Deleted` issues.
- **Core service**: `ICleanService` / `CleanService` contains all business logic. The CLI `CleanCommand` is a thin wrapper.

## File Locations

| Purpose | Location |
|---------|----------|
| Core service interfaces | `src/Fleece.Core/Services/Interfaces/` |
| Core service implementations | `src/Fleece.Core/Services/` |
| Event-sourced services | `src/Fleece.Core/EventSourcing/Services/` |
| Event DTOs | `src/Fleece.Core/EventSourcing/Events/` |
| Lean issue model | `src/Fleece.Core/Models/Issue.cs` |
| Legacy issue model (migration only) | `src/Fleece.Core/Models/Legacy/` |
| CLI commands | `src/Fleece.Cli/Commands/` |
| CLI settings | `src/Fleece.Cli/Settings/` |
| Core unit tests | `tests/Fleece.Core.Tests/` |
