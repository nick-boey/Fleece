# Fleece CLI Reference

Complete documentation for all Fleece CLI commands.

## Global Options

| Option | Description |
|--------|-------------|
| `--help` | Display help for any command |
| `--version` | Display version information |

## Commands

### create

Create a new issue.

```bash
# Interactive mode (opens editor with YAML template)
fleece create

# Command-line mode
fleece create --title <title> --type <type> [options]
```

When run with no arguments, opens your default editor with a YAML template for interactive issue creation. The editor is determined by the `VISUAL` or `EDITOR` environment variable, or defaults to `notepad` on Windows, `open` on macOS, or `nano`/`vim` on Linux.

**Required Options (command-line mode):**
| Option | Short | Description |
|--------|-------|-------------|
| `--title` | `-t` | Issue title (used to generate ID) |
| `--type` | | Issue type: task, bug, chore, idea, feature |

**Optional Options:**
| Option | Short | Description |
|--------|-------|-------------|
| `--description` | `-d` | Detailed description |
| `--status` | `-s` | Initial status (default: open) |
| `--priority` | `-p` | Priority 1-5 (1=highest) |
| `--linked-pr` | | Associated PR number |
| `--linked-issues` | | Comma-separated issue IDs or #numbers |
| `--parent-issues` | | Comma-separated parent issue IDs |
| `--tags` | | Comma-separated tags (no whitespace) |
| `--json` | | Output as JSON |
| `--json-verbose` | | Output as JSON with all metadata |

**Examples:**
```bash
# Interactive mode - opens editor
fleece create

# Create a simple task
fleece create --title "Update documentation" --type task

# Create a high-priority bug
fleece create --title "Login fails on Safari" --type bug --priority 1

# Create a feature with description
fleece create --title "Add dark mode" --type feature -d "Support system theme preference"

# Create with tags
fleece create --title "API refactor" --type task --tags "backend,breaking-change"

# Create linked issue
fleece create --title "Fix tests" --type chore --parent-issues abc123
```

---

### list

List issues with optional filters. By default, only shows open issues.

```bash
fleece list [options]
```

**Options:**
| Option | Short | Description |
|--------|-------|-------------|
| `--all` | `-a` | Show all issues (including complete, closed, archived) |
| `--status` | `-s` | Filter by status |
| `--type` | `-t` | Filter by type |
| `--priority` | `-p` | Filter by priority |
| `--group` | `-g` | Filter by group |
| `--assigned` | | Filter by assignee |
| `--json` | | Output as JSON (simplified format) |
| `--json-verbose` | | Output as JSON with all metadata fields |

**Examples:**
```bash
# List open issues (default)
fleece list

# List all issues including complete/closed
fleece list --all

# List open bugs
fleece list --status open --type bug

# List high-priority items
fleece list --priority 1

# Output as JSON for scripting
fleece list --json

# Output with all metadata (timestamps, modifiedBy fields)
fleece list --json-verbose
```

---

### edit

Edit an existing issue.

```bash
fleece edit <id> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `id` | Issue ID (6-character) |

**Options:**
| Option | Short | Description |
|--------|-------|-------------|
| `--title` | `-t` | New title (changes ID!) |
| `--description` | `-d` | New description |
| `--status` | `-s` | New status |
| `--type` | | New type |
| `--priority` | `-p` | New priority |
| `--linked-pr` | | New PR number |
| `--linked-issues` | | Replace linked issues |
| `--parent-issues` | | Replace parent issues |
| `--group` | `-g` | New issue group |
| `--assign` | `-a` | New assignee username |
| `--tags` | | Replace tags (comma-separated) |
| `--json` | | Output as JSON |
| `--json-verbose` | | Output as JSON with all metadata |

**Examples:**
```bash
# Mark as complete
fleece edit abc123 --status complete

# Update priority
fleece edit abc123 --priority 2

# Link a PR
fleece edit abc123 --linked-pr 42

# Update tags
fleece edit abc123 --tags "reviewed,approved"
```

---

### delete

Delete an issue by ID.

```bash
fleece delete <id>
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `id` | Issue ID to delete |

**Examples:**
```bash
fleece delete abc123
```

---

### search

Search issues by text.

```bash
fleece search <query> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `query` | Text to search in title and description |

**Options:**
| Option | Short | Description |
|--------|-------|-------------|
| `--json` | | Output as JSON |

**Examples:**
```bash
# Search for login-related issues
fleece search "login"

