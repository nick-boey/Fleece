## MODIFIED Requirements

### Requirement: Active change file selection SHALL be deterministic and recover from a missing pointer

On every fleece write, the system SHALL determine the active change file via the following algorithm:

1. Read `.fleece/.active-change` (a JSON file containing at minimum `{"guid":"<guid>"}`).
2. If the file does not exist, OR the GUID it references has no corresponding `.fleece/changes/change_{guid}.jsonl` file on disk, OR the referenced file is already committed at HEAD (per `IEventGitContext.IsFileCommittedAtHead`), generate a fresh GUID:
   - Scan all existing `.fleece/changes/change_*.jsonl` files.
   - Read each first line to build a follows-DAG.
   - Find a leaf of the DAG (a node with no descendants); if multiple leaves exist, pick deterministically by GUID alphabetical order; if none exist (no change files), use `null`.
   - Create `.fleece/changes/change_{newguid}.jsonl` and write its first line as `{"kind":"meta","follows":"<leaf-or-null>"}`.
   - Write `.fleece/.active-change` with `{"guid":"<newguid>"}`.
3. Otherwise, reuse the active change file (append to it).

The new "committed at HEAD" trigger ensures that change files are **immutable once committed**: any edit after the file lands in HEAD rotates to a fresh GUID whose `follows` chains back to the just-sealed file. This eliminates the merge-conflict failure mode that arose when the same GUID was reused across branches.

When `IEventGitContext` is the `NullEventGitContext` (no git repository), the "committed at HEAD" trigger is permanently false; rotation behaviour reverts to the pre-change "rotate only on missing pointer or missing file" model. This is acceptable for non-git consumers of Fleece.Core because they have no commits to rotate against.

`.fleece/.active-change` and `.fleece/.replay-cache` SHALL be added to `.gitignore` automatically by `fleece install` and verified on every fleece operation.

#### Scenario: Active pointer missing triggers fresh GUID generation
- **WHEN** `.fleece/.active-change` does not exist
- **AND** a fleece write operation runs
- **THEN** a new `change_{guid}.jsonl` file is created in `.fleece/changes/`
- **AND** `.fleece/.active-change` is written referencing the new GUID
- **AND** the new file's first line is a meta event with `follows` set to the existing leaf GUID (or `null` if no change files existed)

#### Scenario: Active pointer references missing file triggers rotation
- **WHEN** `.fleece/.active-change` references GUID `"old"`
- **AND** `.fleece/changes/change_old.jsonl` does not exist on disk (e.g., deleted on branch switch)
- **AND** a fleece write operation runs
- **THEN** a new GUID is generated and the active pointer is updated

#### Scenario: Active pointer references committed file triggers rotation
- **WHEN** `.fleece/.active-change` references GUID `"sealed"`
- **AND** `.fleece/changes/change_sealed.jsonl` exists on disk AND is committed at HEAD
- **AND** a fleece write operation runs
- **THEN** a new GUID `"fresh"` is generated and the active pointer is updated
- **AND** `change_fresh.jsonl` is created with `{"kind":"meta","follows":"sealed"}`
- **AND** no further events are appended to `change_sealed.jsonl`

#### Scenario: Multiple edits within one commit accumulate into a single file
- **GIVEN** the active file `change_x.jsonl` exists on disk and is NOT yet committed at HEAD
- **WHEN** three consecutive fleece edits run with no `git commit` between them
- **THEN** all three events are appended to `change_x.jsonl`
- **AND** no rotation occurs

#### Scenario: Same worktree branching twice from main produces distinct change files
- **GIVEN** issue created on main, committed (change file sealed)
- **WHEN** the user branches feature/a, edits, commits
- **AND** the user switches back to main, branches feature/b, edits, commits
- **THEN** feature/a's commit contains a fresh change file with `follows` pointing at main's sealed file
- **AND** feature/b's commit contains a different fresh change file also with `follows` pointing at main's sealed file
- **AND** merging both branches into main produces no content conflicts on any change file

### Requirement: Change files SHALL contain only the defined event kinds

Each non-meta line in a change file SHALL be a JSON object with a `kind` property whose value is one of: `create`, `set`, `add`, `remove`, `hard-delete`. The shape of each event SHALL be:

