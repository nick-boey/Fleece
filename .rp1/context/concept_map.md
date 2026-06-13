# Domain Concepts & Terminology

**Project**: Fleece
**Domain**: Local-first git-native issue tracking

## Core Business Concepts

### Issue
**Definition**: Central unit of work. Lean projected record (no per-property audit fields) persisted in `.fleece/issues.jsonl`. Implements `IGraphNode` for layout engine consumption.
**Implementation**: [`src/Fleece.Core/Models/Issue.cs`], [`src/Fleece.Core/Schemas/issue.schema.json`]
**Key Properties**:
- `Id`: 6-char Base62 GUID-derived identifier, globally unique within the repo
- `Status`: Workflow lifecycle position (see IssueStatus)
- `Type`: Work category (see IssueType)
- `ExecutionMode`: Series or Parallel — controls how children are ordered
- `ParentIssues`: List of `ParentIssueRef` with LexoRank sort orders
- `Tags`: Plain string array; structured metadata encoded as `key=value` keyed tags

**Business Rules**:
- Terminal statuses (Complete, Archived, Closed, Deleted) are excluded from listings by default
- Done statuses (Complete, Archived, Closed) satisfy dependency checks
- Idea type issues are never actionable

### IssueStatus
**Definition**: Workflow lifecycle enum.
**Values**: `Draft → Open → Progress → Review → Complete` (or `Archived`, `Closed`, `Deleted`)
**Implementation**: [`src/Fleece.Core/Models/IssueStatus.cs`]

### IssueType
**Definition**: Work category enum — `Task`, `Bug`, `Chore`, `Feature`, `Idea`, `Verify`
**Implementation**: [`src/Fleece.Core/Models/IssueType.cs`]

### ExecutionMode
**Definition**: Controls child ordering — `Series` (sequential, each blocked until previous done) or `Parallel` (all independently actionable).
**Implementation**: [`src/Fleece.Core/Models/Issue.cs`]

### ParentIssueRef
**Definition**: Reference from child issue to parent, carrying a LexoRank sort order and active flag. Soft-deleted parent links preserved as inactive.
**Implementation**: [`src/Fleece.Core/Models/ParentIssueRef.cs`]

### Tombstone
**Definition**: Immutable record of a hard-deleted issue. Stores `IssueId`, `OriginalTitle`, `CleanedAt`, `CleanedBy`. Persisted in `.fleece/tombstones.jsonl`. Prevents ID reuse.
**Implementation**: [`src/Fleece.Core/Models/Tombstone.cs`]

### KeyedTag
**Definition**: Structured metadata in the plain `Tags` array as `key=value` strings. Enables extensible metadata without schema changes. `hsp-linked-pr` is the canonical keyed tag for PR linkage.
**Implementation**: [`src/Fleece.Core/Models/KeyedTag.cs`]

### IssueGraph
**Definition**: In-memory computed graph of all issues with parent-child, previous-next (series siblings), and root relationships. Used for dependency resolution, next-actionable detection, and tree rendering.
**Implementation**: [`src/Fleece.Core/Models/IssueGraph.cs`]

## Technical Concepts

### Event Sourcing Storage Model
**Purpose**: Immutable audit trail; eliminates merge conflicts by appending events rather than mutating state.
**Implementation**: [`src/Fleece.Core/EventSourcing/`]

Key structures:
- **Snapshot** (`.fleece/issues.jsonl`): Projected state from last `fleece project` run
- **Change File** (`.fleece/changes/change_{guid}.jsonl`): Append-only JSONL per session/commit
- **MetaEvent**: First line of every change file; carries the `follows` DAG pointer(s)
- **Follows DAG**: Directed acyclic graph connecting change files for topological replay ordering
- **Merge Marker**: Change file with array `follows` — written by `fleece link --merge` on merge commits
- **Replay Cache** (`.fleece/.replay-cache`): Gitignored cache keyed by HEAD SHA to skip re-replay
- **Active Change Pointer** (`.fleece/.active-change`): Gitignored; names current session's change file GUID