# Search with JSON output
fleece search "authentication" --json
```

---

### diff

Compare two JSONL files or show current conflicts.

```bash
fleece diff [file1] [file2]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `file1` | First JSONL file (optional) |
| `file2` | Second JSONL file (optional) |

Without arguments, shows current conflicts from `.fleece/conflicts.jsonl`.

**Examples:**
```bash
# Show current conflicts
fleece diff

# Compare two files
fleece diff main-issues.jsonl feature-issues.jsonl
```

---

### merge

Find duplicate issues and move older versions to conflicts.

```bash
fleece merge [options]
```

**Options:**
| Option | Short | Description |
|--------|-------|-------------|
| `--dry-run` | | Show what would be merged without making changes |

**Examples:**
```bash
# Merge duplicates
fleece merge

# Preview merge
fleece merge --dry-run
```

---

### clear-conflicts

Clear conflict records for a specific issue.

```bash
fleece clear-conflicts <id>
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `id` | Issue ID to clear conflicts for |

**Examples:**
```bash
fleece clear-conflicts abc123
```

---

### history

Show change history for issues with user attribution.

```bash
fleece history [issue_id] [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `issue_id` | Optional issue ID to filter history |

**Options:**
| Option | Short | Description |
|--------|-------|-------------|
| `--user` | `-u` | Filter by user name |
| `--json` | | Output as JSON |

**Examples:**
```bash
# Show all change history
fleece history

# Show history for specific issue
fleece history abc123

# Show changes by a specific user
fleece history --user john

# JSON output for scripting
fleece history --json

# Combine filters
fleece history abc123 --user john --json
```

---

### migrate

Migrate issues to property-level timestamps format. This command updates older issue files to use the newer format that tracks timestamps at the property level.

```bash
fleece migrate [options]
```

**Options:**
| Option | Description |
|--------|-------------|
| `--dry-run` | Check if migration is needed without making changes |
| `--json` | Output as JSON |

**Examples:**
```bash
# Run migration
fleece migrate

# Preview migration (no changes made)
fleece migrate --dry-run

# JSON output
fleece migrate --json
```

---

### install

Install Claude Code hooks for automatic issue management.

```bash
fleece install
```

Adds hooks to `.claude/settings.json` for:
- Pre-prompt: Loads current issues for context
- Post-tool: Updates issues based on AI actions

---

### prime

Print LLM instructions for issue tracking.

```bash
fleece prime
```

Outputs instructions that can be included in prompts to help LLMs understand how to work with Fleece issues.

---

## Issue Model

### Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | string | Yes | 6-char SHA256 hash of title |
| `Title` | string | Yes | Issue title |
| `Description` | string | No | Detailed description |
| `Status` | enum | Yes | open, complete, closed, archived |
| `Type` | enum | Yes | task, bug, chore, idea, feature |
| `LinkedPR` | int | No | Associated PR number |
| `LinkedIssues` | string[] | No | Related issue IDs |
| `ParentIssues` | string[] | No | Parent issue IDs |
| `Priority` | int | No | 1 (highest) to 5 (lowest) |
| `Group` | string | No | Issue group for categorization |
| `AssignedTo` | string | No | Username of assignee |
| `Tags` | string[] | No | Arbitrary string tags |
| `CreatedBy` | string | No | Username who created issue |
| `LastUpdate` | DateTimeOffset | Yes | Last modification time |
| `CreatedAt` | DateTimeOffset | Yes | Creation time |

### Example JSONL Entry

```json
{"Id":"a1b2c3","Title":"Fix login bug","Description":"Users can't log in on Safari","Status":"open","Type":"bug","Priority":1,"Tags":["urgent","backend"],"LastUpdate":"2024-01-15T10:30:00Z"}
```

---

## Claude Code Integration

### Installation

```bash
fleece install
```

This adds hooks to your Claude Code configuration that:
1. Load current issues when starting a session
2. Allow the AI to create, update, and close issues
3. Link completed work to relevant issues

### Prime Instructions

```bash
fleece prime
```

Include the output in your system prompt for AI-assisted issue management:

```
When working on this codebase, track issues using the Fleece CLI:
- Create issues: fleece create --title "..." --type task|bug|feature|chore|idea
- List issues: fleece list
- Complete work: fleece edit <id> --status complete
- Link PRs: fleece edit <id> --linked-pr <number>
```

### Workflow Example

1. AI reads current issues via `fleece list --json`
2. AI works on an issue
3. AI marks issue complete: `fleece edit abc123 --status complete`
4. AI creates follow-up issues if needed
