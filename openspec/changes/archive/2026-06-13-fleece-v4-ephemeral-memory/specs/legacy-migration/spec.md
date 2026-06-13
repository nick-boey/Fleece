## MODIFIED Requirements

### Requirement: `fleece migrate` SHALL convert legacy hashed files into the event-sourced layout

`fleece migrate` SHALL remain available for converting pre-event-sourced legacy hashed files, but its help text SHALL be reframed for v4: all maintained repositories are already on the event-sourced layout, so `migrate` is a one-time bring-forward for old hashed-file repositories only. `migrate` SHALL NOT be the path for moving long-running issues to GitHub Issues — that is handled by `fleece prime v4-migration` and `fleece promote`. The command SHALL remain idempotent, exiting cleanly with "no migration needed" on an already-migrated repository.

#### Scenario: Migrate help reflects v4 framing
- **WHEN** `fleece migrate --help` is shown
- **THEN** the help describes a one-time legacy hashed-file bring-forward, not GitHub migration

#### Scenario: Idempotent on migrated repos
- **WHEN** `fleece migrate` runs on a repo already in the current layout
- **THEN** it exits cleanly reporting no migration needed

## ADDED Requirements

### Requirement: Legacy durable snapshot SHALL trigger a v4-migration warning

When any `fleece` command runs in a repository that still contains a legacy durable `.fleece/issues.jsonl` snapshot, Fleece SHALL print a warning informing the user that legacy Fleece issues are present and instructing them to run `fleece prime v4-migration` to migrate long-running issues to GitHub Issues. The warning SHALL be non-destructive — no legacy data is automatically converted or deleted.

#### Scenario: Warning on legacy snapshot presence
- **WHEN** a `fleece` command runs and `.fleece/issues.jsonl` exists
- **THEN** a warning is printed pointing the user to `fleece prime v4-migration`
- **AND** the legacy snapshot is not modified or deleted

#### Scenario: No warning once the legacy snapshot is gone
- **WHEN** a `fleece` command runs and no `.fleece/issues.jsonl` exists
- **THEN** no legacy-migration warning is printed
