## Why

The current task-graph layout lives inside `Fleece.Core.FunctionalCore.Issues` and is tightly coupled to `Issue`. It produces `(Row, Lane)` coordinates but stops short of emitting **edges** and a **per-cell occupancy matrix**. Downstream consumers — most notably the Homespun web UI (`task-graph-layout.ts`, ~1,150 lines) — re-derive edge geometry, hidden-parent connectors, lane reservations, and multi-parent indices on top of the partial output. The two CLI renderers (`TaskGraphRenderer`, `TreeRenderer`) carry their own duplicated edge logic.

The redundancy is not incidental: the C# layout discards the edge knowledge it has during traversal, then every renderer reconstructs it. That re-derivation is fragile, costly to maintain, and impossible for external consumers to reuse without copying the algorithm wholesale.

This change extracts a **generic graph layout engine** keyed on a small `IGraphNode` interface. The engine produces a complete layout — positioned nodes, semantic edges, and an occupancy matrix where each cell lists every edge that touches it. Renderers become trivial consumers. External users can adapt their own node types (e.g. sub-tasks of issues) by implementing `IGraphNode` and supplying delegates that find roots and iterate children.

The engine ships with one mode in this change (`IssueGraph` — leaves-first, current Fleece semantics). A second mode (`NormalTree`) is scaffolded as an enum value but deliberately deferred to the follow-up change `add-normal-tree-layout-mode`.

## What Changes

- **New types in `Fleece.Core.Models`:**
  - `IGraphNode` interface (`Id`, `ChildSequencing`)
  - `ChildSequencing` enum (`Series`, `Parallel`)
  - `LayoutMode` enum (`IssueGraph`, `NormalTree`) — `NormalTree` throws `NotImplementedException` until the follow-up change
  - `GraphLayoutRequest<TNode>` (where `TNode : IGraphNode`)
  - `GraphLayout<TNode>`: positioned nodes, edges, occupancy matrix, totals
  - `PositionedNode<TNode>`: `(Node, Row, Lane, AppearanceIndex, TotalAppearances)`
  - `Edge<TNode>`: `(From, To, Kind, StartPosition, EndPosition, PivotLane)`
  - `EdgeKind` enum: `SeriesSibling`, `SeriesCornerToParent`, `ParallelChildToSpine`
  - `OccupancyCell`: `(Node?, IReadOnlyList<EdgeOccupancy>)`
  - `EdgeOccupancy`: `(EdgeId, EdgeSegmentKind)`
  - `GraphLayoutResult<TNode>`: discriminated `Success` / `CycleDetected(IReadOnlyList<string> cycle)`

- **New service in `Fleece.Core.Services`:**
  - `IGraphLayoutService` with `Layout<TNode>(GraphLayoutRequest<TNode>) → GraphLayoutResult<TNode>`
  - `GraphLayoutService` implementation: DFS, path-tracking cycle detection, multi-parent appearance counting, leaf-upward lane assignment, edge construction, occupancy matrix
  - `IIssueLayoutService` adapter with `LayoutForTree` / `LayoutForNext` (filtered/searched variants), composing Fleece-specific root finder + child iterator delegates that route into `IGraphLayoutService` with `LayoutMode.IssueGraph`

- **`Issue` implements `IGraphNode`** (via existing `Id` and a derived `ChildSequencing` from `ExecutionMode`).

- **CLI migration:** `list --tree`, `list --next`, and the `next` graph-style outputs route through `IIssueLayoutService` instead of `BuildTaskGraphLayout` / `BuildFilteredTaskGraphLayout`. `TaskGraphRenderer` is rewritten to consume `GraphLayout.Edges` and `GraphLayout.Occupancy` instead of inferring them from positions.

- **Removed:** `BuildTaskGraphLayout`, `BuildFilteredTaskGraphLayout`, the `TaskGraph`/`TaskGraphNode` records, and the inline edge derivation in `TaskGraphRenderer`.

- **Cycle detection becomes a first-class capability.** `fleece verify` (existing or new) can call `IGraphLayoutService.Layout` and report `CycleDetected.Cycle` directly.

## Capabilities

### New Capabilities
- `graph-layout`: defines the generic graph layout engine — `IGraphNode` contract, lane/row assignment for `LayoutMode.IssueGraph`, edge emission with semantic kinds, occupancy matrix, cycle detection — and the `IIssueLayoutService` adapter that powers Fleece's task-graph CLI rendering.

### Modified Capabilities
<!-- none -->

## Impact

- **Code:** new files under `src/Fleece.Core/Models/Graph/` (interfaces, records) and `src/Fleece.Core/Services/GraphLayout/` (engine + adapter). `Issues.cs` loses `BuildTaskGraphLayout` / `BuildFilteredTaskGraphLayout` (≈200 lines). `TaskGraphRenderer` shrinks substantially (current edge-inference logic is replaced by a simple cell walk).
- **Public API:** `TaskGraph` / `TaskGraphNode` removed; `GraphLayout<Issue>` / `PositionedNode<Issue>` are the new shapes. Any external Core consumer (Homespun) needs a one-time migration to the new types — but in return they gain edges + occupancy and can delete their re-derivation layer.
- **Tests:** `IssuesTests.BuildTaskGraphLayout_*` migrate to `GraphLayoutServiceTests` plus `IssueLayoutServiceTests`. New tests cover edge emission per `EdgeKind`, occupancy cell merging when edges overlap, and cycle detection.
- **DI:** `IGraphLayoutService` and `IIssueLayoutService` registered in `Fleece.Core` composition; CLI commands resolve `IIssueLayoutService` instead of calling static `Issues.BuildTaskGraphLayout`.
- **Behaviour:** zero visible change to `list --tree`, `list --next`, or `next`. Existing Verify snapshots are the regression net — any deviation surfaces immediately.