**Commit-Scoped Immutability**: A change file is immutable once committed. The next write rotates to a fresh GUID whose `follows` chains back. Enforced via `IEventGitContext.IsFileCommittedAtHead`.

### IssueEvent
**Purpose**: Discriminated-union base for all events in change files.
**Implementation**: [`src/Fleece.Core/EventSourcing/Events/IssueEvent.cs`]
**Subtypes**: `MetaEvent`, `CreateEvent`, `SetEvent`, `AddEvent`, `RemoveEvent`, `HardDeleteEvent`

### FleeceService
**Purpose**: Unified facade — primary public API for all issue operations. Thread-safe via `SemaphoreSlim`.
**Implementation**: [`src/Fleece.Core/Services/FleeceService.cs`], [`src/Fleece.Core/Services/Interfaces/IFleeceService.cs`]

### FunctionalCore
**Purpose**: Static pure-function classes (`Issues`, `Dependencies`, `Cleaning`, `Validation`, `Tags`, etc.) with no I/O. `FleeceService` loads state then delegates to these. Enables testability.
**Implementation**: [`src/Fleece.Core/FunctionalCore/`]

### GraphLayout
**Purpose**: Generic output of the graph layout engine: positioned nodes, semantic edges, OccupancyMatrix. Parameterized on `IGraphNode`. Powers `list --tree` and `list --next` rendering.
**Implementation**: [`src/Fleece.Core/Models/Graph/GraphLayout.cs`]

## Terminology Glossary

### Business Terms
- **LexoRank** (aliases: `lexOrder`, `SortOrder`): Lexicographic rank strings for sibling ordering. O(1) move operations; normalization triggered when string length exceeds bounds.
- **Terminal Status**: Complete, Archived, Closed, Deleted — excluded from listings by default; use `--all` to include.
- **Done Status**: Complete, Archived, Closed — considered resolved for dependency checking (`AllPreviousDone` gate).
- **Actionable Issue**: Status Open or Review, not type Idea, no incomplete children, all series-previous siblings Done.
- **Soft Delete**: Setting status to Deleted via `SetEvent`. Issue persists until hard-deleted or 30-day auto-cleanup.
- **Hard Delete**: Permanent removal via `HardDeleteEvent`; creates a Tombstone.
- **Auto-cleanup**: Automatic hard-deletion of Deleted issues during `fleece project` when `LastUpdate` > 30 days old.

### Technical Terms
- **Partial ID**: 3+ character prefix of an issue ID used for resolution. Ambiguous matches list all candidates.
- **Rotation**: Creating a new change file GUID when the active file is missing or already committed at HEAD.
- **hsp-linked-pr**: Keyed tag format `hsp-linked-pr=<PR number>` for PR association.
- **fleece project**: Compaction command — replays all change files into the snapshot, auto-cleans stale soft-deletes, empties `.fleece/changes/`. Restricted to the default branch. Replaces deprecated `fleece merge`.
- **fleece install**: Bootstrap command installing git hooks (`pre-commit`, `pre-merge-commit`), GitHub Action, and `.gitignore` entries.
- **fleece link --merge**: Writes a merge-marker change file. Invoked from git hooks on merge commits.
- **Commit-Order Tiebreak**: Replay ordering tiebreak — earliest commit ordinal wins; falls back to GUID-alphabetical.
- **Warning Sink** (`IWarningSink`): Receives replay warnings when ordering falls through to GUID-alphabetical between parallel files.
- **NullEventGitContext**: Git context for use outside a git repo — `IsFileCommittedAtHead` always false, disabling rotation.
- **OccupancyMatrix**: 2D array [row, lane] in `GraphLayout` recording positioned nodes and edges per cell.

## Cross-References
- **System topology**: See [architecture.md](architecture.md)
- **Module breakdown**: See [modules.md](modules.md)
- **Implementation patterns**: See [patterns.md](patterns.md)
- **CLI interaction**: See [interaction-model.md](interaction-model.md)
