# Fleece - Interaction Model

**Project**: Fleece
**Analysis Date**: 2026-06-04
**Surfaces**: CLI terminal (ANSI), CLI stdout (JSON), Claude Code session, Git hooks, GitHub Actions

## Experience Principles

- **Git-native local-first**: Issues are JSONL files under `.fleece/` committed alongside code. No external service, no auth, no network round-trips. The working model is the same as editing source files.
- **CLI as the only surface**: All interaction is through the `fleece` binary. No interactive TUI, no editor. Commands are strictly flag-driven and non-interactive.
- **Dual output contract**: Every command supports `--json` (lean `IssueDto`) and `--json-verbose` (full `Issue`). Human output uses Spectre.Console ANSI markup; machine output is plain JSON. These are mutually exclusive paths.
- **Fail-fast with colored diagnostics**: Invalid args, ambiguous ID resolution, incompatible flags, and unmerged file guards all emit `[red]Error:[/]` and return exit code 1 immediately.
- **Partial ID matching with explicit disambiguation**: All commands accept 3+ char prefix. Zero matches → error. Multiple matches → table of all candidates.
- **Automation and AI assistant integration are first-class**: `fleece install` registers `fleece prime` as a `SessionStart` hook for Claude Code. `--commit`/`--push` flags and `fleece commit` enable scripted git operations.
- **Terminal statuses are hidden by default**: `complete`, `archived`, `closed` excluded from `fleece list` unless `--all` is passed.
- **Deprecation is surfaced inline**: `--tree-root` emits a yellow warning; `fleece merge` prints a deprecation notice on stderr. Deprecated behavior continues for one release cycle.

## Actors & Surfaces

| Actor | Surface | Goal | Entry Points |
|-------|---------|------|--------------|
| Developer | CLI terminal (ANSI) | Create, track, and progress work items alongside code | `fleece create`, `fleece list`, `fleece edit`, `fleece next` |
| AI coding assistant (Claude Code) | `fleece prime` markdown output, `--json`/`--one-line` | Read issue context at session start; update status during coding | `SessionStart` hook, `fleece list --json`, `fleece edit --json` |
| CI/CD pipeline | GitHub Actions workflow, `--json`, `--strict` | Daily snapshot compaction; schema violation detection | `fleece project`, `fleece list --strict` |

## Primary Actions

### CLI Terminal (human-readable)
**Role**: Primary interactive surface for developers. Spectre.Console ANSI output — rounded-border tables, boxed panels, Unicode task graphs, ASCII tree indentation.
**Primary actions**:
- `fleece create -t <title> -y <type>` — create issue
- `fleece edit <id> [flags]` — edit issue fields
- `fleece open/progress/review/complete/archived/closed <id>` — advance status via shortcut
- `fleece list [-s/-y/-p/--assigned/--tag/--search]` — browse with filters
- `fleece list --tree` — hierarchy as ASCII tree
- `fleece list --next` / `fleece next` — actionability task graph
- `fleece show <id>` — full detail with hierarchy context
- `fleece dependency` / `fleece move` — manage parent-child ordering
- `fleece delete <id>` / `fleece clean` — soft-delete or hard-delete
- `fleece commit` — commit `.fleece/` changes to git

**Intentional constraints**: No interactive prompts; no external editor; no ANSI in `--json` or `--one-line` output.

### CLI Stdout (JSON / machine-readable)
**Role**: Machine-readable output for scripting, AI agent consumption, and CI.
**Primary actions**: All commands with `--json` (lean `IssueDto`) or `--json-verbose` (full `Issue`).

### Claude Code Session (AI context injection)
**Role**: Delivers structured markdown at session start about Fleece workflow and commands. Only fires if `.fleece/` directory exists.
**Primary actions**:
- `fleece prime` — general workflow overview + OpenSpec integration if `openspec/` exists
- `fleece prime <topic>` — deep-dive: `hierarchy`, `commands`, `statuses`, `sync`, `json`, `next`, `tree`, `merge`, `openspec`

### Git Hooks
**Role**: Transparent automation — keeps `.fleece/changes/` staged on every commit; writes merge topology markers on merge commits.

### GitHub Actions Workflow
**Role**: Scheduled daily `fleece project` compaction on the default branch (`06:00 UTC`). Created by `fleece install` when a `github.com` remote is detected.

## User-Visible States

