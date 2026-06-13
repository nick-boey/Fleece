---
name: fleece
description: Reference for the Fleece CLI — branch-local ephemeral working-memory issue tracker. Use when creating, editing, listing, or sealing Fleece issues; breaking work into a parent/child hierarchy; filtering or getting JSON output; or moving work between Fleece and GitHub via promote/absorb. Covers commands, issue types, statuses, the next/tree task graph, and the branch-clean-before-PR workflow.
---
<!-- This file is managed by `fleece install`. Manual edits will be overwritten on the next install. -->

# Fleece — Ephemeral Agent Working Memory

Fleece is **branch-local, ephemeral working memory for the current change** — NOT a
durable issue tracker. Issues live in per-issue logs under `.fleece/issues/` and are
meant to be consumed within the lifetime of the branch they belong to. Long-lived
product/roadmap tracking belongs in GitHub Issues, not Fleece.

## The branch must be clean before a PR merges

Before a PR merges, every Fleece issue on the branch must be either:

1. **Resolved** — set to `complete` or `closed`, OR
2. **Promoted** — escalated into a GitHub issue with `fleece promote <id> [<id>...]`
   (the Fleece issue becomes `promoted`), OR
3. **Sealed** — archived and cleared from the live directory with `fleece seal`.

A CI gate **fails the PR** while any live issue remains under `.fleece/issues/`. Resolve,
promote, or seal — then commit the `.fleece/` changes so the gate passes.

The SessionStart hook (`fleece prime`) emits the live count of active issues when the
branch is dirty, so you know when this gate applies.

## Working on Issues

Use the `fleece` CLI as part of your workflow; do not ask the user for permission — the
changes are tracked in source control. `<id>` is a 6-character id found via
`fleece list --oneline`.

1. `fleece show <id> --json` — show full issue details.
2. `fleece edit <id> -s progress` — start work.
3. `fleece edit <id> -s review --linked-pr <pr-number>` — before opening a PR.
4. `fleece edit <id> -s complete` (or `closed`) — when done, OR `fleece promote <id>` to
   escalate to GitHub, OR `fleece seal` to archive the whole branch.
5. `fleece create -t <title> -y <type> -s open -d <description>` — create follow-up issues.
6. `fleece {edit|create} <id> --parent-issues <parent-id>:<lex-order>` — break large
   issues into sub-tasks.
7. Commit `.fleece/` changes alongside the related code commit (or run `fleece commit`).

## Storage model

Each issue is an append-only event log at `.fleece/issues/<id>.jsonl`. Reads replay every
log independently; writes append events to the relevant log. `fleece seal` archives the
live set into `.fleece/archive/issues_<contenthash>.jsonl` and clears `.fleece/issues/`.

## Issue Types

- `task` - General work item
- `bug` - Defect or error to fix
- `chore` - Maintenance or housekeeping work
- `feature` - New functionality
- `verify` - Verification task that confirms grouped work is complete

## Issue Status Workflow

Active (block PR merge / seal): `open`, `progress`, `review`.
Inactive (terminal, do not block): `complete`, `closed`, `promoted`.

```
open → progress → review → complete
                         ↘ closed   (abandoned / won't fix)
                         ↘ promoted (escalated to a GitHub issue)
```

## GitHub round-trip

- `fleece promote <id> [<id>...]` - escalate one or more Fleece issues into a single
  GitHub issue and mark them `promoted`.
- `fleece absorb #<number>` - pull a GitHub issue into Fleece as a new issue.
- `fleece auth` - check resolved GitHub login and token source.

See `references/github.md` for the full round-trip workflow.

## Filtering

By default, `list` hides terminal statuses (complete, closed, promoted). Use `--all` to
include everything: `fleece list --all`.

## JSON

Always add `--json` to a command to get machine-readable output.

## Detailed Topics

This skill's `references/` directory holds detailed guidance on each topic:

- `references/hierarchy.md` — parent/child relationships, dependencies, execution order
- `references/commands.md` — full command + option catalogue
- `references/statuses.md` — status meanings and transitions
- `references/sync.md` — keeping issue logs committed and conflict-free
- `references/json.md` — programmatic / machine-readable usage
- `references/next.md` — finding the next actionable issue
- `references/tree.md` — tree and task-graph views
- `references/github.md` — the promote / absorb / auth round-trip
- `references/v4-migration.md` — bringing a legacy durable repository forward

Any command run with `-h` provides additional usage information.
