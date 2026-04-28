## 1. Public API extension

- [ ] 1.1 Add `EdgeAttachSide` enum (`Top`, `Bottom`, `Left`, `Right`) in `src/Fleece.Core/Models/Graph/`.
- [ ] 1.2 Add `SourceAttach` and `TargetAttach` properties to `Edge<TNode>`. Make them `required`.
- [ ] 1.3 Update existing `IssueGraph` edge construction (from `generalize-graph-layout-engine`) to populate the new attach properties:
  - `SeriesSibling`: source = Bottom, target = Top
  - `SeriesCornerToParent`: source = Bottom, target = Left
  - `ParallelChildToSpine`: source = Right, target = Top
- [ ] 1.4 Re-record any `IssueGraph` edge construction tests so they assert on the new attach values without changing existing semantics.

## 2. NormalTree lane strategy

- [ ] 2.1 Add `src/Fleece.Core/Services/GraphLayout/RootDownwardLaneStrategy.cs` (or equivalent inline branch in `GraphLayoutService` keyed on `LayoutMode`).
- [ ] 2.2 Implement pre-order DFS:
  - Place each root at `(row = nextRow++, lane = 0)`.
  - For each child in iterator order: place at `(row = nextRow++, lane = parent.lane + 1)`, then recurse.
- [ ] 2.3 Confirm subtrees emit contiguously (the recursion order alone guarantees this; assert via test).
- [ ] 2.4 Replace the `NotImplementedException` branch in `GraphLayoutService` with a dispatch to the NormalTree strategy.

## 3. NormalTree edge construction

- [ ] 3.1 Series children of the same parent under `NormalTree`:
  - For consecutive series children `i`, `i+1`: emit `SeriesSibling { Start=(row_i, lane), End=(row_{i+1}, lane), Pivot=null, SourceAttach=Bottom, TargetAttach=Top }`.
  - For the **first** series child only: emit `SeriesCornerToParent { From=parent, To=firstChild, Start=(parent.row, parent.lane), End=(first.row, first.lane), Pivot=parent.lane, SourceAttach=Bottom, TargetAttach=Left }`.
  - Subsequent series children do NOT receive a corner edge.
- [ ] 3.2 Parallel children under `NormalTree`:
  - For each parallel child: emit `ParallelChildToSpine { From=parent, To=child, Start=(parent.row, parent.lane), End=(child.row, child.lane), Pivot=parent.lane, SourceAttach=Bottom, TargetAttach=Left }`.
- [ ] 3.3 Edge IDs follow the same pattern as `IssueGraph` (`"{from.Id}->{to.Id}:{kind}#{appearance}"`).

## 4. Occupancy matrix for NormalTree

- [ ] 4.1 Confirm the occupancy matrix builder (shared with `IssueGraph`) correctly walks `NormalTree` edge paths. The builder is mode-agnostic; it walks `Start → Pivot → End`. No mode-specific code expected.
- [ ] 4.2 Add a test that a `SeriesSibling` edge in `NormalTree` whose path passes through a previous sibling's subtree rows correctly populates the intervening cells with `Vertical` segments.

## 5. Test-only ASCII renderer

- [ ] 5.1 Add `tests/Fleece.Core.Tests/Services/GraphLayout/Helpers/NormalTreeAsciiRenderer.cs`. Renders a `GraphLayout<TNode>` to a string using simple Unicode box-drawing glyphs:
  - Node glyph: `○` (open), `●` (closed) — caller-supplied marker function.
  - Vertical: `│`. Horizontal: `─`. Corners as appropriate (`┌`, `┐`, `└`, `┘`).
  - Junctions where multiple edges meet.
- [ ] 5.2 Renderer iterates the occupancy matrix row-by-row, lane-by-lane, choosing glyphs from `cell.Edges` and `cell.Node`.
- [ ] 5.3 Renderer collapses collinear edges to a single glyph (matches `IssueGraph` renderer behaviour).

## 6. Tests

- [ ] 6.1 Add `tests/Fleece.Core.Tests/Services/GraphLayout/NormalTreeLayoutTests.cs`:
  - Pre-order row assignment for a single-root, multi-level tree.
  - `child.lane = parent.lane + 1` for every parent-child pair.
  - Series children share parent.lane + 1 (no advancement).
  - Parallel children share parent.lane + 1.
  - Subtrees emit contiguously.
  - Multi-root layouts: each root starts at its own row, all at lane 0.
- [ ] 6.2 Add `tests/Fleece.Core.Tests/Services/GraphLayout/NormalTreeEdgeTests.cs`:
  - Series chain produces exactly one `SeriesCornerToParent` (parent → first child) and (n-1) `SeriesSibling` edges for n series children.
  - Parallel parent produces n `ParallelChildToSpine` edges with `Pivot = parent.lane`.
  - All `NormalTree` edges have `SourceAttach` and `TargetAttach` matching the spec table.
- [ ] 6.3 Add `tests/Fleece.Core.Tests/Services/GraphLayout/NormalTreeOccupancyTests.cs`:
  - Spine cells under parallel parent list every edge.
  - Series sibling edge passes through previous-subtree rows correctly.
  - Cell at parent's position has `Node = parent` and `Edges` lists outgoing edges.
- [ ] 6.4 Add cycle and multi-parent tests scoped to `NormalTree` mode (mirror the `IssueGraph` tests from `generalize-graph-layout-engine`):
  - 3-node cycle returns `CycleDetected`.
  - Self-loop returns `CycleDetected`.
  - Multi-parent fan-in produces duplicate appearances with correct indices.
- [ ] 6.5 Add Verify snapshot tests using `NormalTreeAsciiRenderer`:
  - `NormalTreeRendering_SeriesChain.verified.txt`
  - `NormalTreeRendering_ParallelFan.verified.txt`
  - `NormalTreeRendering_MixedDepth.verified.txt`
  - `NormalTreeRendering_MultiParent.verified.txt`
- [ ] 6.6 Update `tests/Fleece.Core.Tests/Services/GraphLayout/GraphLayoutServiceTests.cs` to remove the assertion that `LayoutMode.NormalTree` throws, since it now succeeds.

## 7. Verification

- [ ] 7.1 `dotnet build` clean.
- [ ] 7.2 `dotnet test` green (Core, Cli, E2E, Integration). The CLI E2E suite SHALL be unaffected since no command uses `NormalTree`.
- [ ] 7.3 Confirm `IssueGraph` Verify snapshots still pass byte-for-byte (no regression from the `EdgeAttachSide` addition).
- [ ] 7.4 Run `openspec validate add-normal-tree-layout-mode --strict` and address findings.
