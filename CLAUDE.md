# CLAUDE.md

This file provides guidance for AI assistants working on the Fleece codebase.

## Project Architecture

Fleece is a local-first, **branch-scoped ephemeral working-memory** issue tracker (v4). Issues are
branch-local scratch memory created while working a branch and cleared before the PR merges —
durable work escapes to GitHub Issues via `promote`. It has three components:

### Fleece.Core (Library)
The core library (`src/Fleece.Core/`) contains all business logic and is designed for external consumption:
- **Services**: `IFleeceService`, `IStorageService`, `ISealService`, `IGitHubService` (interface only — impl lives in `Fleece.GitHub`), etc.
- **Models**: `Issue`, enums like `IssueStatus`, `IssueType`
- **Serialization**: JSON handling for issue storage
- **Purity contract**: no I/O statics, everything mockable via `MockFileSystem` (System.IO.Abstractions); no OctoKit reference.

### Fleece.Cli (CLI Application)
The command-line interface (`src/Fleece.Cli/`) is a thin wrapper around Core APIs:
- **Commands**: Each command (e.g., `ListCommand`, `CreateCommand`) delegates to Core services
- **Settings**: Command option definitions (e.g., `ListSettings`)
- **Output**: Formatters for table and JSON output

### Fleece.GitHub (GitHub integration)
`src/Fleece.GitHub/` holds the OctoKit-backed `IGitHubService` implementation so `Fleece.Core`
stays OctoKit-free and the E2E suite can substitute a fake. Token resolution order is
`gh auth token` → `GH_TOKEN`/`GITHUB_TOKEN` → config PAT; the target repo is inferred from
`git remote get-url origin`.

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
| `tests/Fleece.Cli.E2E.Tests` | In-process CLI scenarios against `MockFileSystem` + `TestConsole` + a fake `IGitHubService` (`Fakes/FakeGitHubService.cs`). Hermetic & offline. |
| `tests/Fleece.Cli.Integration.Tests` | Real-disk + real-git scenarios (`commit`). `[NonParallelizable]`. |

GitHub command tests (`promote`/`absorb`/`auth`) drive the fake via `RunWithGitHubAsync` on
`CliScenarioTestBase`, which substitutes `IGitHubService` in DI (the last registration wins).

### Verify snapshots (CLI E2E suite)

