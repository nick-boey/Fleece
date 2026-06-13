# branch-lifecycle Specification

## Purpose
TBD - created by archiving change fleece-v4-ephemeral-memory. Update Purpose after archive.
## Requirements
### Requirement: `fleece seal` SHALL archive and clear issues only when all are inactive

The `fleece seal` command SHALL be the "finish the branch" operation. It SHALL succeed only when **every** issue is in an inactive status (`Complete`, `Closed`, or `Promoted`). When any issue is in an active status (`Open`, `Progress`, or `Review`), the command SHALL make no changes and SHALL print a warning listing each remaining active issue by id, title, and status, and SHALL exit with a non-zero status.

#### Scenario: Seal refuses while active issues remain
- **WHEN** `fleece seal` runs and at least one issue has status `Open`, `Progress`, or `Review`
- **THEN** no files under `.fleece/issues/` are removed and no archive file is written
- **AND** stderr lists each active issue's id, title, and status
- **AND** the process exits non-zero

#### Scenario: Seal succeeds when all issues are inactive
- **WHEN** `fleece seal` runs and every issue has status `Complete`, `Closed`, or `Promoted`
- **THEN** the command writes `.fleece/archive/issues_<contenthash>.jsonl` containing the sealed issue set
- **AND** removes every `.fleece/issues/*.jsonl` file
- **AND** exits zero

#### Scenario: Seal on an empty issue set is a no-op success
- **WHEN** `fleece seal` runs and `.fleece/issues/` contains no issue files
- **THEN** the command exits zero without writing a new archive file

### Requirement: The seal archive SHALL be an immutable content-addressed audit log

The archive file `fleece seal` writes SHALL be named `issues_<contenthash>.jsonl` where `<contenthash>` is derived from the canonicalised content of the sealed issue set, so identical logical state yields a single stable archive file. Archive files SHALL be treated as immutable and are the only Fleece issue data permitted to land on the default branch.

#### Scenario: Identical sealed state produces a stable archive name
- **WHEN** two seals occur over an identical logical set of issues
- **THEN** both produce an archive file with the same `<contenthash>` name

#### Scenario: Archive is written under `.fleece/archive/`
- **WHEN** `fleece seal` succeeds with a non-empty issue set
- **THEN** the archive file is created under `.fleece/archive/` and the live `.fleece/issues/` directory is left empty

### Requirement: A mergeable branch SHALL have an empty live issues directory

The CI gate installed by `fleece install` SHALL enforce the invariant that a branch eligible to merge has no live Fleece issues: `.fleece/issues/` SHALL contain no `*.jsonl` files. The gate SHALL be a cross-platform script requiring no Fleece binary on the runner, and SHALL fail the check when any live issue file is present.

#### Scenario: CI gate fails when live issue files exist
- **WHEN** the CI gate runs on a branch whose `.fleece/issues/` contains one or more `*.jsonl` files
- **THEN** the gate exits non-zero and the PR check fails

#### Scenario: CI gate passes when the live issues directory is empty or absent
- **WHEN** the CI gate runs on a branch whose `.fleece/issues/` is empty or does not exist
- **THEN** the gate exits zero and the PR check passes

