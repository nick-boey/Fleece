## ADDED Requirements

### Requirement: `fleece migrate` SHALL convert legacy hashed files into the event-sourced layout

Fleece SHALL convert the legacy storage layout into the event-sourced layout via the existing `fleece migrate` command (the canonical "bring my data up to the current schema" command). No new sibling command (e.g. `migrate-events`) SHALL be introduced. The command SHALL:

1. Locate every `.fleece/issues_*.jsonl` file in the working tree.
2. Parse them using the legacy issue shape (the pre-migration `Issue` model with all `*LastUpdate` and `*ModifiedBy` fields).
3. **Apply pre-3.0.0 intra-shape fixups** to each parsed legacy issue *before* cross-file reconciliation (see "Pre-3.0.0 fixups SHALL run before merge" below).
4. Reconcile per-issue conflicts using the existing property-by-property timestamp-based merge logic, producing a single merged set of issues.
5. Write the merged set to `.fleece/issues.jsonl` in the new lean format (no `*LastUpdate` or `*ModifiedBy` fields, retaining `id`, `title`, `description`, `status`, `type`, `linkedPR`, `linkedIssues`, `parentIssues`, `priority`, `assignedTo`, `tags`, `workingBranchId`, `executionMode`, `createdBy`, `createdAt`, `lastUpdate`).
6. Locate every `.fleece/tombstones_*.jsonl` file, union their entries, and write them to `.fleece/tombstones.jsonl`.
7. Delete every legacy `.fleece/issues_*.jsonl` and `.fleece/tombstones_*.jsonl` file.
8. Create the `.fleece/changes/` directory if it does not exist.
9. Add `.fleece/.active-change` and `.fleece/.replay-cache` to the repository's `.gitignore` if not already present.

The migration SHALL NOT emit any events. Per-property timestamps and modifier history SHALL be discarded during projection to the lean shape.

The migration SHALL be safe to invoke on a repository that has already been migrated (no legacy files present): it SHALL detect this and exit with status code 0 and a message indicating no work was needed.

#### Scenario: `fleece migrate` converts legacy files into new format
- **GIVEN** a repository with `.fleece/issues_aaa.jsonl` (3 issues) and `.fleece/issues_bbb.jsonl` (2 issues, 1 overlapping with `issues_aaa.jsonl`)
- **WHEN** `fleece migrate` runs
- **THEN** `.fleece/issues.jsonl` exists with 4 distinct issues (overlap reconciled by property-level timestamp merge)
- **AND** `.fleece/issues_aaa.jsonl` and `.fleece/issues_bbb.jsonl` are deleted
- **AND** no JSON object in `.fleece/issues.jsonl` contains keys ending in `LastUpdate` or `ModifiedBy`

#### Scenario: `fleece migrate` unions tombstones
- **GIVEN** a repository with `.fleece/tombstones_aaa.jsonl` (2 entries) and `.fleece/tombstones_bbb.jsonl` (1 entry)
- **WHEN** `fleece migrate` runs
- **THEN** `.fleece/tombstones.jsonl` contains the union of all 3 entries (deduplicated by issue ID)
- **AND** the legacy tombstone files are deleted

#### Scenario: `fleece migrate` is idempotent on already-migrated repository
- **GIVEN** a repository with `.fleece/issues.jsonl` and no `.fleece/issues_*.jsonl` files
- **WHEN** `fleece migrate` runs
- **THEN** the command exits with status code 0
- **AND** prints a message indicating no migration was needed
- **AND** no files are modified

#### Scenario: `fleece migrate` adds gitignore entries
- **GIVEN** a repository whose `.gitignore` does not contain `.fleece/.active-change`
- **WHEN** `fleece migrate` runs
- **THEN** the resulting `.gitignore` contains entries for `.fleece/.active-change` and `.fleece/.replay-cache`

#### Scenario: No separate `migrate-events` command exists
- **WHEN** the user runs `fleece migrate-events`
- **THEN** the command is unrecognised (no `migrate-events` registration in the CLI)
- **AND** `fleece migrate` is the only command that performs schema migration

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
