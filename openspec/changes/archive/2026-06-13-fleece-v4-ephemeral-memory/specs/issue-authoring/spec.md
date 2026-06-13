## MODIFIED Requirements

### Requirement: Write commands SHALL emit events to the active change file

Write commands (`create`, `edit`, status commands, `dependency`, `move`, and the like) SHALL persist changes by appending events to the affected issue's own `.fleece/issues/<id>.jsonl` log. `create` SHALL write a new file beginning with a `create` event; subsequent mutations SHALL append `set`/`add`/`remove` events to that file. There is no shared active change file and no `.active-change` pointer.

#### Scenario: Create writes a new per-issue log
- **WHEN** `fleece create --title "x" --type task` runs
- **THEN** a new `.fleece/issues/<id>.jsonl` file is created whose first line is a `create` event

#### Scenario: Edit appends to the issue's own log
- **WHEN** `fleece edit <id> -s progress` runs
- **THEN** a `set` event is appended to `.fleece/issues/<id>.jsonl`

## ADDED Requirements

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
