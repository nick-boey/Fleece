## ADDED Requirements

### Requirement: Write commands SHALL emit events to the active change file

Every fleece command that mutates issue state (`create`, `edit`, `delete`, `move`, `dependency`, `clean`, status change commands, and any future write commands) SHALL persist its mutation by appending one or more events to the active `change_{guid}.jsonl` file (selecting or creating that file per the active-change rules defined in the event-sourced-storage capability).

Specifically:

- `fleece create` SHALL emit a single `create` event whose `data` payload contains every property supplied on the command line, plus `createdAt` and `createdBy`.
- `fleece edit` SHALL emit one `set` event per scalar field flag supplied. For array field flags, it SHALL emit `add` and `remove` events as appropriate to reach the requested final state.
- `fleece delete` (soft-delete) SHALL emit a `set` event for the `status` property with value `Deleted`.
- `fleece clean` (hard-delete) SHALL emit a `hard-delete` event per issue removed.
- Dependency manipulation SHALL emit `add`/`remove` events for the `parentIssues` array property.

Write commands SHALL NOT directly modify `.fleece/issues.jsonl` outside of `fleece project`. They SHALL NOT write to any legacy `.fleece/issues_*.jsonl` file.

After emitting events, the command SHALL surface the resulting in-memory state to the user (e.g., `fleece create` echoes the created issue's ID; `fleece show` reflects the just-emitted edits) by replaying through the event store, not by re-reading the snapshot.

#### Scenario: Create emits a single create event
- **GIVEN** the user is on a fresh feature branch (no existing change file)
- **WHEN** the user runs `fleece create --title "Foo" --type task`
- **THEN** `.fleece/changes/change_{guid}.jsonl` exists with a meta event followed by exactly one `create` event
- **AND** the `create` event's `data` payload contains `title="Foo"`, `type="Task"`, `status="Open"`, plus `createdAt` and `createdBy`

#### Scenario: Edit emits one set event per scalar field
- **GIVEN** an issue `abc123` exists
- **WHEN** the user runs `fleece edit abc123 --title "Renamed" --status complete`
- **THEN** the active change file contains two new events: a `set` for `title` with value `"Renamed"` and a `set` for `status` with value `"Complete"`

#### Scenario: Edit emits add and remove events for array field updates
- **GIVEN** an issue `abc123` has `tags=["foo","bar"]`
- **WHEN** the user runs `fleece edit abc123 --tags "bar,baz"`
- **THEN** the active change file contains a `remove` event for `tags` with value `"foo"` and an `add` event for `tags` with value `"baz"`
- **AND** no event is emitted for `bar` (unchanged element)

#### Scenario: Delete (soft) emits a status set event
- **WHEN** the user runs `fleece delete abc123`
- **THEN** the active change file contains a `set` event for `status` with value `"Deleted"`

#### Scenario: Clean (hard) emits a hard-delete event
- **WHEN** the user runs `fleece clean` and issue `xyz` is hard-deleted
- **THEN** the active change file contains a `hard-delete` event for issue `xyz`

#### Scenario: Read after write reflects in-memory replay
- **GIVEN** the user has just run `fleece edit abc123 --title "New"` (no commit yet)
- **WHEN** the user runs `fleece show abc123`
- **THEN** the displayed title is `"New"`

#### Scenario: Write commands do not modify legacy files
- **WHEN** any fleece write command runs in a repository that has been migrated to event sourcing
- **THEN** no `.fleece/issues_*.jsonl` or `.fleece/tombstones_*.jsonl` file is created or modified
- **AND** `.fleece/issues.jsonl` is not modified by the command (only by `fleece project`)
