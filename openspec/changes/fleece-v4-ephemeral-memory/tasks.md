# Implementation Tasks

## 1. Storage: per-issue append-only logs (foundation)

- [x] 1.1 Introduce `.fleece/issues/<id>.jsonl` path resolution; remove snapshot path (`.fleece/issues.jsonl`) and `.fleece/changes/` path constants from `EventStore`/`SnapshotStore`/`ReplayCache`
- [x] 1.2 Rewrite `EventStore` writes to append `create`/`set`/`add`/`remove` events to the target issue's own `<id>.jsonl` (new file on create)
- [x] 1.3 Rewrite `ReplayEngine` to load every `.fleece/issues/*.jsonl` and replay each file independently in append order (no DAG, no tiebreak, no merge-marker resolution)
- [x] 1.4 Delete follows-DAG / topological-ordering code, `meta`/`follows` event handling, and `IChangeFileCommitOrder`
- [x] 1.5 Delete `.active-change` pointer (`ActiveChangePointer`) and all active-change selection/rotation logic in `EventStore`
- [x] 1.6 Delete `.replay-cache` (`ReplayCacheFile`, `ReplayCache`, `IReplayCache`) and its read-path integration
- [x] 1.7 Delete tombstones: `.fleece/tombstones.jsonl` writes, tombstone records, and the id-collision retry loop in `CreateAsync`
- [x] 1.8 Change `delete` to remove `.fleece/issues/<id>.jsonl` (drop `hard-delete` event kind and `SnapshotStore` tombstone sidecar)
- [x] 1.9 Update `.gitignore` emission: drop `.active-change`/`.replay-cache` entries; ensure `.fleece/issues/` and `.fleece/archive/` are tracked

## 2. Model: statuses and types

- [x] 2.1 Remove `Draft` from `IssueStatus`; rename `Archived` → `Promoted` across the enum and serialization
- [x] 2.2 Update `IssueStatusExtensions`: `DoneStatuses`/`TerminalStatuses`/`IsTerminal`/`IsDone` to use `{Complete, Closed, Promoted}`; add explicit active-set helper `{Open, Progress, Review}`
- [x] 2.3 Remove `Idea` from `IssueType`
- [x] 2.4 Update any status/type parsing, CLI option converters, and JSON contexts for the new sets

## 3. Remove obsolete commands

- [x] 3.1 Delete `project` (command + settings + `ProjectionService`/`IProjectionService` if unused elsewhere)
- [x] 3.2 Delete `merge` (command + settings + deprecation plumbing)
- [x] 3.3 Delete `diff` (command + settings)
- [x] 3.4 Delete `link` (command + settings + `LinkService`/`ILinkService` + merge-marker writing)
- [x] 3.5 Delete the old `clean` command + `CleanService`/`ICleanService` (superseded by `seal`)
- [x] 3.6 Remove `defaultBranch` from `config` (settings model, `ConfigCommand`, `SettingsService`) and the default-branch refusal it fed

## 4. `seal` command (branch-lifecycle)

- [x] 4.1 Add `SealCommand` + `SealSettings`; refuse with a listed warning unless all issues are inactive
- [x] 4.2 Implement archive write to `.fleece/archive/issues_<contenthash>.jsonl` using a canonicalised content hash (stable across reorderings)
- [x] 4.3 On success remove all `.fleece/issues/*.jsonl`; treat an empty issue set as a no-op success
- [x] 4.4 Register `seal` in DI/command resolution; add `Fleece.Cli.Tests` resolution check

## 5. GitHub integration assembly (`Fleece.GitHub`)

- [x] 5.1 Create `Fleece.GitHub` project referencing OctoKit; define `IGitHubService` (in Core) with the OctoKit impl in the new assembly so Core stays OctoKit-free
- [x] 5.2 Implement token resolution order: `gh auth token` → `GH_TOKEN`/`GITHUB_TOKEN` → config PAT
- [x] 5.3 Implement target-repo inference from `git remote get-url origin`
- [x] 5.4 Add a fake `IGitHubService` for the E2E/Core test suites (keeps them hermetic)

## 6. GitHub commands

- [x] 6.1 Add `auth` command: resolve credentials, print login + token source, non-zero when unauthenticated
- [x] 6.2 Add `promote <id> [<id>...]`: create one GitHub issue (root title, task-list body of the bundle); set each issue `Promoted` + `promoted=<#>` tag; skip+warn if already promoted
- [x] 6.3 Add `absorb #<github-#>`: create Fleece issue from GH issue, tag `absorbed-from=<#>`, comment on + assign (not close) the GH issue; bare `absorb 123` (no `#`) performs no action and warns
- [x] 6.4 Register `auth`/`promote`/`absorb` in DI; add command-resolution tests

## 7. `install` + CI gate (git-integration)

- [x] 7.1 Rewrite pre-commit hook: stage `.fleece/` only (no merge markers); optional non-blocking active-issue-count warning + seal hint
- [x] 7.2 Remove pre-merge-commit / merge-marker hook installation
- [x] 7.3 Replace daily projection Action template with the CI gate workflow: cross-platform (bash + PowerShell) check failing when `.fleece/issues/` contains any `*.jsonl`, requiring no fleece binary on the runner
- [x] 7.4 Update the SessionStart hook + CLAUDE.md block to the v4 ephemeral-memory philosophy

## 8. `prime` + legacy migration

- [x] 8.1 Rewrite default `prime` overview: ephemeral-memory philosophy, resolve/promote/seal-before-PR, active-issue count, v4 status/type sets
- [x] 8.2 Remove OpenSpec-per-change linking section and the dedicated `openspec` prime topic; remove `merge` topic
- [x] 8.3 Add `github` topic (promote/absorb/auth) and `v4-migration` topic (promote long-running legacy issues → GitHub, then seal)
- [x] 8.4 Reframe `migrate` help text for v4 (one-time legacy hashed-file bring-forward only); keep idempotent
- [x] 8.5 Add legacy `.fleece/issues.jsonl` detection that prints the non-destructive `fleece prime v4-migration` warning on any command

## 9. `openspec dependencies` command

- [x] 9.1 Add `openspec` command group with `dependencies` subcommand
- [x] 9.2 Parse `depends-on:` YAML frontmatter from `openspec/changes/*/dependencies.md` (ignore HTML-comment soft deps); build nodes=changes, edges=depends-on
- [x] 9.3 Render via the existing graph-layout `next` renderer
- [x] 9.4 Reuse `validate`'s cycle detection to warn on circular change dependencies

## 10. Tests & docs

- [x] 10.1 Rework `Fleece.Core.Tests` storage tests for per-issue logs; delete DAG/merge-marker/replay-cache/tombstone tests
- [x] 10.2 Add Core tests: `seal` gating + archive, status/type set changes, `delete` file removal
- [x] 10.3 Add E2E tests (fake `IGitHubService`) for `promote`/`absorb`/`auth`; add `openspec dependencies` rendering test
- [x] 10.4 Regenerate affected Verify snapshots (prime output, list output, install output)
- [x] 10.5 Update project `CLAUDE.md` and any user docs to the v4 model; bump version to v4 (major)
- [x] 10.6 Full `dotnet build` + `dotnet test` green