- `create`: `{ "kind":"create", "at":"<ISO-8601>", "by":"<user|null>", "issueId":"<id>", "data": { ... initial property bag including title, type, status, createdAt, ... } }`
- `set`: `{ "kind":"set", "at":"<ISO-8601>", "by":"<user|null>", "issueId":"<id>", "property":"<name>", "value":<scalar-or-null> }`
- `add`: `{ "kind":"add", "at":"<ISO-8601>", "by":"<user|null>", "issueId":"<id>", "property":"<name>", "value":<element> }`
- `remove`: `{ "kind":"remove", "at":"<ISO-8601>", "by":"<user|null>", "issueId":"<id>", "property":"<name>", "value":<element-or-key> }`
- `hard-delete`: `{ "kind":"hard-delete", "at":"<ISO-8601>", "by":"<user|null>", "issueId":"<id>" }`

The first line of every change file SHALL be a `meta` event of the form `{"kind":"meta","follows":<value>}` where `<value>` is one of:

- `null` — the file is a root (no predecessor).
- A string GUID — the file follows a single predecessor (most common; chains within one branch).
- An array of one or more string GUIDs — the file is a merge marker linking multiple predecessor chains (one entry per parent leaf). The array form SHALL be used when length >= 2; length-1 arrays SHALL be written as the scalar form to minimise diff churn. Readers SHALL accept all three forms.

For array properties whose elements are structured (e.g., `parentIssues`), `remove` SHALL match elements by their natural key only (e.g., `parentIssue` ID), not by full object equality.

Unknown `kind` values SHALL cause replay to fail with a clear diagnostic identifying the file and line.

#### Scenario: Set event applied to scalar property
- **WHEN** a change file contains `{"kind":"set","at":"2026-04-30T10:00:00Z","by":"alice","issueId":"abc123","property":"title","value":"New title"}`
- **AND** replay processes that event
- **THEN** the in-memory issue `abc123` has `title` equal to `"New title"`

#### Scenario: Add event appends to an array property idempotently
- **WHEN** a change file contains an `add` event for `tags` with value `"foo"` followed by another `add` event for `tags` with value `"foo"`
- **AND** replay processes both events
- **THEN** the in-memory issue's `tags` array contains `"foo"` exactly once

#### Scenario: Remove event matches parentIssues by ID only
- **WHEN** a change file contains `{"kind":"add","property":"parentIssues","value":{"parentIssue":"P1","lexOrder":"aaa","active":true}}` followed by `{"kind":"remove","property":"parentIssues","value":{"parentIssue":"P1"}}`
- **AND** replay processes both events
- **THEN** the in-memory issue's `parentIssues` array does not contain any entry with `parentIssue` equal to `"P1"`

#### Scenario: Unknown event kind fails replay with diagnostic
- **WHEN** a change file contains an event with `"kind":"reticulate-splines"`
- **AND** any read operation triggers replay
- **THEN** replay fails with an error message identifying the file path and the line number of the unknown event

#### Scenario: Meta event with array follows is parsed as multi-parent
- **WHEN** a change file's first line is `{"kind":"meta","follows":["g1","g2"]}`
- **AND** replay reads the meta event
- **THEN** the file is treated as a node with two parents (`g1` and `g2`) in the DAG
- **AND** the file's events apply only after both `g1`'s events and `g2`'s events have been applied

#### Scenario: Meta event with scalar follows is parsed as single-parent
- **WHEN** a change file's first line is `{"kind":"meta","follows":"g1"}`
- **AND** replay reads the meta event
- **THEN** the file is treated as a node with one parent (`g1`) in the DAG

#### Scenario: Meta event serialiser emits scalar for single-parent follows
- **WHEN** a meta event with follows = `["g1"]` is serialised
- **THEN** the JSON output is `{"kind":"meta","follows":"g1"}` (scalar form)
- **AND** when follows = `[]` the output is `{"kind":"meta","follows":null}`
- **AND** when follows = `["g1","g2"]` the output is `{"kind":"meta","follows":["g1","g2"]}`

### Requirement: All read paths SHALL replay snapshot plus change files in DAG order

Every fleece read (`list`, `show`, `next`, `tree`, `search`, `validate`, `diff`, `status`, etc.) SHALL produce its result from an in-memory state computed by:

1. Loading `.fleece/issues.jsonl` into a dictionary keyed by issue ID.
2. Listing all `.fleece/changes/change_*.jsonl` files.
3. Reading each file's first-line meta event to build the follows-DAG. Each file's `follows` value yields zero, one, or more parent edges; multi-parent files contribute one edge per listed parent.
4. Topologically sorting the DAG. A file is "ready" once all its parents have been emitted.
5. When the topological sort is ambiguous (multiple ready files at the same step), tiebreaking by the first commit on the current branch's `git log` that introduced the file. When that is also ambiguous (e.g., post-squash with multiple files at the same commit), tiebreaking by GUID alphabetical order.
6. Within each file, applying events in line order.
7. The active uncommitted file is treated as a regular DAG node, sorted naturally by its `follows` pointer(s).
8. A `follows` entry that references a GUID not present locally (e.g., from a cherry-picked commit or a dropped rebase commit) SHALL be silently treated as if it were absent. A file all of whose `follows` entries are dangling SHALL become a DAG root for ordering purposes.

