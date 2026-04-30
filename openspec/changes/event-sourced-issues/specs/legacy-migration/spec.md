## ADDED Requirements

### Requirement: Migration command SHALL convert legacy hashed files into the event-sourced layout

Fleece SHALL provide a one-shot migration command that converts the legacy storage layout into the event-sourced layout. The command SHALL:

1. Locate every `.fleece/issues_*.jsonl` file in the working tree.
2. Parse them using the legacy issue shape (the pre-migration `Issue` model with all `*LastUpdate` and `*ModifiedBy` fields).
3. Reconcile per-issue conflicts using the existing property-by-property timestamp-based merge logic, producing a single merged set of issues.
4. Write the merged set to `.fleece/issues.jsonl` in the new lean format (no `*LastUpdate` or `*ModifiedBy` fields, retaining `id`, `title`, `description`, `status`, `type`, `linkedPR`, `linkedIssues`, `parentIssues`, `priority`, `assignedTo`, `tags`, `workingBranchId`, `executionMode`, `createdBy`, `createdAt`, `lastUpdate`).
5. Locate every `.fleece/tombstones_*.jsonl` file, union their entries, and write them to `.fleece/tombstones.jsonl`.
6. Delete every legacy `.fleece/issues_*.jsonl` and `.fleece/tombstones_*.jsonl` file.
7. Create the `.fleece/changes/` directory if it does not exist.
8. Add `.fleece/.active-change` and `.fleece/.replay-cache` to the repository's `.gitignore` if not already present.

The migration SHALL NOT emit any events. Per-property timestamps and modifier history SHALL be discarded.

The migration SHALL be safe to invoke on a repository that has already been migrated (no legacy files present): it SHALL detect this and exit with status code 0 and a message indicating no work was needed.

#### Scenario: Migration converts legacy files into new format
- **GIVEN** a repository with `.fleece/issues_aaa.jsonl` (3 issues) and `.fleece/issues_bbb.jsonl` (2 issues, 1 overlapping with `issues_aaa.jsonl`)
- **WHEN** the migration command runs
- **THEN** `.fleece/issues.jsonl` exists with 4 distinct issues (overlap reconciled by property-level timestamp merge)
- **AND** `.fleece/issues_aaa.jsonl` and `.fleece/issues_bbb.jsonl` are deleted
- **AND** no JSON object in `.fleece/issues.jsonl` contains keys ending in `LastUpdate` or `ModifiedBy`

#### Scenario: Migration unions tombstones
- **GIVEN** a repository with `.fleece/tombstones_aaa.jsonl` (2 entries) and `.fleece/tombstones_bbb.jsonl` (1 entry)
- **WHEN** the migration command runs
- **THEN** `.fleece/tombstones.jsonl` contains the union of all 3 entries (deduplicated by issue ID)
- **AND** the legacy tombstone files are deleted

#### Scenario: Migration is idempotent on already-migrated repository
- **GIVEN** a repository with `.fleece/issues.jsonl` and no `.fleece/issues_*.jsonl` files
- **WHEN** the migration command runs
- **THEN** the command exits with status code 0
- **AND** prints a message indicating no migration was needed
- **AND** no files are modified

#### Scenario: Migration adds gitignore entries
- **GIVEN** a repository whose `.gitignore` does not contain `.fleece/.active-change`
- **WHEN** the migration command runs
- **THEN** the resulting `.gitignore` contains entries for `.fleece/.active-change` and `.fleece/.replay-cache`

### Requirement: Legacy DTOs SHALL be isolated to a `Legacy` namespace

The pre-migration `Issue` model (carrying all `*LastUpdate` and `*ModifiedBy` fields) SHALL be relocated to the `Fleece.Core.Models.Legacy` namespace. The post-migration `Issue` model in `Fleece.Core.Models` SHALL be the lean form (no per-property timestamps).

Code under `Fleece.Core.Models.Legacy` SHALL be referenced only by the migration command. No production read or write path SHALL depend on the legacy types.

#### Scenario: Legacy types isolated to migration code path
- **WHEN** the codebase is searched for references to `Fleece.Core.Models.Legacy.LegacyIssue`
- **THEN** the only references are within the migration command and its tests
- **AND** no other CLI command or Core service references the legacy namespace
