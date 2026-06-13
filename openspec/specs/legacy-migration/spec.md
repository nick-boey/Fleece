# Legacy Migration

## Purpose

Defines how Fleece migrates from the legacy hashed-file storage layout to the event-sourced layout via `fleece migrate`, including pre-3.0.0 intra-shape fixups, cross-file conflict reconciliation, and isolation of legacy types.
## Requirements
### Requirement: `fleece migrate` SHALL convert legacy hashed files into the event-sourced layout

`fleece migrate` SHALL remain available for converting pre-event-sourced legacy hashed files, but its help text SHALL be reframed for v4: all maintained repositories are already on the event-sourced layout, so `migrate` is a one-time bring-forward for old hashed-file repositories only. `migrate` SHALL NOT be the path for moving long-running issues to GitHub Issues — that is handled by `fleece prime v4-migration` and `fleece promote`. The command SHALL remain idempotent, exiting cleanly with "no migration needed" on an already-migrated repository.

#### Scenario: Migrate help reflects v4 framing
- **WHEN** `fleece migrate --help` is shown
- **THEN** the help describes a one-time legacy hashed-file bring-forward, not GitHub migration

#### Scenario: Idempotent on migrated repos
- **WHEN** `fleece migrate` runs on a repo already in the current layout
- **THEN** it exits cleanly reporting no migration needed

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

### Requirement: Pre-3.0.0 fixups SHALL run before merge

When `fleece migrate` reads legacy issues, it SHALL apply the pre-3.0.0 intra-shape fixup logic (currently `Fleece.Core.FunctionalCore.Legacy.LegacyMigration.Migrate`) to each parsed legacy issue *before* invoking the property-level cross-file merger. Specifically the fixups SHALL:

1. Backfill per-property `*LastUpdate` timestamps from the issue's top-level `LastUpdate` when they are zeroed.
2. Fold any present `LinkedPR` scalar into the `Tags` collection as a `hsp-linked-pr=<value>` keyed-tag (using `KeyedTag.LinkedPrKey`), and clear the scalar `LinkedPR` field.
3. Backfill `LastUpdated` on parent-issue references when zeroed.
4. Strip unknown JSON properties present on the parsed legacy issue.

#### Scenario: Pre-3.0.0 LinkedPR scalar is folded into Tags during migration
- **GIVEN** a repository with a legacy `.fleece/issues_aaa.jsonl` file containing a single issue with `"linkedPR": 42` and no `hsp-linked-pr=` entry in `tags`
- **WHEN** `fleece migrate` runs
- **THEN** `.fleece/issues.jsonl` contains a lean Issue whose `tags` array includes `"hsp-linked-pr=42"`
- **AND** the lean Issue's `linkedPR` field is `null`

#### Scenario: Pre-3.0.0 zeroed timestamps are backfilled before merge
- **GIVEN** two legacy files `.fleece/issues_aaa.jsonl` and `.fleece/issues_bbb.jsonl` that each contain the same issue ID with all `*LastUpdate` fields defaulted (zero) but with distinct top-level `LastUpdate` values
- **WHEN** `fleece migrate` runs
- **THEN** the cross-file merger receives issues whose per-property timestamps have been backfilled from each file's `LastUpdate`
- **AND** the resulting lean Issue reflects the more-recent `LastUpdate`'s property values rather than an arbitrary file-order choice

### Requirement: Legacy DTOs SHALL be isolated to a `Legacy` namespace

The pre-migration `Issue` model (carrying all `*LastUpdate` and `*ModifiedBy` fields) SHALL be relocated to the `Fleece.Core.Models.Legacy` namespace. The post-migration `Issue` model in `Fleece.Core.Models` SHALL be the lean form (no per-property timestamps).

Code under `Fleece.Core.Models.Legacy` SHALL be referenced only by the migration command. No production read or write path SHALL depend on the legacy types.

#### Scenario: Legacy types isolated to migration code path
- **WHEN** the codebase is searched for references to `Fleece.Core.Models.Legacy.LegacyIssue`
- **THEN** the only references are within the migration command and its tests
- **AND** no other CLI command or Core service references the legacy namespace

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

