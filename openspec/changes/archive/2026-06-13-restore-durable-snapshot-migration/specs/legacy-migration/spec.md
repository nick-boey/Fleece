## ADDED Requirements

### Requirement: `fleece migrate` SHALL convert the legacy durable snapshot layout into per-issue logs

When run explicitly, `fleece migrate` SHALL auto-detect a repository in the legacy durable snapshot layout — a `.fleece/issues.jsonl` snapshot optionally layered with `.fleece/changes/change_{guid}.jsonl` change files — and convert it into the v4 per-issue log layout (`.fleece/issues/<id>.jsonl`), with no additional flag required. The conversion SHALL replay the snapshot together with all change files in their `follows` order to compute each issue's current state, project that state to the lean `Issue` shape, and write one `create` event per issue via the event store. On success it SHALL delete the consumed `.fleece/issues.jsonl` snapshot and the `.fleece/changes/` directory. The conversion SHALL be idempotent: a repository with no durable snapshot SHALL exit cleanly with no conversion performed.

#### Scenario: Snapshot-only repository is converted
- **WHEN** the durable conversion runs and `.fleece/issues.jsonl` exists with no `.fleece/changes/` files
- **THEN** a per-issue log is written under `.fleece/issues/` for each snapshot issue
- **AND** `.fleece/issues.jsonl` is deleted

#### Scenario: Snapshot plus change files is replayed in order
- **WHEN** the durable conversion runs and `.fleece/changes/` contains change files layered over the snapshot
- **THEN** each issue's converted state reflects the snapshot with all change-file events applied in `follows` order
- **AND** `.fleece/issues.jsonl` and the `.fleece/changes/` directory are deleted

#### Scenario: No flag required to convert the durable layout
- **WHEN** `fleece migrate` runs with no extra arguments in a repository containing `.fleece/issues.jsonl`
- **THEN** the durable layout is converted into per-issue logs

#### Scenario: Idempotent when no durable snapshot is present
- **WHEN** `fleece migrate` runs and no `.fleece/issues.jsonl` exists
- **THEN** no durable conversion is performed and the command exits cleanly

## MODIFIED Requirements

### Requirement: Legacy durable snapshot SHALL trigger a v4-migration warning

When any `fleece` command runs in a repository that still contains a legacy durable `.fleece/issues.jsonl` snapshot, Fleece SHALL print a warning informing the user that legacy Fleece issues are present and instructing them to run `fleece prime v4-migration`. The warning SHALL be non-destructive — no legacy data is automatically converted or deleted. Converting the durable layout into per-issue logs SHALL occur only when the user explicitly runs `fleece migrate`; the command interceptor's automatic migration SHALL NOT convert the durable layout.

#### Scenario: Warning on legacy snapshot presence
- **WHEN** a `fleece` command runs and `.fleece/issues.jsonl` exists
- **THEN** a warning is printed pointing the user to `fleece prime v4-migration`
- **AND** the legacy snapshot is not modified or deleted

#### Scenario: Durable snapshot is not auto-converted by the interceptor
- **WHEN** a `fleece` command other than `migrate` runs and `.fleece/issues.jsonl` exists
- **THEN** the interceptor's automatic migration does not convert the durable snapshot
- **AND** the snapshot and any `.fleece/changes/` files remain on disk

#### Scenario: No warning once the legacy snapshot is gone
- **WHEN** a `fleece` command runs and no `.fleece/issues.jsonl` exists
- **THEN** no legacy-migration warning is printed
