## MODIFIED Requirements

### Requirement: `fleece install` SHALL install a pre-commit hook that stages fleece data and writes merge markers

The `fleece install` command SHALL write or update `.git/hooks/pre-commit` so that, on every commit, the hook:

1. If `.git/MERGE_HEAD` exists (i.e., the commit being created is a conflict-resolved merge commit), invokes `fleece link --merge` to write a merge marker change file.
2. Runs `git add .fleece/changes/` (and any other paths the projection writes on the default branch, e.g., `.fleece/issues.jsonl` and `.fleece/tombstones.jsonl`) to stage the active change file plus any merge marker just written.

The hook SHALL be idempotent: running `fleece install` repeatedly SHALL NOT duplicate the staging logic. If a `.git/hooks/pre-commit` file already exists with content unrelated to fleece, the command SHALL append a clearly demarcated fleece block (between unique start and end marker comments) without overwriting existing content.

The hook SHALL exit with status code 0 even when there is nothing to stage (e.g., no fleece edits in the commit).

The command SHALL NOT install a post-commit hook for fleece-related rotation.

#### Scenario: Fresh install writes pre-commit hook
- **GIVEN** a repository with no `.git/hooks/pre-commit` file
- **WHEN** the user runs `fleece install`
- **THEN** `.git/hooks/pre-commit` exists and is executable
- **AND** the hook contains a `git add .fleece/changes/` invocation between fleece marker comments
- **AND** the hook contains a `fleece link --merge` invocation guarded by `[ -f .git/MERGE_HEAD ]`

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

#### Scenario: Hook writes a marker during a conflict-resolved merge commit
- **GIVEN** the pre-commit hook is installed
- **AND** a `git merge` produced conflicts that the user has resolved
- **AND** `.git/MERGE_HEAD` exists
- **WHEN** the user runs `git commit`
- **THEN** the hook invokes `fleece link --merge` before staging
- **AND** the resulting commit contains a new `change_*.jsonl` whose meta event's `follows` is an array of the two side leaves

## ADDED Requirements

### Requirement: `fleece install` SHALL install a pre-merge-commit hook that writes merge markers

The `fleece install` command SHALL write or update `.git/hooks/pre-merge-commit` so that, on every clean auto-merge commit (i.e., one git creates without invoking the editor or requiring conflict resolution), the hook:

1. Invokes `fleece link --merge` to write a merge marker change file. If `fleece link` fails (non-zero exit), the hook SHALL exit non-zero, aborting the merge — this surfaces install/path problems early rather than silently producing unmarked merges.
2. Runs `git add .fleece/changes/` to stage the marker.

The hook SHALL be idempotent via the same marker-comment block mechanism as the pre-commit hook. Running `fleece install` repeatedly SHALL NOT duplicate the block.

The hook SHALL be set executable.

`pre-merge-commit` requires git 2.24 or later. `fleece install` SHALL print a one-line notice to stdout when it cannot detect a sufficient git version; merges on older git versions will not produce markers and will rely on the replay warning instead.

#### Scenario: Fresh install writes pre-merge-commit hook
- **GIVEN** a repository with no `.git/hooks/pre-merge-commit` file
- **WHEN** the user runs `fleece install`
- **THEN** `.git/hooks/pre-merge-commit` exists and is executable
- **AND** the hook invokes `fleece link --merge` between fleece marker comments
- **AND** the hook stages `.fleece/changes/`

#### Scenario: Clean auto-merge writes a marker via the pre-merge-commit hook
- **GIVEN** both hooks are installed
- **AND** two branches with non-conflicting changes are about to be merged
- **WHEN** the user runs `git merge feature/b`
- **THEN** the resulting merge commit contains a new `change_*.jsonl` whose meta event's `follows` is the list of side leaves
- **AND** no follow-up commit is created

#### Scenario: Fast-forward merge writes no marker
- **GIVEN** both hooks are installed
- **AND** a fast-forward-able merge is about to occur
- **WHEN** the user runs `git merge feature/b`
- **THEN** no `change_*.jsonl` is added (no merge commit, no hook fires)

#### Scenario: Re-running install is idempotent for pre-merge-commit
- **GIVEN** the pre-merge-commit hook is already installed
- **WHEN** the user runs `fleece install` again
- **THEN** the hook contains exactly one fleece block

### Requirement: `fleece link --merge` SHALL write a merge marker change file when a merge is in progress

The `fleece link --merge` command SHALL:

1. Detect whether a merge is in progress by checking for the presence of `.git/MERGE_HEAD`. If absent, the command SHALL exit 0 with no output and no file changes (this enables unconditional invocation from hooks).
2. Compute the DAG leaves on the "our side" (current `HEAD`) and on each "their side" referenced by `.git/MERGE_HEAD` (one entry for two-way merges, multiple for octopus merges).
3. Generate a fresh GUID and write a new `.fleece/changes/change_{guid}.jsonl` file whose first line is `{"kind":"meta","follows":[<all-parent-leaves>]}` with no body events. Duplicate parent GUIDs SHALL be deduplicated.
4. Stage the new file via `git add` so it is included in the merge commit.

If both sides of the merge resolve to the same leaf (no parallel chains exist), the command SHALL still write a marker for symmetry — its `follows` will be a single-element list, serialised as scalar — at negligible cost.

The command SHALL also be invocable manually (`fleece link --merge` outside a hook) for post-hoc reconciliation of legacy merge commits that predate hook installation.

#### Scenario: Link with no merge in progress is a no-op
- **WHEN** the user runs `fleece link --merge` and `.git/MERGE_HEAD` does not exist
- **THEN** the command exits 0
- **AND** no files are created or modified

#### Scenario: Link during a two-way merge writes a two-parent marker
- **GIVEN** a merge is in progress with our-leaf `"a"` and their-leaf `"b"`
- **WHEN** the user (or hook) runs `fleece link --merge`
- **THEN** a new change file is created with first line `{"kind":"meta","follows":["a","b"]}`
- **AND** the file is staged

#### Scenario: Link during an octopus merge writes an N-parent marker
- **GIVEN** an octopus merge with our-leaf `"a"` and three their-leaves `"b"`, `"c"`, `"d"`
- **WHEN** the user (or hook) runs `fleece link --merge`
- **THEN** the new marker's meta event has `follows=["a","b","c","d"]`
