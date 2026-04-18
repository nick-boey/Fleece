# Getting Started with Fleece

Fleece is a lightweight, file-based issue tracking system designed to live alongside your code. This guide will help you install Fleece and start tracking issues in minutes.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

## Installation

### Global Installation (Recommended)

Install Fleece as a global .NET tool to use it from any directory:

```bash
dotnet tool install --global Fleece.Cli
```

### Local Installation

Install Fleece locally in your project for team-consistent versioning:

```bash
dotnet new tool-manifest  # If you don't have a manifest yet
dotnet tool install Fleece.Cli
```

When installed locally, run commands with `dotnet fleece` instead of `fleece`.

### Verify Installation

```bash
fleece --version
```

## Quick Start

### Create Your First Issue

Issues are stored in `.fleece/` in your repository root. The directory is created automatically when you create your first issue.

```bash
# Create an issue (--title and --type required)
fleece create --title "Add user authentication" --type feature
```

### List Issues

```bash
# Show open issues (default)
fleece list

# Show all issues including completed
fleece list --all

# Filter by status or type
fleece list --status progress --type bug
```

### Update an Issue

Use the 6-character issue ID to update:

```bash
# Mark as in progress
fleece edit abc123 --status progress

# Mark as complete
fleece edit abc123 --status complete

# Link a pull request
fleece edit abc123 --linked-pr 42
```

### Search Issues

```bash
fleece search "authentication"
```

## Storage Format

Fleece stores issues as JSONL (JSON Lines) files:

```
.fleece/
  issues_abc123.jsonl    # Active issues
```

Each line is a self-contained JSON object representing an issue:

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
| `feature` | New functionality |
| `verify` | Verification checkpoint |

## Issue Statuses

Issues progress through these statuses:

```
open -> progress -> review -> complete
                           \-> archived (no longer relevant)
                           \-> closed (abandoned/won't fix)
```

| Status | Description |
|--------|-------------|
| `open` | Active, needs work |
| `progress` | Currently being worked on |
| `review` | Work complete, pending review |
| `complete` | Work finished and verified |
| `archived` | No longer relevant |
| `closed` | Abandoned or won't fix |

## Claude Code Integration

Fleece integrates with [Claude Code](https://claude.com/claude-code) for AI-assisted issue management.

### Install Hooks

```bash
fleece install
```

This adds hooks to `.claude/settings.json` that:
1. Load current issues when starting a session
2. Provide issue management context to the AI
3. Enable automatic issue updates based on work completed

### AI Workflow

1. Start Claude Code - it automatically sees current issues
2. Work on tasks - Claude can update issue status
3. Complete work - Claude marks issues complete and creates follow-ups
4. Link PRs - Associate pull requests with issues

## Next Steps

- Review the [CLI Reference](cli-reference.md) for all available commands
- Learn about the [Architecture](architecture.md) if you want to use Fleece programmatically
- Set up [CI/CD](ci-cd.md) for automated releases