| State | Meaning | Surface Signals |
|-------|---------|-----------------|
| `draft` | Created but not ready for work | `[dim]` color |
| `open` | Ready to be worked on | `[cyan]` color |
| `progress` | Currently being worked on | `[blue]` color |
| `review` | Work complete, awaiting review | `[purple]` color |
| `complete` | Finished and verified (terminal) | `[green]` color; `●` in task graph |
| `archived` | No longer relevant (terminal) | `[dim]` color; `⊘` in task graph |
| `closed` | Abandoned / won't fix (terminal) | `[dim]` color; `⊘` in task graph |
| actionable (graph only) | Unblocked and ready to pick up | `○` marker (vs `◌` for non-actionable open) |
| sync status | Git sync state (`--sync-status` only) | `~` green (synced), `+` yellow (committed), `*` red (local only) |
| context-only (search in `--next`) | Structural context for matched issues | `[dim]` rendering |
| unmerged conflict guard | Multiple unmerged change files detected | `[red]Error:[/]` blocks all write commands |

## Feedback Loops

- **Issue created**: `[green]Created issue[/] [bold]{id}[/]` + single-issue table panel; if `--commit`, `[dim]Changes committed to git[/]`; git failure → `[yellow]Warning:[/]`
- **Issue updated** (edit): `[green]Updated issue[/] [bold]{id}[/]` + table panel (with `--json` → `IssueDto`)
- **Status advanced** (shortcut): `[green]Updated {n} issue(s) to status '{status}'[/]` + table
- **Issue deleted**: `[green]Deleted issue[/] [bold]{id}[/]`
- **Clean completed**: `[yellow]Cleaned {n} issue(s)[/]` + tombstone table; if refs stripped, `[yellow]Stripped {n} dangling reference(s)[/]`; footer `[green]Clean complete! Tombstone records created.[/]`; if nothing: `[green]No issues to clean.[/]`
- **Dry run**: `[yellow]Dry run mode - no changes will be made[/]` before results
- **Project completed**: `[green]Projected {n} change file(s) into {n} issue(s).[/]`; if auto-cleaned: `[yellow]Auto-cleaned {n} soft-deleted issue(s) older than 30 days.[/]`
- **Empty results**: `[dim]No issues found.[/]` (table), `[dim]No actionable issues found.[/]` (next)
- **Dependency cycle**: `[red]Found {n} cycle(s):[/]` + numbered paths; exit code 1

## Accessibility & Discoverability

- **No interactive prompts**: All input is via flags — enables CI, git hooks, and AI automation without terminal control sequences.
- **Non-ANSI paths**: `--one-line` produces plain `{id} {status} {type} {title}` per line; `--json` produces no ANSI markup.
- **Silent exit when no `.fleece/`**: `fleece prime` exits 0 silently if `.fleece/` directory does not exist — prevents noise in non-Fleece repos.
- **`--strict` flag**: CI-safe validation — fails exit code 1 on schema warnings.
- **Node markers use distinct symbols**: `○ ◌ ● ⊘` distinguish actionable/non-actionable/complete/terminal independently of color.
- **Sync indicators use color + symbol**: `~ + *` convey meaning in colorblind/monochrome terminals.
- **`fleece prime <topic>`**: Progressive disclosure for AI assistants and developers learning the system.

## Cross-Surface Deltas

| Behavior | Surfaces | Delta | Reason |
|----------|----------|-------|--------|
| Issue list output | ANSI vs `--json` | ANSI: colored table with footer; `--json`: JSON array of `IssueDto` | Lean JSON for scripting; verbose metadata via `--json-verbose` |
| Single-issue detail | ANSI vs `--json` | ANSI: boxed panel with hierarchy context; `--json`: `IssueShowDto` (includes hierarchy); `--json-verbose`: raw `Issue` without hierarchy | `--json` exposes computed hierarchy useful for scripting |
| One-line listing | ANSI default vs `--one-line` | ANSI: colored columns; `--one-line`: plain `{id} {status} {type} {title}` | Shell scripting / non-ANSI environments |
| Task graph vs tree | `--next` vs `--tree` | Graph: Unicode box-drawing matrix + actionability markers; tree: ASCII box-drawing indentation | Different visual grammar for dynamic vs static views |
| AI session context | Claude Code session | Raw markdown, no ANSI, prescriptive workflow instructions | AI assistants consume markdown; `fleece prime` content deliberately includes "do not ask user for permission" |
| Config display | ANSI vs `--json` | ANSI: three-column table with color-coded source; JSON: nested `{value, source}` objects | Source provenance important for debugging config layering |

## Related KB Links

- **System topology**: See [architecture.md](architecture.md)
- **Component inventory**: See [modules.md](modules.md)
- **Terminology**: See [concept_map.md](concept_map.md)
- **Implementation details**: See [patterns.md](patterns.md)
