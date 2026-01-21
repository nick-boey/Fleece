# Fleece

Simple issue management in repository folders.

Fleece is a lightweight, file-based issue tracking system designed to live alongside your code. Issues are stored as JSONL files in your repository, making them versionable, portable, and always accessible - even offline.

## Philosophy

- **Local-first**: Issues live in your repository, not a remote server
- **Version-controlled**: Track issue changes alongside code changes
- **Simple**: No database, no server, just files
- **AI-friendly**: Built-in integration with Claude Code for AI-assisted development

## Features

- Create, edit, and track issues with a simple CLI
- Interactive editor-based issue creation with YAML templates
- JSONL storage format for easy parsing and diffing
- Change history tracking with user attribution
- Conflict detection and resolution for collaborative workflows
- Claude Code hooks for AI-assisted issue management
- JSON output for scripting and automation
- Tag support for categorizing issues

## Installation

Fleece is distributed as a .NET tool. Install it globally with:

```bash
dotnet tool install --global Fleece.Cli
```

Or install locally in your project:

```bash
dotnet tool install Fleece.Cli
```

## Quick Start

### Initialize issue tracking

Issues are stored in `.fleece/issues.jsonl` in your repository root. The directory is created automatically when you create your first issue.

### Create an issue

```bash
# Interactive creation (opens editor with YAML template)
fleece create

# Command-line creation
fleece create --title "Add user authentication" --type feature

# Create with tags
fleece create --title "Fix login bug" --type bug --tags "urgent,backend"
```

### List issues

```bash
fleece list                      # Shows only open issues by default
fleece list --all                # Show all issues including complete/closed
fleece list --status complete    # Filter by specific status
fleece list --type bug           # Filter by type
fleece list --json               # JSON output for scripting
fleece list --json-verbose       # JSON with all metadata fields
```

### Update an issue

```bash
fleece edit abc123 --status complete
fleece edit abc123 --priority 1 --linked-pr 42
fleece edit abc123 --tags "reviewed,ready"
```

### Search issues

```bash
fleece search "authentication"
```

### View change history

```bash
fleece history
fleece history abc123  # History for specific issue
fleece history --user john  # Filter by user
```

## Storage Format

Issues are stored as [JSONL](https://jsonlines.org/) (JSON Lines) - one JSON object per line:

```
.fleece/
  issues.jsonl      # Active issues
  changes.jsonl     # Change history
  conflicts.jsonl   # Conflict records (if any)
```

Example issue entry:

```json
{"Id":"a1b2c3","Title":"Fix login bug","Status":"open","Type":"bug","Priority":1,"LastUpdate":"2024-01-15T10:30:00Z"}
```

This format provides:
- Easy version control diffing
- Append-only change tracking
- Simple parsing in any language
- Human-readable storage

## Issue Types

| Type | Description |
|------|-------------|
| `task` | General work item |
| `bug` | Something broken |
| `chore` | Maintenance work |
| `idea` | Future consideration |
| `feature` | New functionality |

## Issue Statuses

| Status | Description |
|--------|-------------|
| `open` | Active, needs work |
| `complete` | Work finished, pending verification |
| `closed` | Done and verified |
| `archived` | No longer relevant |

## Change History Tracking

Fleece tracks all changes to issues with user attribution. View the history with:

```bash
fleece history
```

Each change record includes:
- Timestamp
- User who made the change
- Issue ID
- Type of change (created, updated, deleted)
- Changed fields and values

## Claude Code Integration

Fleece integrates with [Claude Code](https://claude.com/claude-code) to enable AI-assisted issue management.

### Install hooks

```bash
fleece install
```

This adds hooks to `.claude/settings.json` that:
1. Load current issues when starting a session
2. Provide issue management commands to the AI
3. Enable automatic issue updates based on work completed

### Get AI instructions

```bash
fleece prime
```

Outputs instructions that help Claude Code understand how to work with Fleece issues.

### Workflow with Claude Code

1. Start Claude Code - it automatically sees current issues
2. Work on tasks - Claude can update issue status
3. Complete work - Claude marks issues complete and creates follow-ups
4. Link PRs - Associate pull requests with issues

## CLI Reference

See [docs/CLI.md](docs/CLI.md) for complete command documentation.

## Contributing

Contributions are welcome. Please open an issue to discuss significant changes before submitting a PR.

## License

MIT License - see [LICENSE](LICENSE) for details.
