# Event-Sourced Storage

## Purpose

Defines how Fleece persists issue data using an event-sourced model: a projected snapshot file plus per-session append-only change files, with deterministic replay ordering via a follows-DAG.
## Requirements
### Requirement: Issue data SHALL be persisted as a snapshot file plus per-session change files

Fleece SHALL persist each issue as a single append-only event log at `.fleece/issues/<id>.jsonl`, where `<id>` is the issue id. There SHALL be no projected snapshot file and no shared per-session change files. The first line of each file SHALL be a `create` event; subsequent lines SHALL be `set`/`add`/`remove` event objects in append order. File order within a single issue's log is authoritative — there is no cross-file ordering, no `follows` pointer, and no `meta` event. The combined state of (all `.fleece/issues/*.jsonl` files, each replayed independently) SHALL be the source of truth for any read.

#### Scenario: Each issue is its own append-only log
- **WHEN** an issue with id `a1b2c3` is created and later edited
- **THEN** all of its events live in `.fleece/issues/a1b2c3.jsonl`
- **AND** the first line is a `create` event and later lines are `set`/`add`/`remove` events

#### Scenario: No snapshot or shared change files exist
- **WHEN** the `.fleece/` directory is inspected after any write
- **THEN** there is no `.fleece/issues.jsonl` snapshot and no `.fleece/changes/` directory

### Requirement: Change files SHALL contain only the defined event kinds

Each per-issue log file SHALL contain only the event kinds `create`, `set`, `add`, and `remove`. It SHALL NOT contain `meta`, `follows`, `hard-delete`, or merge-marker events.

#### Scenario: Only issue-mutation events are present
- **WHEN** any `.fleece/issues/*.jsonl` file is read
- **THEN** every line's `kind` is one of `create`, `set`, `add`, or `remove`

### Requirement: All read paths SHALL replay snapshot plus change files in DAG order

All read paths SHALL load every `.fleece/issues/*.jsonl` file and replay each file independently in its own append order. There SHALL be no topological ordering across files, no commit-order or GUID tiebreak, and no merge-marker resolution.

#### Scenario: Reads replay per-issue files independently
- **WHEN** a list/show/search read executes
- **THEN** each issue's current state is computed by replaying only its own `.fleece/issues/<id>.jsonl` file
- **AND** no ordering relationship between separate issue files is consulted

### Requirement: `fleece delete` SHALL remove an issue by deleting its log file

The `fleece delete <id>` command SHALL remove an issue by deleting its `.fleece/issues/<id>.jsonl` file. No tombstone record SHALL be written and no `.fleece/tombstones.jsonl` sidecar SHALL be maintained. Because issues are branch-scoped and never compacted into a durable snapshot, id-collision bookkeeping across deletions is not required.

#### Scenario: Delete removes the issue file
- **WHEN** `fleece delete a1b2c3` runs
- **THEN** `.fleece/issues/a1b2c3.jsonl` no longer exists
- **AND** no tombstone record is written

#### Scenario: No tombstone sidecar is maintained
- **WHEN** the `.fleece/` directory is inspected after a delete
- **THEN** there is no `.fleece/tombstones.jsonl` file

