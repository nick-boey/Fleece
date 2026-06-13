## Why

Fleece today is a durable, long-term task tracker: issues accumulate forever on `main`, and an event-sourced change-file DAG exists almost entirely to let that data survive squash-merges into a permanent snapshot. The agentic workflows Fleece is now used for don't need a permanent ledger — they need **branch-local working memory**: a scratchpad of medium-term follow-ups an agent can pick up in a refreshed context window, which is then deliberately emptied before the work merges. Durable work belongs in GitHub Issues, not in Fleece. This change inverts Fleece's identity to match how it is actually used, and in doing so deletes the majority of the storage machinery whose only justification (surviving a merge into a long-lived `main` snapshot) no longer exists.

## What Changes

- **BREAKING** — Issues become **ephemeral and branch-scoped**. They are created while working on a branch and MUST be resolved, promoted, or sealed before the PR merges. No Fleece issues land on `main` except a read-only `.fleece/archive/` audit log.
- **BREAKING** — **Storage radically simplified**: replace the event-sourced change-file DAG with one append-only event log per issue at `.fleece/issues/<id>.jsonl`. File order is truth. This **deletes** the follows-DAG, merge markers, `.active-change` pointer, `.replay-cache`, and tombstones. `delete` simply removes the issue's file.
- **BREAKING** — **Statuses**: remove `Draft`; rename `Archived` → `Promoted` (a distinct terminal state meaning "escaped to a GitHub issue", set automatically by `promote`). Inactive set = `{Complete, Closed, Promoted}`; Active set = `{Open, Progress, Review}`.
- **BREAKING** — **Types**: remove `Idea`. Keep `Task`, `Bug`, `Chore`, `Feature`, `Verify`.
- **BREAKING** — **Remove commands**: `project`, `merge`, `diff`, `link` (and all merge-marker code). The old `clean` command is removed entirely.
- **ADD `seal`**: archive all issues into `.fleece/archive/issues_<contenthash>.jsonl` **only if** every issue is inactive; otherwise warn with the remaining active issues. On success, remove live files so `.fleece/issues/` is empty (the "seal the branch before PR" operation; replaces `project` + old `clean`).
- **ADD `promote <id> [<id>...]`**: agent-driven bundling — create one GitHub issue representing one or many Fleece issues, then mark each `Promoted` with keyed tag `promoted=<github-#>`. Idempotent.
- **ADD `absorb #<github-#>`**: create a Fleece issue from a GitHub issue, tag `absorbed-from=<github-#>`, comment on and assign (not close) the GitHub issue. Refuses with a warning if the `#` is omitted.
- **ADD `auth`**: check GitHub authentication via OctoKit.
- **ADD `openspec dependencies`**: parse `depends-on:` frontmatter from `openspec/changes/<name>/dependencies.md`, build a DAG, and render it with the existing graph-layout `next` renderer (reusing `validate`'s cycle detection).
- **MODIFY `install`**: stop installing the daily projection Action and merge-marker git hooks; install a cross-platform GitHub Action (bash + PowerShell, no fleece binary on the runner) that fails the PR when `.fleece/issues/` is non-empty, plus a refreshed SessionStart philosophy block and an optional non-blocking pre-commit active-issue-count warning.
- **MODIFY `prime`**: drop OpenSpec-per-change-linking and merge topics; update statuses/commands; rework `sync` into a `github` topic; add a `v4-migration` topic; emit the new philosophy + incomplete-issue count by default.
- **MODIFY `migrate` / legacy detection**: when an old `.fleece/issues.jsonl` snapshot is present, warn the user to run `fleece prime v4-migration` to move long-running issues to GitHub Issues.
- **REMOVE `config defaultBranch`**: its only consumer (project-on-main refusal) is deleted.
- **ARCHITECTURE**: isolate OctoKit behind an `IGitHubService` interface in its own assembly so `Fleece.Core` stays pure and the E2E suite stays hermetic.

## Capabilities

### New Capabilities
- `branch-lifecycle`: the ephemeral issue lifecycle — `seal`, the `.fleece/archive/` audit log, and the CI gate invariant that a mergeable branch has an empty `.fleece/issues/` directory.
- `github-integration`: `promote`, `absorb`, `auth`, the `IGitHubService` boundary, OctoKit isolation, token resolution order, and target-repo inference from the git remote.
- `openspec-dependencies`: the `openspec dependencies` command that builds and renders a DAG of OpenSpec changes from `depends-on` frontmatter.

### Modified Capabilities
- `event-sourced-storage`: replace the snapshot + change-file DAG with per-issue append-only logs at `.fleece/issues/<id>.jsonl`; remove follows-DAG, merge markers, `.active-change`, `.replay-cache`, tombstones, and the `project`/`merge`/`diff`/`link` commands.
- `issue-authoring`: remove `Draft` status and `Idea` type; rename `Archived` → `Promoted`; define the active/inactive status sets.
- `git-integration`: replace the daily projection Action and merge-marker hooks with the CI gate Action and refreshed install hooks; remove `defaultBranch` config.
- `prime-command`: rewrite onboarding philosophy for ephemeral memory; restructure topics (`github`, `v4-migration`); remove OpenSpec-per-change-linking and merge guidance.
- `legacy-migration`: reframe `migrate` for v4 and add the legacy `.fleece/issues.jsonl` detection warning pointing at `fleece prime v4-migration`.

## Impact

- **Source**: `src/Fleece.Core/EventSourcing/**` (major reduction), `src/Fleece.Core/Models/{IssueStatus,IssueType,ExecutionMode}.cs`, `src/Fleece.Cli/Commands/**` and `Settings/**` (add `seal`/`promote`/`absorb`/`auth`/`openspec`; delete `project`/`merge`/`diff`/`link`/`clean`), `InstallCommand`, `PrimeCommand`, `MigrateCommand`, `ConfigCommand`.
- **New assembly**: `Fleece.GitHub` (OctoKit) behind `IGitHubService`.
- **Dependencies**: add OctoKit; GitHub token resolution (`gh auth token` → `GH_TOKEN`/`GITHUB_TOKEN` → config PAT).
- **Tests**: rework `Fleece.Core.Tests` storage tests; keep `Fleece.Cli.E2E.Tests` hermetic via a fake `IGitHubService`; regenerate affected Verify snapshots.
- **On-disk**: `.fleece/issues/<id>.jsonl`, `.fleece/archive/issues_<hash>.jsonl`; removal of `.fleece/changes/`, `.fleece/tombstones.jsonl`, `.fleece/.active-change`, `.fleece/.replay-cache`, `.fleece/issues.jsonl` snapshot.
- **CI**: new cross-platform GitHub Action installed into consuming repos; **major version bump to v4**.
