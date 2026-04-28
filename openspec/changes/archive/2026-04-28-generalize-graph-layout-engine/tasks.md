## 1. Public model surface

- [x] 1.1 Add `src/Fleece.Core/Models/Graph/IGraphNode.cs` with `IGraphNode` interface and `ChildSequencing` enum.
- [x] 1.2 Add `src/Fleece.Core/Models/Graph/LayoutMode.cs` with `IssueGraph` and `NormalTree` values.
- [x] 1.3 Add `src/Fleece.Core/Models/Graph/GridPosition.cs` (`readonly record struct` with `Row`, `Lane`).
- [x] 1.4 Add `src/Fleece.Core/Models/Graph/PositionedNode.cs` with `Node`, `Row`, `Lane`, `AppearanceIndex`, `TotalAppearances`.
- [x] 1.5 Add `src/Fleece.Core/Models/Graph/Edge.cs` with `Edge<TNode>`, `EdgeKind` enum.
- [x] 1.6 Add `src/Fleece.Core/Models/Graph/OccupancyCell.cs` with `OccupancyCell`, `EdgeOccupancy`, `EdgeSegmentKind` enum.
- [x] 1.7 Add `src/Fleece.Core/Models/Graph/GraphLayoutRequest.cs` with `GraphLayoutRequest<TNode>`.
- [x] 1.8 Add `src/Fleece.Core/Models/Graph/GraphLayout.cs` with `GraphLayout<TNode>`.
- [x] 1.9 Add `src/Fleece.Core/Models/Graph/GraphLayoutResult.cs` with `Success` / `CycleDetected` discriminated record.
- [x] 1.10 Add `src/Fleece.Core/Models/Graph/InvalidGraphException.cs` carrying the offending cycle.
- [x] 1.11 Make `Issue : IGraphNode`; expose `ChildSequencing` derived from `ExecutionMode`.

## 2. Layout engine

- [x] 2.1 Add `src/Fleece.Core/Services/Interfaces/IGraphLayoutService.cs` with `Layout<TNode>(GraphLayoutRequest<TNode>) → GraphLayoutResult<TNode>`.
- [x] 2.2 Add `src/Fleece.Core/Services/GraphLayout/GraphLayoutService.cs` orchestrating the engine.
- [x] 2.3 Add `src/Fleece.Core/Services/GraphLayout/LayoutContext.cs` carrying mutable per-call state (node list, path stack, appearance counts).
- [x] 2.4 Implement `LeafUpwardLaneStrategy` reproducing `Issues.LayoutSubtree` semantics for `LayoutMode.IssueGraph`:
  - Children-first DFS emission.
  - `parent.lane = max(child.lane) + 1`.
  - Series sibling lane advancement (subsequent child subtree starts at `currentLane + 1`).
  - Parallel sibling stacking (all children share `startLane`).
