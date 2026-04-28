## ADDED Requirements

### Requirement: LayoutMode.NormalTree SHALL produce parent-first row order with depth-from-root lane assignment

When `Mode = LayoutMode.NormalTree`, the engine SHALL:

- Emit each node in **pre-order DFS**: a parent's row index is lower than every descendant's row index.
- Assign **`child.lane = parent.lane + 1`** for every parent-child edge in the layout.
- Roots SHALL be placed at lane 0 (or the caller's `startLane` if exposed; default 0).
- Children of a single parent SHALL share the same lane (`parent.lane + 1`) regardless of `ChildSequencing`. Lateral subtree separation SHALL be handled by row sequencing (subtrees are emitted contiguously) rather than by lane offset.

`LayoutMode.NormalTree` SHALL no longer throw `NotImplementedException`.

#### Scenario: Series children in NormalTree share the same lane
- **GIVEN** parent `P` with `ChildSequencing.Series` and three leaf children `A`, `B`, `C` (in iterator order)
- **WHEN** the engine lays out the graph with `Mode = LayoutMode.NormalTree` starting at lane 0
- **THEN** `P` is at `(row 0, lane 0)`
- **AND** `A`, `B`, `C` are at `(row 1, lane 1)`, `(row 2, lane 1)`, `(row 3, lane 1)` respectively
- **AND** no series-related lane advancement occurs

#### Scenario: Parallel children in NormalTree share the same lane
- **GIVEN** parent `P` with `ChildSequencing.Parallel` and three leaf children `X`, `Y`, `Z`
- **WHEN** the engine lays out the graph with `Mode = LayoutMode.NormalTree`
- **THEN** `X`, `Y`, `Z` are at `(row 1, lane 1)`, `(row 2, lane 1)`, `(row 3, lane 1)`
- **AND** behaviour is identical to the series case at the lane level

#### Scenario: Subtrees emit contiguously in row order
- **GIVEN** parent `P` with two children `A` and `B`, where `A` has its own children `X`, `Y`
- **WHEN** the engine lays out the graph with `Mode = LayoutMode.NormalTree`
- **THEN** rows are assigned in order `P, A, X, Y, B`
- **AND** `B`'s row is strictly greater than every row in `A`'s subtree

#### Scenario: NormalTree no longer throws
- **WHEN** any caller invokes `Layout` with `GraphLayoutRequest { Mode = LayoutMode.NormalTree }`
- **AND** the input graph is acyclic
- **THEN** the engine returns `GraphLayoutResult.Success` with a populated `GraphLayout<TNode>`
- **AND** the previous `NotImplementedException` from `generalize-graph-layout-engine` is gone

### Requirement: NormalTree edge construction SHALL mirror IssueGraph over the row axis

The engine SHALL emit the same edge kinds (`SeriesSibling`, `SeriesCornerToParent`, `ParallelChildToSpine`) for `NormalTree`, with mirrored geometry:

- **`SeriesSibling`**: vertical edge connecting consecutive series children of the same parent. `Start = (row_prev, lane)`, `End = (row_next, lane)`, `PivotLane = null`. Identical structural shape to `IssueGraph`. Note that intermediate rows between series siblings may belong to the previous sibling's subtree; the vertical edge passes through those cells.
- **`SeriesCornerToParent`**: L-shape from parent to the **first** series child (the chain end nearest the parent). `Start = (parent.row, parent.lane)`, `End = (first.row, first.lane)`, `PivotLane = parent.lane`. Path: vertical down through the parent's lane to the first child's row, then horizontal right to the first child's lane.
- **`ParallelChildToSpine`**: L-shape from parent down through the spine then left into each parallel child. `Start = (parent.row, parent.lane)`, `End = (child.row, child.lane)`, `PivotLane = parent.lane`. Path: vertical down to the child's row, then horizontal right to the child's lane.

The first child of a `Series` parent SHALL still NOT produce a `SeriesSibling` edge (it is the chain start). It receives the `SeriesCornerToParent` edge instead.

#### Scenario: Series chain in NormalTree produces sibling edges plus a corner from parent to first child
- **GIVEN** parent `P (row 0, lane 0)` in `Series` mode with children `A (row 1, lane 1)`, `B (row 2, lane 1)`, `C (row 3, lane 1)`
- **WHEN** the engine emits edges in `NormalTree` mode
- **THEN** edges include:
  - `SeriesCornerToParent { From=P, To=A, Start=(0,0), End=(1,1), Pivot=0 }`
  - `SeriesSibling { From=A, To=B, Start=(1,1), End=(2,1), Pivot=null }`
  - `SeriesSibling { From=B, To=C, Start=(2,1), End=(3,1), Pivot=null }`
- **AND** there is no edge from `P` to `B` or `C` (their connection to `P` is via the chain)

#### Scenario: Parallel children in NormalTree share a downward spine from the parent
- **GIVEN** parent `P (row 0, lane 0)` in `Parallel` mode with children `X (row 1, lane 1)`, `Y (row 2, lane 1)`, `Z (row 3, lane 1)`
- **WHEN** the engine emits edges in `NormalTree` mode
- **THEN** three `ParallelChildToSpine` edges are produced:
  - `{ From=P, To=X, Start=(0,0), End=(1,1), Pivot=0 }`
  - `{ From=P, To=Y, Start=(0,0), End=(2,1), Pivot=0 }`
  - `{ From=P, To=Z, Start=(0,0), End=(3,1), Pivot=0 }`
- **AND** the occupancy cells in lane 0 between rows 0 and 3 list every spine edge passing through

#### Scenario: SeriesSibling edge passes through subtree rows
- **GIVEN** parent `P (row 0, lane 0)` in `Series` mode with first child `A (row 1, lane 1)` having its own child `X (row 2, lane 2)`, and second child `B (row 3, lane 1)`
- **WHEN** the engine emits the `SeriesSibling { From=A, To=B }` edge
- **THEN** the edge's path covers cells `[1, 1]`, `[2, 1]`, `[3, 1]`
- **AND** cell `[2, 1]` (which is in row 2, lane 1) appears in `OccupancyCell.Edges` with `Segment = Vertical` even though row 2's node is `X` at lane 2

### Requirement: Edge<TNode> SHALL expose SourceAttach and TargetAttach indicating visual sides

Every `Edge<TNode>` SHALL carry `SourceAttach: EdgeAttachSide` and `TargetAttach: EdgeAttachSide` properties so renderers can draw without re-deriving sides from row/lane arithmetic.

```csharp
public enum EdgeAttachSide { Top, Bottom, Left, Right }
```

For each `(Mode, EdgeKind)` the engine SHALL populate the sides as follows:

- `(IssueGraph, SeriesSibling)`: source = Bottom, target = Top.
- `(IssueGraph, SeriesCornerToParent)`: source = Bottom, target = Left.
- `(IssueGraph, ParallelChildToSpine)`: source = Right, target = Top.
- `(NormalTree, SeriesSibling)`: source = Bottom, target = Top.
- `(NormalTree, SeriesCornerToParent)`: source = Bottom, target = Left.
- `(NormalTree, ParallelChildToSpine)`: source = Bottom, target = Left.

#### Scenario: Renderer reads attach side directly
- **WHEN** a renderer draws an edge in `NormalTree` mode with `Kind = ParallelChildToSpine`
- **THEN** it reads `edge.TargetAttach == EdgeAttachSide.Left` and draws the connector entering the child node from its left side
- **AND** it does not need to compare `Start.Row` to `End.Row` to figure out direction

#### Scenario: IssueGraph parallel attach is unchanged
- **WHEN** the engine emits a `ParallelChildToSpine` edge in `IssueGraph` mode
- **THEN** `edge.TargetAttach == EdgeAttachSide.Top`
- **AND** the visual rendering of `list --tree` and `list --next` is identical to before

### Requirement: NormalTree SHALL pass the same shared-engine guarantees as IssueGraph

`NormalTree` SHALL exhibit the same engine-level behaviours as `IssueGraph`:

- **Cycle detection**: a back-edge to a node currently on the DFS path SHALL return `GraphLayoutResult.CycleDetected`.
- **Multi-parent fan-in**: a node reachable from multiple ancestors SHALL appear once per ancestor with `AppearanceIndex` and `TotalAppearances` populated.
- **Occupancy matrix**: every cell along every edge's path SHALL contain an `EdgeOccupancy` entry; cells under shared spines SHALL list multiple entries.

#### Scenario: Cycle detection works in NormalTree
- **GIVEN** an acyclic-violating graph where `A → B → C → A` and `Mode = LayoutMode.NormalTree`
- **WHEN** `Layout` runs
- **THEN** the result is `GraphLayoutResult.CycleDetected` with `Cycle = ["A", "B", "C", "A"]` (or rotation)
- **AND** no partial layout is returned

#### Scenario: Multi-parent in NormalTree produces duplicate appearances
- **GIVEN** node `Shared` is a child of both `A` and `B` (no cycle), with `Mode = LayoutMode.NormalTree`
- **WHEN** `Layout` runs
- **THEN** `Shared` appears twice in `GraphLayout.Nodes`, once under `A`'s subtree and once under `B`'s subtree
- **AND** each appearance has the correct `AppearanceIndex` (1 and 2) and `TotalAppearances` (2)

#### Scenario: Occupancy under a parallel spine in NormalTree lists every edge
- **GIVEN** parent `P` in `Parallel` mode with three children producing three `ParallelChildToSpine` edges sharing pivot lane `parent.lane`
- **WHEN** the engine builds the occupancy matrix
- **THEN** spine cells in `parent.lane` between `parent.row` and the deepest child's row each list multiple `EdgeOccupancy` entries with `Segment = Vertical`