The result of replay SHALL be identical pre- and post-squash-merge, given the same set of events AND the same set of merge-marker files.

#### Scenario: Read on a feature branch reflects uncommitted edits
- **WHEN** a user runs `fleece edit abc123 --title "Updated"` on a feature branch (not yet committed)
- **AND** the user then runs `fleece show abc123`
- **THEN** the displayed title is `"Updated"`

#### Scenario: Replay produces same state pre- and post-squash
- **GIVEN** a feature branch with three commits, each creating one change file (`change_aaa.jsonl`, `change_bbb.jsonl` with `follows="aaa"`, `change_ccc.jsonl` with `follows="bbb"`), where each event sets the same property to a different value
- **WHEN** the branch is replayed pre-squash on the feature branch
- **AND** the branch is squash-merged to main and replayed post-squash
- **THEN** both replays produce the same final value for the property (the value set by the last event in the chain)

#### Scenario: Multi-parent marker linearizes squash of integration branch
- **GIVEN** main has change file `m0`
- **AND** `develop` regularly merges feature/a (introducing `a` with follows=`m0`) — the merge commit also contains marker `link_a` with follows=[`m0`,`a`]
- **AND** `develop` regularly merges feature/b (introducing `b` with follows=`m0`) — the merge commit also contains marker `link_b` with follows=[`link_a`,`b`]
- **WHEN** `develop` is squash-merged into main
- **AND** replay runs on main
- **THEN** the order is `m0` → `a` → `link_a` → `b` → `link_b`
- **AND** feature/b's events win over feature/a's on any conflicting property
- **AND** the outcome is identical to the pre-squash replay on develop

#### Scenario: Cherry-picked file with all-dangling follows is treated as root
- **WHEN** a change file's meta event has `follows="zzz"` but no `change_zzz.jsonl` exists locally
- **AND** the file is processed during replay
- **THEN** the file is treated as a DAG root (as if `follows=null`)
- **AND** ordering relative to other files is determined by commit position and GUID alphabetical tiebreaks

#### Scenario: Multi-parent file with one dangling follows uses the remaining parent
- **WHEN** a change file's meta event has `follows=["alive","dead"]`, where `alive` exists locally and `dead` does not
- **AND** the file is processed during replay
- **THEN** the file is treated as having one parent (`alive`)
- **AND** it becomes ready after `alive`'s events have been applied

## ADDED Requirements

### Requirement: Replay SHALL emit a warning when ordering falls through to GUID alphabetical between equally-ordinaled parallel files

When the topological sort encounters two simultaneously-ready files whose commit ordinals are equal AND non-null AND no later file in the DAG lists both as parents (i.e., they are unlinked parallel chains at the same commit), the replay engine SHALL emit a one-line warning to a configurable warning sink identifying both files and the shared commit ordinal. The warning SHALL NOT abort the replay; ordering proceeds via the GUID-alphabetical fallback.

The warning's purpose is to surface situations where a merge marker would have been expected but is absent (e.g., legacy history predating this change, hook bypass, or manual commits without `fleece link --merge`). Its presence indicates that ordering for these files is non-semantic.

The warning sink SHALL be injectable so test runs can capture warnings deterministically.

#### Scenario: Warning fires for parallel files at the same commit ordinal with no marker
- **GIVEN** two change files `change_aaa.jsonl` and `change_bbb.jsonl` both have `follows=null` and both were introduced by the same commit (e.g., a single squash commit)
- **AND** no later file in the DAG lists both as parents
- **WHEN** replay runs
- **THEN** a warning is emitted naming both files and the shared commit ordinal
- **AND** replay completes with the GUID-alphabetical order (`aaa` first, `bbb` second)

#### Scenario: Warning is suppressed when a marker links the parallel files
- **GIVEN** two change files `change_aaa.jsonl` and `change_bbb.jsonl` are parallel at the same commit ordinal
- **AND** a third file `change_link.jsonl` has `follows=["aaa","bbb"]`
- **WHEN** replay runs
- **THEN** no warning is emitted
- **AND** the order is deterministic via the DAG (aaa and bbb both before link)
