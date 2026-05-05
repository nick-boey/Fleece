## ADDED Requirements

### Requirement: Git-backed IEventGitContext SHALL resolve commit ordinals for change files

The system SHALL implement a real git-backed `IEventGitContext` (named `GitEventContext`) that resolves the 0-based commit-ordinal position of each change file's introducing commit, using the current branch's `git rev-list --reverse HEAD` as the ordered reference. The ordinal SHALL be the index of the commit that first added the change file (via `git log --diff-filter=A`). Files not yet committed SHALL receive a `null` ordinal (sorted last by the replay engine). The `GitEventContext` SHALL also provide real `GetHeadSha()` and `IsFileCommittedAtHead()` implementations.

The DI container SHALL register `GitEventContext` when a git repository is detected at the base path, falling back to `NullEventGitContext.Instance` otherwise.

#### Scenario: Git-backed context resolves ordinals for committed change files
- **GIVEN** a git repository with commits C1, C2, C3 (in that order)
- **AND** change file `change_aaa.jsonl` was added in commit C1
- **AND** change file `change_bbb.jsonl` was added in commit C3
- **WHEN** `GitEventContext.GetFirstCommitOrdinal("change_aaa.jsonl")` is called
- **THEN** it returns 0 (oldest commit)
- **AND** `GetFirstCommitOrdinal("change_bbb.jsonl")` returns 2 (newest commit)

#### Scenario: Git-backed context returns null for uncommitted files
- **GIVEN** a git repository
- **AND** a change file exists on disk but has never been committed
- **WHEN** `GetFirstCommitOrdinal` is called for that file
- **THEN** it returns `null`

#### Scenario: NullEventGitContext is used when not in a git repository
- **GIVEN** a working directory that is not a git repository
- **WHEN** Fleece Core DI resolves `IEventGitContext`
- **THEN** it returns `NullEventGitContext.Instance`

### Requirement: Integration tests SHALL verify real git merge of branches editing the same issue

Integration tests (using real git) SHALL exercise the scenario where two branches independently edit the same issue and are then merged. After merge, the replay engine SHALL produce the correct state reflecting the event ordering from the merged branch.

#### Scenario: Merge two branches that edit the same issue scalars
- **GIVEN** a git repository with an issue "I1" on main
- **AND** branch "feature/a" edits I1's title to "Title A" and commits
- **AND** branch "feature/b" (forked from the same base) edits I1's status to "Complete" and commits
- **WHEN** branch "feature/b" is merged into branch "feature/a"
- **THEN** replay on the merge result produces issue "I1" with title "Title A" AND status "Complete"

#### Scenario: Replay after real squash merge matches pre-squash state
- **GIVEN** a git repository with an issue "I1" on main
- **AND** a feature branch with three commits each editing I1's title to a different value
- **WHEN** the feature branch is squash-merged to main using `git merge --squash`
- **THEN** replay on main produces the same final title as replay on the pre-merge feature branch

#### Scenario: After merge, later-committed parallel changes win on same property
- **GIVEN** a git repository with an issue "I1" on main
- **AND** branch "feature/a" edits I1's title to "From A" and commits at time T1
- **AND** branch "feature/b" (forked from same base) edits I1's title to "From B" and commits at time T2 (T2 > T1)
- **AND** both branches have no `follows` relationship (they are parallel DAG roots)
- **WHEN** a merge commit combines both branches
- **THEN** the final title of I1 is "From B" (the later-committed change file's events apply last and win)

### Requirement: Tests SHALL verify migration using the real legacy fixtures

Integration and E2E tests SHALL use the legacy issue files in `tests/examples/diff-issues/` and `tests/examples/nested-issues/` to verify the `fleece migrate` pipeline handles real-world data correctly.

#### Scenario: Migrate diff-issues fixture reconciles overlapping issues
- **GIVEN** the `tests/examples/diff-issues/` fixture containing two legacy hashed files with overlapping issue IDs
- **WHEN** `fleece migrate` runs in a repository seeded with those files
- **THEN** `.fleece/issues.jsonl` is produced containing the deduplicated set of issues
- **AND** each issue reflects the property values from the file with the more recent `*LastUpdate` timestamps
- **AND** no `*LastUpdate` or `*ModifiedBy` keys remain in the output

#### Scenario: Migrate nested-issues fixture preserves parent references
- **GIVEN** the `tests/examples/nested-issues/` fixture containing 13 issues with `parentIssues` chains
- **WHEN** `fleece migrate` runs
- **THEN** `.fleece/issues.jsonl` contains all 13 issues
- **AND** each issue with `parentIssues` in the legacy file retains those parent references in the lean format
- **AND** issue "ISSUE-005" has "ISSUE-004" as a parent via the `parentIssues` array

### Requirement: Commit-order tiebreak SHALL order parallel-root change files by git commit position

When the follows-DAG has multiple root nodes (no `follows` relationship between them), the replay engine SHALL use the commit-ordinal tiebreak to sort them: files introduced by earlier commits are replayed before files from later commits. Within the same commit, GUID alphabetical order is the tiebreak.

#### Scenario: Parallel roots sorted by commit order
- **GIVEN** commit C1 introduces `change_aaa.jsonl` (follows=null) setting title to "early"
- **AND** commit C2 introduces `change_bbb.jsonl` (follows=null) setting title to "late"
- **WHEN** replay processes both files
- **THEN** `change_aaa.jsonl` is replayed first (commit ordinal 0)
- **AND** `change_bbb.jsonl` is replayed second (commit ordinal 1)
- **AND** the final title is "late" (last write wins)

#### Scenario: Same-commit parallel roots fall back to GUID alphabetical
- **GIVEN** both `change_aaa.jsonl` and `change_bbb.jsonl` were introduced in the same commit (same ordinal)
- **WHEN** replay processes both files
- **THEN** `change_aaa.jsonl` is replayed before `change_bbb.jsonl` (GUID alphabetical tiebreak, "aaa" < "bbb")
