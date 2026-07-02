<!-- This file is managed by `fleece install`. Manual edits will be overwritten on the next install. -->

# Available Commands

## Creating and Editing

- `fleece create -t <title> -y {task|bug|chore|feature|verify} [OPTIONS]` - Create an issue
- `fleece edit <id> [OPTIONS]` - Update an issue (at least one field flag required)

**Create/Edit Options:**
- `-p, --priority` - Set priority (1-5)
- `-d, --description` - Set description
- `--parent-issues` - Parent issue ids for hierarchy and sorting (comma-separated)
- `--linked-issues` - Related issue ids (comma-separated)
- `--linked-pr` - Link to pull request number
- `--assign` - Assign to a user
- `--tags` - Add tags (comma-separated, supports simple tags and key=value keyed tags)

## Tags

Issues support two types of tags:
- **Simple tags**: Plain labels like `urgent`, `backend`
- **Keyed tags**: Key-value pairs like `project=frontend`, `team=platform`

Adding tags: `fleece edit <id> --tags "project=frontend,urgent"`
Filtering by key only (matches all values): `fleece list --tag project`
Filtering by exact key=value: `fleece list --tag project=frontend`
The `--tag` option can be specified multiple times (OR logic).

## Viewing

- `fleece list [-s STATUS] [-y TYPE] [-p PRIORITY]` - List issues with filters
- `fleece show <id>` - Display all details for a single issue
- `fleece list --tree` - Display parent-child hierarchy
- `fleece list --next` - Display task graph with execution ordering
- `fleece list <id>` - Show an issue with its full hierarchy (parents + children)
- `fleece list <id> --parents` - Show an issue with only its parent hierarchy
- `fleece list <id> --children` - Show an issue with only its child hierarchy
- `fleece next` - Find issues ready to be worked on next
- `fleece search "query"` - Search issues by text

## Managing

- `fleece delete <id>` - Delete an issue (removes its log)
- `fleece dependency --parent <id> --child <id>` - Add/remove parent-child dependency
- `fleece validate` - Check for cyclic dependencies in issue hierarchy

## Branch lifecycle

- `fleece seal` - Archive all issues and clear the live directory. Refuses while any issue
  is active ({open, progress, review}).

## Durable tracker ({{TRACKER_TITLE}})

Promoted work escalates into this repository's durable tracker, {{TRACKER_TITLE}}. See
`references/{{TRACKER}}.md` for the full workflow.

- `fleece auth` - Report durable-tracker auth status (GitHub: login + token source; Linear: not applicable)
- `fleece promote <id> [<id>...]` - Escalate Fleece issues into {{TRACKER_TITLE}}
- `fleece promote <id> [<id>...] --ref <ref>` - Record an externally-created reference (used by Linear: create the issue, then record it)
- `fleece absorb <ref>` - Bring a {{TRACKER_TITLE}} issue into Fleece (GitHub: `#<number>`; Linear: guided `fleece create --tag absorbed-from=<ref>`)

## OpenSpec

- `fleece openspec dependencies` - Render a DAG of OpenSpec changes from their depends-on
  frontmatter

## Setup

- `fleece install` - Install Claude Code hooks, the `fleece` skill, pre-commit hook, gitignore entries, and the CI gate workflow
- `fleece migrate` - One-time bring-forward of legacy hashed-layout repos to the current storage layout
