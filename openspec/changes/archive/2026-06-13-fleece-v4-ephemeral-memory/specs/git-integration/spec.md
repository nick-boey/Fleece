## MODIFIED Requirements

### Requirement: `fleece install` SHALL install a pre-commit hook that stages fleece data and writes merge markers

The `fleece install` command SHALL write or update `.git/hooks/pre-commit` so that, on every commit, the hook stages the contents of the `.fleece/` directory. The hook SHALL NOT write merge markers (the merge-marker mechanism is removed). The hook MAY additionally print a non-blocking warning reporting the count of active issues (`Open`/`Progress`/`Review`) and a hint to run `fleece seal` before opening a PR; this warning SHALL NOT block the commit.

#### Scenario: Pre-commit stages fleece data
- **WHEN** a commit is made after `fleece install`
- **THEN** the pre-commit hook stages `.fleece/` changes
- **AND** does not write any merge-marker file

#### Scenario: Active-issue warning is non-blocking
- **WHEN** a commit is made while active issues exist
- **THEN** the hook may print an active-issue count and a seal hint
- **AND** the commit still proceeds

### Requirement: `fleece install` SHALL write a daily projection GitHub Action template

The `fleece install` command SHALL write a GitHub Action workflow that enforces the empty-live-issues gate on pull requests targeting the default branch. The workflow SHALL run a cross-platform check (bash on Linux/macOS runners, PowerShell on Windows runners) requiring no Fleece binary, failing the check when `.fleece/issues/` contains any `*.jsonl` file. It SHALL NOT perform projection or run `fleece project` (which is removed).

#### Scenario: Installed Action gates on live issues
- **WHEN** `fleece install` runs
- **THEN** a GitHub Action workflow is written that fails a PR when `.fleece/issues/` contains `*.jsonl` files
- **AND** the workflow requires no Fleece binary on the runner

#### Scenario: Cross-platform gate
- **WHEN** the installed workflow runs on a Windows runner
- **THEN** it performs the same empty-`.fleece/issues/` check via PowerShell

## REMOVED Requirements

### Requirement: `fleece install` SHALL install a pre-merge-commit hook that writes merge markers
**Reason**: Per-issue logs eliminate the follows-DAG, so there is no merge topology to capture; the merge-marker mechanism is removed.
**Migration**: Re-run `fleece install` to remove the pre-merge-commit hook; no replacement is needed.

### Requirement: `fleece link --merge` SHALL write a merge marker change file when a merge is in progress
**Reason**: Merge markers and the `fleece link` command are removed along with the follows-DAG.
**Migration**: None — `fleece link` is removed.

### Requirement: `fleece merge` SHALL be deprecated in favor of `fleece project`
**Reason**: Both `fleece merge` and `fleece project` are removed; branch sealing is handled by `fleece seal`.
**Migration**: Use `fleece seal` to archive and clear inactive issues before merging.

## ADDED Requirements

### Requirement: `defaultBranch` configuration SHALL be removed

Fleece SHALL NOT expose a `defaultBranch` configuration setting. Its only consumer — the `fleece project` default-branch refusal — is removed. The CI gate targets pull requests via GitHub workflow configuration, and `absorb` records the current branch from git, so no Fleece-side default-branch setting is required.

#### Scenario: defaultBranch is not a recognised setting
- **WHEN** `fleece config --set defaultBranch=main` runs
- **THEN** the command reports `defaultBranch` as an unknown setting
