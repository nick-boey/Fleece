# Issue Authoring

## Purpose

Defines how `fleece create` and `fleece edit` accept issue data. All inputs are passed via CLI flags; no external text editor is ever launched.
## Requirements
### Requirement: Create command SHALL require title and type via CLI flags

The `fleece create` command SHALL obtain all issue fields exclusively from CLI flags. It SHALL NOT launch an external text editor under any invocation.

When invoked without `--title` or without `--type`, the command SHALL exit with a non-zero status code and print an error identifying the missing required flag(s).

#### Scenario: Missing title flag
- **WHEN** a user runs `fleece create` with no arguments
- **THEN** the command exits with a non-zero status code
- **AND** prints an error indicating `--title` is required
- **AND** does not spawn any external process

#### Scenario: Missing type flag
- **WHEN** a user runs `fleece create --title "My issue"` with no `--type`
- **THEN** the command exits with a non-zero status code
- **AND** prints an error indicating `--type` is required

#### Scenario: All required flags present
- **WHEN** a user runs `fleece create --title "My issue" --type task`
- **THEN** the command creates the issue using only CLI-provided values
- **AND** exits with status code 0
- **AND** does not spawn any external process

### Requirement: Edit command SHALL require at least one field flag

The `fleece edit <id>` command SHALL apply updates exclusively from CLI flags. It SHALL NOT launch an external text editor under any invocation.

When invoked with only an issue ID and no field flags (such as `--title`, `--description`, `--status`, `--type`, `--priority`, `--linked-issues`, `--linked-pr`, `--assign`, `--tags`, `--working-branch`, `--execution-mode`), the command SHALL exit with a non-zero status code and print an error listing the available field flags.

#### Scenario: Edit invoked with only an ID
- **WHEN** a user runs `fleece edit abc123` with no field flags
- **THEN** the command exits with a non-zero status code
- **AND** prints an error listing the available field flags
- **AND** does not spawn any external process
- **AND** does not modify the issue

#### Scenario: Edit invoked with at least one field flag
- **WHEN** a user runs `fleece edit abc123 -s complete`
- **THEN** the command updates the issue using the provided flag value(s)
- **AND** exits with status code 0
- **AND** does not spawn any external process

### Requirement: Create and edit commands SHALL accept a linked PR via `--linked-pr`

Both `fleece create` and `fleece edit` SHALL accept an integer `--linked-pr <PR>` flag. When supplied, the CLI SHALL forward the value to the Core service so that the issue's PR linkage (stored as the `hsp-linked-pr` keyed tag) is set or updated.

On `fleece edit`, supplying `--linked-pr` alone (no other field flags) SHALL be treated as a valid field update.

#### Scenario: Create with linked PR
- **WHEN** a user runs `fleece create --title "Fix bug" --type bug --linked-pr 42`
- **THEN** the created issue is linked to PR #42
- **AND** the command exits with status code 0

#### Scenario: Edit with only linked PR
- **WHEN** a user runs `fleece edit abc123 --linked-pr 99`
- **THEN** the command updates the issue's linked PR to 99
- **AND** exits with status code 0
- **AND** no missing-field-flag error is printed

### Requirement: Write commands SHALL emit events to the active change file

Write commands (`create`, `edit`, status commands, `dependency`, `move`, and the like) SHALL persist changes by appending events to the affected issue's own `.fleece/issues/<id>.jsonl` log. `create` SHALL write a new file beginning with a `create` event; subsequent mutations SHALL append `set`/`add`/`remove` events to that file. There is no shared active change file and no `.active-change` pointer.

#### Scenario: Create writes a new per-issue log
- **WHEN** `fleece create --title "x" --type task` runs
- **THEN** a new `.fleece/issues/<id>.jsonl` file is created whose first line is a `create` event

#### Scenario: Edit appends to the issue's own log
- **WHEN** `fleece edit <id> -s progress` runs
- **THEN** a `set` event is appended to `.fleece/issues/<id>.jsonl`

### Requirement: CLI SHALL not depend on external editors or a templates directory

The Fleece CLI SHALL NOT invoke the `VISUAL` or `EDITOR` environment variables, `notepad`, `open -W -t`, `nano`, `vim`, or `vi` for any command.

The CLI SHALL NOT create or rely on the `~/.fleece/templates/` directory.

The CLI SHALL NOT ship an `EditorService` or any equivalent abstraction whose purpose is to launch an external editor.

#### Scenario: No editor is spawned on any CLI path
- **WHEN** any `fleece` subcommand is executed
- **THEN** no child process is started for `vi`, `vim`, `nano`, `notepad`, `open`, or the value of `$EDITOR` / `$VISUAL`

#### Scenario: Templates directory is not created
- **WHEN** any `fleece` subcommand is executed on a machine with no pre-existing `~/.fleece/templates/` directory
- **THEN** the directory is not created by the CLI

### Requirement: Issue status set SHALL be the v4 ephemeral lifecycle

The issue status set SHALL be `Open`, `Progress`, `Review`, `Complete`, `Closed`, and `Promoted`. There SHALL be no `Draft` status. The **active** set (a branch is not mergeable while any exist) SHALL be `{Open, Progress, Review}`. The **inactive** set SHALL be `{Complete, Closed, Promoted}`. `Promoted` is a terminal status meaning the issue has been escalated to a GitHub issue and SHALL carry a `promoted=<github-#>` keyed tag.

#### Scenario: Draft is not an accepted status
- **WHEN** any command attempts to set status to `Draft`
- **THEN** the command rejects the value as unknown

#### Scenario: Active vs inactive classification
- **WHEN** issues exist with statuses across the set
- **THEN** only `Open`, `Progress`, and `Review` count as active for seal and CI-gate purposes
- **AND** `Complete`, `Closed`, and `Promoted` count as inactive

### Requirement: Issue type set SHALL exclude Idea

The issue type set SHALL be `Task`, `Bug`, `Chore`, `Feature`, and `Verify`. There SHALL be no `Idea` type.

#### Scenario: Idea is not an accepted type
- **WHEN** `fleece create --title "x" --type idea` runs
- **THEN** the command rejects `idea` as an unknown type

#### Scenario: Supported types are accepted
- **WHEN** `fleece create` is invoked with type `task`, `bug`, `chore`, `feature`, or `verify`
- **THEN** the issue is created with that type

