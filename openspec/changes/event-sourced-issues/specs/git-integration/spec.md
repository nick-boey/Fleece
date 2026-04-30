## ADDED Requirements

### Requirement: `fleece install` SHALL install a pre-commit hook that stages fleece data

The `fleece install` command SHALL write or update `.git/hooks/pre-commit` so that, on every commit, the hook runs `git add .fleece/changes/` (and any other paths the projection writes on the default branch, e.g., `.fleece/issues.jsonl` and `.fleece/tombstones.jsonl`).

The hook SHALL be idempotent: running `fleece install` repeatedly SHALL NOT duplicate the staging logic. If a `.git/hooks/pre-commit` file already exists with content unrelated to fleece, the command SHALL append a clearly demarcated fleece block (between unique start and end marker comments) without overwriting existing content.

The hook SHALL exit with status code 0 even when there is nothing to stage (e.g., no fleece edits in the commit).

The command SHALL NOT install a post-commit hook for fleece-related rotation.

#### Scenario: Fresh install writes pre-commit hook
- **GIVEN** a repository with no `.git/hooks/pre-commit` file
- **WHEN** the user runs `fleece install`
- **THEN** `.git/hooks/pre-commit` exists and is executable
- **AND** the hook contains a `git add .fleece/changes/` invocation between fleece marker comments

#### Scenario: Re-running install is idempotent
- **GIVEN** a repository where `fleece install` has already run once
- **WHEN** the user runs `fleece install` again
- **THEN** the pre-commit hook contains exactly one fleece block (no duplication)

#### Scenario: Existing non-fleece pre-commit hook is preserved
- **GIVEN** a repository with a `.git/hooks/pre-commit` containing user-authored shell commands unrelated to fleece
- **WHEN** the user runs `fleece install`
- **THEN** the original user-authored commands are preserved verbatim
- **AND** a fleece block is appended between marker comments

#### Scenario: No post-commit hook is installed
- **WHEN** the user runs `fleece install`
- **THEN** no `.git/hooks/post-commit` file is created or modified by fleece

#### Scenario: Hook is robust to no-op commits
- **GIVEN** the pre-commit hook is installed
- **AND** `.fleece/changes/` is empty
- **WHEN** the user runs `git commit -m "non-fleece change"`
- **THEN** the commit succeeds
- **AND** no error is emitted by the hook

### Requirement: `fleece install` SHALL write a daily projection GitHub Action template

The `fleece install` command SHALL write a workflow file at `.github/workflows/fleece-project.yml` containing a GitHub Action that:

- Triggers on `schedule` with a cron expression running once per day, AND on `workflow_dispatch` (manual trigger).
- Checks out the default branch with full history.
- Installs the Fleece CLI (or uses a prebuilt binary, as appropriate for the repository's setup).
- Runs `fleece project`.
- Commits any resulting changes with a message such as `chore: project fleece events` and pushes back to the default branch.

The command SHALL skip writing the workflow file if a file already exists at that path, printing a warning that the user must reconcile manually.

The command SHALL NOT write the workflow file if `.github/workflows/` does not already exist or if the repository is not detected as a GitHub repository (presence of `.git/config` referencing a github.com remote).

#### Scenario: Install creates daily projection workflow
- **GIVEN** a repository with a github.com remote and a `.github/workflows/` directory
- **WHEN** the user runs `fleece install`
- **THEN** `.github/workflows/fleece-project.yml` exists
- **AND** the file contains both `schedule:` and `workflow_dispatch:` triggers
- **AND** the file invokes `fleece project`

#### Scenario: Install does not overwrite existing workflow
- **GIVEN** a repository where `.github/workflows/fleece-project.yml` already exists with custom content
- **WHEN** the user runs `fleece install`
- **THEN** the existing file is unchanged
- **AND** a warning is printed indicating the user must reconcile manually

#### Scenario: Install skips workflow on non-GitHub repository
- **GIVEN** a repository with no github.com remote
- **WHEN** the user runs `fleece install`
- **THEN** no file is written under `.github/workflows/`
- **AND** the pre-commit hook is still installed

### Requirement: `fleece merge` SHALL be deprecated in favor of `fleece project`

The `fleece merge` command SHALL print a deprecation notice on every invocation, pointing the user at `fleece project`. The command SHALL still execute its existing behavior for the duration of one release cycle, after which it SHALL be removed.

#### Scenario: Merge prints deprecation notice
- **WHEN** a user runs `fleece merge`
- **THEN** a clearly-marked deprecation notice is printed to stderr
- **AND** the notice points the user at `fleece project`
- **AND** the existing merge behavior still executes
