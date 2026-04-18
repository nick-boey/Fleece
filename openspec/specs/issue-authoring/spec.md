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