`tests/Fleece.Cli.E2E.Tests` uses [`Verify.NUnit`](https://github.com/VerifyTests/Verify) for golden-file snapshots of human-readable CLI output. Snapshots live in `tests/Fleece.Cli.E2E.Tests/Snapshots/` and are committed to the repo.

- **Regenerating snapshots after an intentional output change**: delete the relevant `*.verified.txt` file(s) and rerun `dotnet test`. Verify will produce `*.received.txt`; rename to `*.verified.txt` to accept. Alternatively use the Verify CLI or IDE tooling.
- **Reviewing snapshot diffs in a PR**: treat `.verified.txt` diffs as user-facing output changes — they should be reviewed like any other UX change.
- **JSON output is not snapshotted**: tests parse `--json` output structurally. Only stable human-readable stdout uses snapshots.

### Storage: per-issue append-only logs (v4)

Each issue is **one append-only event log** at `.fleece/issues/<id>.jsonl`. **File order is truth** —
there is no projected snapshot, no `.fleece/changes/` directory, no `follows`-DAG, no merge markers,
no `.active-change` pointer, no `.replay-cache`, and no tombstones.

- The first line of a log is a `create` event; later lines are `set`/`add`/`remove` events.
- Reads enumerate `.fleece/issues/*.jsonl` and replay **each file independently** in its own append
  order. There is no cross-file ordering or tiebreak (`ReplayEngine`).
- Writes diff the new state against current and append events to each issue's own log
  (`EventSourcedStorageAdapter` → `EventStore`). Distinct issues are distinct files, so they never
  conflict; the same issue edited on two branches conflicts on that one file (rare, semantically correct).
- `delete` removes the issue's log file directly. `fleece delete <id>` hard-removes
  `.fleece/issues/<id>.jsonl` at the storage layer (no tombstone, no soft-delete status); the file is
  also removed when an issue is dropped from a save or via `EventStore.DeleteIssueLogAsync`.

Layering: `FleeceService` → `IStorageService` (`EventSourcedStorageAdapter`) →
`IEventSourcedStorageService` → { `EventStore`, `ReplayEngine` }. DI is centralised in
`ServiceCollectionExtensions.AddFleeceCore`.

### Statuses & types (v4)

- **Statuses**: `Open`, `Progress`, `Review`, `Complete`, `Promoted`, `Closed`.
  Active set = `{Open, Progress, Review}` (blocks `seal`); inactive set = `{Complete, Closed, Promoted}`.
  `Promoted` is terminal and means "escaped to a GitHub issue" (carries the `promoted=<#>` keyed tag).
  `Draft` was removed; `Archived` was renamed to `Promoted`.
- **Types**: `Task`, `Bug`, `Chore`, `Feature`, `Verify`. `Idea` was removed.

### Branch lifecycle: `seal` + CI gate

`fleece seal` is the "finish the branch" operation. It refuses (exit 1, listing the offending active
issues) unless **every** issue is inactive. On success it writes
`.fleece/archive/issues_<contenthash>.jsonl` (content-addressed over the canonicalised issue set, so
identical logical state yields one stable name) and removes every `.fleece/issues/*.jsonl`. An empty
issue set is a no-op success. The `.fleece/archive/` audit log is the **only** Fleece issue data
permitted to land on the default branch.

The CI gate installed by `fleece install` (`fleece-ci-gate.yml`) is a tool-free, cross-platform
(bash + PowerShell) workflow that **fails the PR iff `.fleece/issues/` contains any `*.jsonl`**.
`active issues → seal refuses → files remain → CI fails`; `all inactive → seal clears → CI passes`.

### GitHub integration: `promote` / `absorb` / `auth`

- `fleece auth` — reports the resolved login + token source; exits non-zero when unauthenticated.
- `fleece promote <id> [<id>...]` — creates **one** GitHub issue (root title, task-list body of the
  bundle), then sets each issue `Promoted` + tag `promoted=<#>`. Idempotent: already-promoted issues
  are skipped with a warning.
- `fleece absorb #<github-#>` — creates a Fleece issue from the GitHub issue (tag `absorbed-from=<#>`),
  comments on and assigns (does **not** close) the GitHub issue. A bare `absorb 123` (no `#`) performs
  no action and warns.

### `fleece migrate` (one-time legacy bring-forward)

Converts a pre-event-sourced **legacy hashed-file** repository
(`.fleece/issues_{hash}.jsonl` + `tombstones_{hash}.jsonl`) into the v4 per-issue log layout:

1. **Pre-3.0.0 intra-shape fixups** per legacy issue (`LegacyMigration.Migrate`): timestamp backfill,
   `LinkedPR` scalar → keyed-tag fold-in, parent-ref `LastUpdated` backfill, unknown-property strip.
2. **Cross-file merge** of duplicates via `LegacyMerging`.
3. **Projection** to the lean `Issue` shape (drops `*LastUpdate`/`*ModifiedBy`; removed enum members
   like type `Idea` are remapped by `IssueTypeConverter`).
4. **Write per-issue logs** under `.fleece/issues/`; legacy hashed files deleted. No snapshot, no
   `.fleece/changes/`, no tombstone sidecar, no gitignore entries.

Idempotent: a second run exits cleanly with "no migration needed." A legacy **durable
`.fleece/issues.jsonl` snapshot** is NOT auto-migrated — `AutoMigrateInterceptor` prints a
non-destructive warning routing the user to `fleece prime v4-migration` (promote long-running issues
to GitHub, then seal).

### Removed commands

`project`, `merge`, `diff`, `link` (and all merge-marker code), and `clean` are gone in v4, along with
`config defaultBranch`. `seal` supersedes `project` + `clean`.

### `openspec dependencies`

`fleece openspec dependencies` is a pure read-only visualizer: it parses `depends-on:` YAML
frontmatter from `openspec/changes/<name>/dependencies.md` (ignoring HTML-comment soft deps), builds
a change-name DAG, reuses `validate`'s cycle detection to warn on cycles, and renders via the `next`
graph-layout renderer.

## File Locations

| Purpose | Location |
|---------|----------|
| Core service interfaces | `src/Fleece.Core/Services/Interfaces/` |
| Core service implementations | `src/Fleece.Core/Services/` |
| Event-sourced services | `src/Fleece.Core/EventSourcing/Services/` |
| Event DTOs | `src/Fleece.Core/EventSourcing/Events/` |
| Seal service | `src/Fleece.Core/Services/SealService.cs` |
| `IGitHubService` interface + GitHub models | `src/Fleece.Core/Services/Interfaces/IGitHubService.cs`, `src/Fleece.Core/Models/GitHub/` |
| OctoKit-backed GitHub impl | `src/Fleece.GitHub/` |
| Lean issue model | `src/Fleece.Core/Models/Issue.cs` |
| Legacy issue model (migration only) | `src/Fleece.Core/Models/Legacy/` |
| CLI commands | `src/Fleece.Cli/Commands/` |
| CLI settings | `src/Fleece.Cli/Settings/` |
| Core unit tests | `tests/Fleece.Core.Tests/` |

<!-- >>> fleece memory >>> -->
## Fleece: ephemeral working memory

Fleece issues are **branch-local, ephemeral working memory** for the current branch — not
a durable backlog. They track the work in flight and must be cleared before the branch
merges.

**Where does a piece of work go?**

- **Blocks this branch / PR** → a Fleece issue (`fleece create`). Branch-local memory.
- **Non-blocking follow-up, a new feature, or anything that must outlive this branch** → a
  **GitHub issue**, not Fleece. Use `fleece promote <id> [<id>...]` to escalate an existing
  Fleece issue (it becomes `promoted`).

**Before opening a PR**, every Fleece issue must reach an inactive status (`complete`,
`closed`, or `promoted`), or be archived with `fleece seal`. A CI gate fails the PR while
any live issue remains under `.fleece/issues/`.

For commands, hierarchy, statuses, JSON output, and the GitHub round-trip, use the
**`fleece` skill** (installed at `.claude/skills/fleece/`). The `fleece prime` SessionStart
hook surfaces the live count of active issues when the branch is dirty.
<!-- <<< fleece memory <<< -->
