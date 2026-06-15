# Fleece

**Branch-scoped ephemeral working memory for the work in flight.**

Fleece is a local-first issue tracker that holds **branch-local scratch memory** — the
tasks, bugs, and notes you generate while working a branch. These issues live as JSONL files in your
repository, so they are versionable, portable, and always available offline. Unlike a durable
backlog, Fleece issues are meant to be **cleared before the branch's PR merges**: finish them,
or escape the durable ones to GitHub Issues with `promote`, then `seal` the branch.

## Philosophy

- **Branch-local & ephemeral**: issues track *this branch's* work and must be resolved before merge
- **Durable work escapes to GitHub**: anything that must outlive the branch becomes a GitHub issue
- **Local-first**: issues live in your repository, not a remote server
- **Version-controlled**: each issue is an append-only log; diffs are small and semantic
- **No server, no database**: just files
- **AI-friendly**: built-in Claude Code integration (hook + installable skill)

### Where does a piece of work go?

| The work… | Goes to… |
|-----------|----------|
| Blocks this branch / PR | A **Fleece issue** (`fleece create`) — branch-local memory |
| Is a follow-up, new feature, or must outlive this branch | A **GitHub issue** — use `fleece promote <id>` to escalate |

Before opening a PR, every Fleece issue must reach an **inactive** status (`complete`,
`closed`, or `promoted`), or the branch must be archived with `fleece seal`. A CI gate fails
the PR while any live issue remains under `.fleece/issues/`.

## Installation

Fleece is distributed as a .NET tool.

```bash
# Global install
dotnet tool install --global Fleece.Cli

# Or local to a project
dotnet tool install Fleece.Cli
```

## Quick Start

```bash
# Create an issue (--title and --type required; default status is open)
fleece create --title "Add user authentication" --type feature

# Move it through the workflow (status shortcut commands)
fleece progress abc123        # → progress
fleece review abc123          # → review
fleece complete abc123        # → complete

# List active issues (terminal statuses hidden by default)
fleece list
fleece list --all             # include complete/closed/promoted
fleece list --tree            # parent-child hierarchy
fleece list --next            # task graph with execution order

# Finish the branch: archive everything and clear .fleece/issues/
fleece seal
```

The `.fleece/` directory is created automatically with your first issue.

## Commands

### Authoring

```bash
fleece create -t "Fix login bug" -y bug -p 1 -d "Fails on Safari"
fleece edit abc123 --status review --linked-pr 42
fleece show abc123 --json
fleece search "authentication"
fleece delete abc123          # hard-removes .fleece/issues/abc123.jsonl
```

`create`/`edit` flags: `-t/--title`, `-y/--type`, `-d/--description`, `-s/--status`,
`-p/--priority`, `--tags`, `--linked-pr`, `--linked-issues`, `--parent-issues`, `-a/--assign`.

### Hierarchy & ordering

```bash
fleece create -t "Sub-task" -y task --parent-issues abc123:aaa   # parent:lex-order
fleece dependency --parent abc123 --child def456                 # link existing issues
fleece move abc123 --up                                          # reorder among siblings
fleece next                                                      # what to work on next
fleece list --tree                                              # parent-child tree
fleece list --next                                             # bottom-up task graph
```

### Branch lifecycle

```bash
fleece seal      # archive all issues → .fleece/archive/ and clear .fleece/issues/
```

`seal` refuses (exit 1) while **any** issue is active (`open`, `progress`, `review`), listing
the offenders. On success it writes a content-addressed
`.fleece/archive/issues_<contenthash>.jsonl` audit log and removes every `.fleece/issues/*.jsonl`.
An empty issue set is a no-op success. The `.fleece/archive/` log is the **only** Fleece data
permitted to land on the default branch.

### GitHub round-trip

```bash
fleece auth                      # report resolved login + token source
fleece promote abc123 def456     # → one GitHub issue, marks each issue promoted
fleece absorb #123               # create a Fleece issue from a GitHub issue
```

- `promote` creates **one** GitHub issue (root title + task-list body of the bundle), then sets
  each issue to `Promoted` with a `promoted=<#>` tag. Idempotent — already-promoted issues are
  skipped.
- `absorb #<n>` creates a Fleece issue tagged `absorbed-from=<n>`, then comments on and assigns
  the GitHub issue (it does **not** close it). A bare `absorb 123` (no `#`) does nothing and warns.
- Token resolution order: `gh auth token` → `GH_TOKEN`/`GITHUB_TOKEN` → config PAT. The target
  repo is inferred from `git remote get-url origin`.

### Setup & maintenance

```bash
fleece install                   # Claude Code hook + CI gate workflow
fleece migrate                   # one-time bring-forward from a legacy (pre-v4) repo
fleece commit --ci               # commit .fleece/ changes and let CI run
fleece validate                  # check issue dependencies for cycles
fleece config --list             # view/modify configuration
fleece openspec dependencies     # render a DAG of OpenSpec changes
```