- [x] 2.5 Implement path-stack-based cycle detection. Maintain `pathStack : Stack<string>` separate from `appearanceCounts : Dictionary<string,int>`. On entering a node already on the stack, return `GraphLayoutResult.CycleDetected` with the cycle slice.
- [x] 2.6 Implement multi-parent appearance counting in a post-pass: assign `AppearanceIndex` and `TotalAppearances` after layout completes.
- [x] 2.7 Implement edge construction:
  - `SeriesSibling` edges between consecutive series children of the same parent.
  - `SeriesCornerToParent` edge from the chain's last max-lane node to the series parent.
  - `ParallelChildToSpine` edges from each parallel child to the parent (pivot at parent's lane).
  - First child of a series parent SHALL NOT produce an edge.
  - Edge IDs follow the pattern `"{from.Id}->{to.Id}:{kind}#{appearance}"` for stable lookups.
- [x] 2.8 Implement occupancy matrix builder: `OccupancyCell[,]` of size `[totalRows, totalLanes]`. For each edge, walk every cell along its path and append an `EdgeOccupancy` entry. For each positioned node, set `cell.Node`. A cell may carry both a node and edges.
- [x] 2.9 Implement `LayoutMode.NormalTree` branch that throws `NotImplementedException("LayoutMode.NormalTree is reserved; see openspec change add-normal-tree-layout-mode")`.

## 3. Issue layout adapter

- [x] 3.1 Add `src/Fleece.Core/Services/Interfaces/IIssueLayoutService.cs` with `LayoutForTree` and `LayoutForNext`.
- [x] 3.2 Add `src/Fleece.Core/Services/GraphLayout/IssueLayoutService.cs` that:
  - Composes `GraphLayoutRequest<Issue>` from issue inputs (filter pipeline mirrors `BuildTaskGraphLayout`).
  - Supplies a `RootFinder` matching the existing "no displayed parent" logic.
  - Supplies a `ChildIterator` that respects `SortOrder`, soft-deletion, and (for next mode) `HasActiveDescendants`.
  - Calls `IGraphLayoutService.Layout` with `Mode = LayoutMode.IssueGraph`.
  - Unwraps `Success` and throws `InvalidGraphException` on `CycleDetected`.
- [x] 3.3 Migrate ancestor-context collection (currently inside `BuildFilteredTaskGraphLayout`) into `LayoutForNext` when `matchedIds` is supplied.
- [x] 3.4 Register `IGraphLayoutService` and `IIssueLayoutService` in `Fleece.Core` DI composition (whatever module the CLI imports — same place `IFleeceService` is registered).

## 4. CLI migration

- [x] 4.1 Update `Fleece.Cli/Commands/ListCommand.cs` to resolve `IIssueLayoutService` and call `LayoutForTree` / `LayoutForNext` instead of `Issues.BuildTaskGraphLayout` / `BuildFilteredTaskGraphLayout`.
- [x] 4.2 Update `Fleece.Cli/Commands/NextCommand.cs` likewise where it builds graph-style output.
- [x] 4.3 Rewrite `Fleece.Cli/Output/TaskGraphRenderer.cs` to render from `GraphLayout<Issue>`:
  - Iterate `Occupancy[row, lane]` cells row-by-row.
  - Draw node glyphs from `cell.Node`.
  - Draw connector glyphs from `cell.Edges` (collapse collinear segments to one glyph).
  - Delete inline parent-relationship inference and `RenderingParentId` consumption.
- [x] 4.4 If `TreeRenderer.cs` references `TaskGraph` / `TaskGraphNode`, update or leave behind a TODO — it's not on the critical path for `list --tree` after this change.

## 5. Removals

- [x] 5.1 Delete `Issues.BuildTaskGraphLayout` and `Issues.BuildFilteredTaskGraphLayout`.
- [x] 5.2 Delete `Fleece.Core/Models/TaskGraph.cs` (records `TaskGraph`, `TaskGraphNode`).
- [x] 5.3 Remove any remaining references to `RenderingParentId`.
- [x] 5.4 Remove now-dead helpers used only by the removed methods (`LayoutSubtree`, `LayoutSeriesChildren`, `LayoutParallelChildren`, `AssignAppearanceCounts`, `GetIncompleteChildrenForLayout` if unused after migration).

## 6. Tests

- [x] 6.1 Add `tests/Fleece.Core.Tests/Services/GraphLayout/GraphLayoutServiceTests.cs`:
  - Generic test node type implementing `IGraphNode` for testing engine in isolation from `Issue`.
  - Series sibling lane advancement.
  - Parallel sibling stacking.
  - Mixed series/parallel subtrees.
  - Multi-parent fan-in produces duplicate leaves with correct `AppearanceIndex` / `TotalAppearances`.
  - Cycle (3-node loop) returns `CycleDetected` with the cycle path.
  - Self-loop returns `CycleDetected`.
  - `LayoutMode.NormalTree` throws `NotImplementedException`.
- [x] 6.2 Add `tests/Fleece.Core.Tests/Services/GraphLayout/EdgeConstructionTests.cs`:
  - Series chain with N children produces N-1 `SeriesSibling` edges plus one `SeriesCornerToParent` edge.
  - Parallel parent with N children produces N `ParallelChildToSpine` edges all sharing pivot lane.
  - First child of series parent has no incoming edge.
  - Edge IDs are unique and stable across appearances.
- [x] 6.3 Add `tests/Fleece.Core.Tests/Services/GraphLayout/OccupancyMatrixTests.cs`:
  - Single edge populates every cell along its path.
  - Two parallel edges sharing a vertical spine produce cells with multiple `EdgeOccupancy` entries.
  - Node and edge can co-exist in a cell.
  - Matrix dimensions equal `(totalRows, totalLanes)`.
- [x] 6.4 Add `tests/Fleece.Core.Tests/Services/GraphLayout/IssueLayoutServiceTests.cs`:
  - `LayoutForTree` with assorted issue fixtures matches the layout produced by the existing `BuildTaskGraphLayout` (snapshot test, removed once the static method is deleted).
  - `LayoutForNext` with `matchedIds` includes ancestor context.
  - Cycle in issue graph surfaces as `InvalidGraphException`.
- [x] 6.5 Migrate `IssuesTests.BuildTaskGraphLayout_*` tests into `IssueLayoutServiceTests`. Delete the originals.
- [x] 6.6 Verify the `tests/Fleece.Cli.E2E.Tests` snapshots for `list --tree` and `list --next` remain green after the renderer rewrite. Treat any `*.received.txt` diff as a regression to fix in the engine.

## 7. Verification

- [x] 7.1 `dotnet build` clean.
- [x] 7.2 `dotnet test` green for every project (Core, Cli, E2E, Integration).
- [x] 7.3 `grep -rn "TaskGraph\b\|RenderingParentId\|BuildTaskGraphLayout" src/` returns no hits outside removed-code comments.
- [x] 7.4 Smoke: `dotnet run -- list --tree` and `dotnet run -- list --next` render the expected output against this repo's `.fleece/` issues.
- [x] 7.5 Run `openspec validate generalize-graph-layout-engine --strict` and address any findings.