Run any command with `-h` for full options. Use `--json` (or `--json-verbose`) on most commands
for machine-readable output, and `fleece list --one-line` for `<id> <status> <type> <title>`.

## Issue Types

| Type | Description |
|------|-------------|
| `task` | General work item |
| `bug` | Defect to fix |
| `chore` | Maintenance / housekeeping |
| `feature` | New functionality |
| `verify` | Checkpoint that confirms grouped child work is complete |

## Issue Statuses

| Status | Set | Description |
|--------|-----|-------------|
| `open` | active | Created, not started |
| `progress` | active | Being worked |
| `review` | active | Awaiting review |
| `complete` | inactive | Work finished |
| `closed` | inactive | Abandoned / won't fix |
| `promoted` | inactive | Escaped to a GitHub issue (terminal; carries `promoted=<#>`) |

**Active** statuses (`open`, `progress`, `review`) block `seal`. The branch cannot be sealed —
and the CI gate fails the PR — until every issue is inactive.

Each status has a shortcut command (`fleece open|progress|review|complete|closed <id>...`) as an
alias for `fleece edit <id> --status <status>`.

## Storage Format

Each issue is **one append-only event log** at `.fleece/issues/<id>.jsonl`. **File order is
truth** — there is no projected snapshot, no change-file DAG, and no tombstones.

```
.fleece/
  issues/
    <id>.jsonl          # one append-only log per issue (create, then set/add/remove events)
  archive/
    issues_<hash>.jsonl # content-addressed audit log written by `seal` (read-only)
```

- The first line of each log is a `create` event; later lines are `set`/`add`/`remove` events.
- Reads enumerate `.fleece/issues/*.jsonl` and replay **each file independently** in its own
  append order — no cross-file ordering.
- Writes diff the new state against current and append events to that issue's own log. Distinct
  issues are distinct files, so they never conflict; the same issue edited on two branches
  conflicts on that one file (rare, and semantically correct).
- `delete` removes the issue's log file directly — no soft-delete, no tombstone.

This gives minimal semantic per-commit diffs, conflict isolation per issue, and simple parsing in
any language.

## CI gate

`fleece install` adds a tool-free, cross-platform (bash + PowerShell) GitHub Actions workflow
(`fleece-ci-gate.yml`) that **fails the PR iff `.fleece/issues/` contains any `*.jsonl`**:

```
active issues → seal refuses → files remain → CI fails
all inactive  → seal clears  → files gone   → CI passes
```

This enforces the "clear your branch memory before merge" rule automatically.

## Claude Code Integration

```bash
fleece install
```

Installs:
- A **`fleece prime` SessionStart hook** that surfaces the live count of active issues when the
  branch is dirty.
- The **`fleece` skill** at `.claude/skills/fleece/` — the full reference for commands, hierarchy,
  statuses, JSON output, and the GitHub round-trip.
- The **CI gate** workflow described above.

## Migrating from v3

v4 is a breaking rewrite. Key changes:

| Area | v3 | v4 |
|------|----|----|
| **Model** | Durable backlog | Branch-local ephemeral working memory |
| **Storage** | Snapshot (`issues.jsonl`) + change-file DAG + tombstones | One append-only log per issue (`issues/<id>.jsonl`); file order is truth |
| **Statuses** | included `draft`, `archived`, `deleted` | `draft`/`deleted` removed; `archived` → `promoted` |
| **Types** | included `idea` | `idea` removed |
| **Removed commands** | — | `project`, `merge`, `diff`, `link`, `clean` (and `config defaultBranch`) gone |
| **New commands** | — | `seal`, `promote`, `absorb`, `auth`, `openspec dependencies` |
| **Branch close-out** | `project` + `clean` | `seal` + CI gate |
| **Durable work** | stayed in Fleece | escapes to GitHub via `promote` |

To bring a legacy (pre-v4) repository forward:

```bash
fleece migrate
```

`migrate` auto-detects and converts the legacy **hashed-file** layout
(`.fleece/issues_<hash>.jsonl` + `tombstones_<hash>.jsonl`) into per-issue logs, then deletes the
legacy sources. It is idempotent — a second run exits with "no migration needed."

A legacy **durable snapshot** (`.fleece/issues.jsonl`) is **not** auto-converted on normal
commands: Fleece prints a non-destructive warning routing you to `fleece prime v4-migration`,
which walks through promoting long-running issues to GitHub and then sealing. (Running
`fleece migrate` explicitly will convert the durable layout too.)

## Architecture

Fleece is split into three assemblies so the core stays pure and testable:

- **`Fleece.Core`** — all business logic, models, and serialization. No I/O statics, no OctoKit;
  everything is mockable via `System.IO.Abstractions`.
- **`Fleece.Cli`** — thin command wrappers around Core services.
- **`Fleece.GitHub`** — the OctoKit-backed `IGitHubService` implementation (kept out of Core).

## Contributing

Contributions welcome. Please open an issue to discuss significant changes before submitting a PR.

## License

MIT License — see [LICENSE](LICENSE) for details.
