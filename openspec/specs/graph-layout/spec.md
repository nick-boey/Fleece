# graph-layout

## Purpose

Defines the generic, node-agnostic graph layout engine (`IGraphLayoutService`) and the Fleece-specific adapter (`IIssueLayoutService`) that powers `list --tree` and `list --next` rendering. The engine is pure, DI-registered, and reusable for any `IGraphNode` implementation outside of Fleece.

## Requirements

### Requirement: Fleece.Core SHALL provide a generic graph layout engine keyed on IGraphNode

`Fleece.Core` SHALL expose `IGraphLayoutService` with a single method `Layout<TNode>(GraphLayoutRequest<TNode>) → GraphLayoutResult<TNode>` where `TNode : IGraphNode`. The engine SHALL be pure (no I/O, no statics) and reusable for any node type that implements `IGraphNode`.

`IGraphNode` SHALL be defined as:

```csharp
public interface IGraphNode
{
    string Id { get; }
    ChildSequencing ChildSequencing { get; }
}

public enum ChildSequencing { Series, Parallel }
```

`Issue` SHALL implement `IGraphNode`, mapping `Issue.ExecutionMode` to `ChildSequencing`.

#### Scenario: Layout method consumes any IGraphNode implementation
- **WHEN** an external caller defines `class MyNode : IGraphNode { ... }` and supplies a `GraphLayoutRequest<MyNode>` to `IGraphLayoutService.Layout`
- **THEN** the engine returns `GraphLayoutResult<MyNode>` with positioned nodes, edges, and occupancy matrix populated
- **AND** the engine never references `Fleece.Core.Models.Issue` in its execution path

#### Scenario: Issue implements IGraphNode
- **WHEN** an `Issue` instance with `ExecutionMode.Series` is queried via `((IGraphNode)issue).ChildSequencing`
- **THEN** the property returns `ChildSequencing.Series`
- **AND** an `Issue` with `ExecutionMode.Parallel` returns `ChildSequencing.Parallel`

### Requirement: GraphLayoutRequest SHALL accept root and child delegates plus a LayoutMode

The engine SHALL drive topology via two delegates (`RootFinder`, `ChildIterator`) and select algorithm strategy via a `LayoutMode` enum. The same delegates SHALL serve both tree-mode and next-mode rendering by varying the child iterator's filter.

```csharp
public sealed record GraphLayoutRequest<TNode> where TNode : IGraphNode
{
    public required IReadOnlyList<TNode> AllNodes { get; init; }
    public required Func<IReadOnlyList<TNode>, IEnumerable<TNode>> RootFinder { get; init; }
    public required Func<TNode, IEnumerable<TNode>> ChildIterator { get; init; }
    public LayoutMode Mode { get; init; } = LayoutMode.IssueGraph;
}

public enum LayoutMode { IssueGraph, NormalTree }
```

#### Scenario: Caller supplies root finder and child iterator
- **WHEN** a caller supplies `RootFinder = nodes => nodes.Where(n => n.ParentIssues.Count == 0)` and `ChildIterator = parent => allChildrenOf(parent)`
- **THEN** the engine begins DFS from each node returned by `RootFinder`
- **AND** descends recursively via `ChildIterator(currentNode)` for each visited node

#### Scenario: Tree mode and next mode share the engine via different child iterators
- **WHEN** `LayoutForTree` calls `Layout` with a child iterator that returns all children
- **AND** `LayoutForNext` calls `Layout` with a child iterator that returns only children with active descendants
- **THEN** both produce a `GraphLayout<Issue>` of the appropriate scope
- **AND** the engine code path is identical apart from the iterator delegate

### Requirement: LayoutMode.IssueGraph SHALL produce leaves-first row order with leaf-upward lane assignment

When `Mode = LayoutMode.IssueGraph`, the engine SHALL emit nodes in row order such that every child appears before its parent, assign lanes leaf-upward (`parent.lane = max(child.lane) + 1`), and apply Series/Parallel sibling layout rules:

- **Series parent**: the *n*-th child's subtree starts at `currentLane + 1` where `currentLane` is the previous child's max lane; the first child starts at `startLane`. The parent is then placed at `currentLane + 1` after the last child.
- **Parallel parent**: every child's subtree starts at `startLane`; the parent is placed at `max(child max lanes) + 1`.

Output positions SHALL match the layout produced by the prior `Issues.BuildTaskGraphLayout` for equivalent inputs.

#### Scenario: Series children advance lanes
- **GIVEN** parent `P` in `Series` mode with leaf children `A`, `B`, `C` in that order
- **WHEN** the engine lays out `P`'s subtree starting at lane 0
- **THEN** `A` is at lane 0, `B` is at lane 1, `C` is at lane 2, `P` is at lane 3
- **AND** rows are 0 (A), 1 (B), 2 (C), 3 (P)

#### Scenario: Parallel children stack at the same lane
- **GIVEN** parent `P` in `Parallel` mode with leaf children `X`, `Y`, `Z` in that order
- **WHEN** the engine lays out `P`'s subtree starting at lane 0
- **THEN** `X`, `Y`, `Z` are all at lane 0
- **AND** `P` is at lane 1
- **AND** rows are 0 (X), 1 (Y), 2 (Z), 3 (P)

#### Scenario: Existing Verify snapshots remain green
- **WHEN** `list --tree` or `list --next` runs against the test fixture issues
- **THEN** the rendered output matches the existing `*.verified.txt` snapshot byte-for-byte
- **AND** any deviation is a regression to be fixed in the engine, not an accepted snapshot update

### Requirement: LayoutMode.NormalTree SHALL be reserved but not implemented in this change

The `LayoutMode.NormalTree` enum value SHALL be defined in the public API. Calling `Layout` with `Mode = LayoutMode.NormalTree` SHALL throw `NotImplementedException` until the follow-up change `add-normal-tree-layout-mode` lands.

#### Scenario: NormalTree throws until the follow-up change
- **WHEN** a caller invokes `Layout` with `GraphLayoutRequest { Mode = LayoutMode.NormalTree }`
- **THEN** the engine throws `NotImplementedException`
- **AND** the exception message identifies `LayoutMode.NormalTree` as the unimplemented case
- **AND** a unit test asserts this throw to lock the contract

### Requirement: The engine SHALL emit semantic edges with absolute endpoints

Every parent-child relationship rendered by the layout SHALL produce zero or more `Edge<TNode>` records with semantic `EdgeKind`, absolute `Start` and `End` positions, and a `PivotLane` for L-shaped paths. Edge kinds SHALL be:

```csharp
public enum EdgeKind
{
    SeriesSibling,           // vertical: previous series sibling → next series sibling
    SeriesCornerToParent,    // L-shape: last series sibling → parent
    ParallelChildToSpine     // L-shape: parallel child → parent
}
```

For `LayoutMode.IssueGraph`:

- Each pair of consecutive series children under the same `Series` parent SHALL produce one `SeriesSibling` edge with `Start = (row_prev, lane_prev)`, `End = (row_next, lane_next)`, `PivotLane = null`.
- Each `Series` parent SHALL produce one `SeriesCornerToParent` edge from the last series-child max-lane node to the parent, with `PivotLane` equal to the max lane reached by the chain.
- Each child of a `Parallel` parent SHALL produce one `ParallelChildToSpine` edge from the child to the parent, with `PivotLane = parent.Lane`.
- The first child of a `Series` parent SHALL NOT produce an edge — its connection to the parent is implicit through the chain.

#### Scenario: Series parent with three children produces two sibling edges and one corner edge
- **GIVEN** parent `P` in `Series` mode with leaf children `A` (row 0, lane 0), `B` (row 1, lane 1), `C` (row 2, lane 2), `P` (row 3, lane 3)
- **WHEN** the engine emits edges
- **THEN** edges include:
  - `SeriesSibling { From=A, To=B, Start=(0,0), End=(1,1), Pivot=null }`
  - `SeriesSibling { From=B, To=C, Start=(1,1), End=(2,2), Pivot=null }`
  - `SeriesCornerToParent { From=C, To=P, Start=(2,2), End=(3,3), Pivot=2 }`
- **AND** there is no edge with `From=P, To=A` (implicit first-child connection)

#### Scenario: Parallel parent with three children produces three spine edges
- **GIVEN** parent `P` in `Parallel` mode with leaf children `X` (row 0, lane 0), `Y` (row 1, lane 0), `Z` (row 2, lane 0), `P` (row 3, lane 1)
- **WHEN** the engine emits edges
- **THEN** edges include three `ParallelChildToSpine` edges:
  - `{ From=X, To=P, Start=(0,0), End=(3,1), Pivot=1 }`
  - `{ From=Y, To=P, Start=(1,0), End=(3,1), Pivot=1 }`
  - `{ From=Z, To=P, Start=(2,0), End=(3,1), Pivot=1 }`

### Requirement: The engine SHALL produce a per-cell occupancy matrix listing every edge that touches each cell

`GraphLayout<TNode>` SHALL include a 2D `OccupancyCell[,]` indexed `[row, lane]`. Each cell SHALL contain:

- An optional reference to the `PositionedNode` that sits at this cell (or `null`).
- A list of `EdgeOccupancy` entries — one per edge whose path crosses this cell. Each entry carries the edge's `Id` and an `EdgeSegmentKind` describing how the edge intersects the cell (vertical pass-through, horizontal pass-through, corner, or junction).

When multiple edges occupy the same cell (e.g. parallel siblings sharing a vertical spine), all SHALL appear in the cell's `Edges` list. Renderers SHALL be free to combine collinear edges into a single visual line; the engine SHALL preserve the source data.

#### Scenario: Parallel spine cell lists every edge passing through
- **GIVEN** parent `P` in `Parallel` mode with three children `X`, `Y`, `Z` producing three `ParallelChildToSpine` edges all pivoting at lane 1
- **WHEN** the engine builds the occupancy matrix
- **THEN** cell `[row=1, lane=1]` (between `X`'s spine entry and `P`'s row) contains **two** `EdgeOccupancy` entries with `Segment = Vertical`: one for edge `X→P` and one for edge `Y→P`
- **AND** cell `[row=2, lane=1]` contains **three** entries, one per child edge

#### Scenario: A node and its incoming edge segment can co-exist in a cell
- **WHEN** the parent node sits at the foot of a parallel spine
- **THEN** the parent's cell `Occupancy[parent.row, parent.lane]` has `Node = positionedParent` AND `Edges` lists every spine edge ending at this cell
- **AND** the renderer can use both signals to draw the node glyph plus the incoming connector

### Requirement: The engine SHALL detect cycles via path-tracking and return GraphLayoutResult.CycleDetected

The engine SHALL distinguish legitimate multi-parent fan-in (a node reachable from multiple distinct ancestors) from a true cycle (a back-edge to a node currently on the DFS path). Multi-parent fan-in SHALL produce duplicate `PositionedNode` emissions with incrementing `AppearanceIndex`. A true cycle SHALL halt layout and return `GraphLayoutResult.CycleDetected(cycle)` where `cycle` is the ordered list of node ids forming the cycle (from the prior occurrence on the path stack to the cycle-closing re-entry, inclusive).

#### Scenario: Multi-parent node produces duplicate leaves, not a cycle
- **GIVEN** node `C` is a child of both `A` and `B`, neither of which is reachable from the other
- **WHEN** the engine lays out the graph
- **THEN** `C` appears twice in `GraphLayout.Nodes` with `AppearanceIndex = 1, 2` and `TotalAppearances = 2`
- **AND** the result is `GraphLayoutResult.Success`

#### Scenario: Back-edge to ancestor is reported as a cycle
- **GIVEN** node `A`'s child is `B`, `B`'s child is `C`, and `C`'s child is `A` (cycle: A → B → C → A)
- **WHEN** the engine attempts to lay out the graph
- **THEN** the engine returns `GraphLayoutResult.CycleDetected`
- **AND** `Cycle` contains `["A", "B", "C", "A"]` or an equivalent rotation
- **AND** no partial `GraphLayout` is returned

#### Scenario: Self-loop is reported as a cycle
- **GIVEN** node `A` lists itself as its own child
- **WHEN** the engine attempts to lay out the graph
- **THEN** the engine returns `GraphLayoutResult.CycleDetected`
- **AND** `Cycle` contains `["A", "A"]`

### Requirement: PositionedNode SHALL track multi-parent appearance counts

Every `PositionedNode<TNode>` SHALL carry `AppearanceIndex` (1-based, the n-th time this node is emitted) and `TotalAppearances` (the total count of emissions of this node within the layout). For nodes that appear once (the common case), both fields SHALL be 1. The values SHALL be assigned in a single post-pass after layout completes.

#### Scenario: Single-parent node has appearance index 1 of 1
- **GIVEN** node `N` has exactly one parent in the layout
- **WHEN** the engine returns a layout
- **THEN** `N`'s `PositionedNode` has `AppearanceIndex = 1` and `TotalAppearances = 1`

#### Scenario: Multi-parent node has separate index per appearance
- **GIVEN** node `N` is a child of both `A` and `B`
- **WHEN** the engine returns a layout
- **THEN** `N` appears twice in `Nodes`; first appearance has `AppearanceIndex = 1, TotalAppearances = 2`; second appearance has `AppearanceIndex = 2, TotalAppearances = 2`

### Requirement: Fleece.Core SHALL provide IIssueLayoutService as the Fleece-specific adapter

`Fleece.Core` SHALL expose `IIssueLayoutService` with `LayoutForTree` and `LayoutForNext` methods that wrap `IGraphLayoutService` and supply Fleece-specific delegates: a root finder over `Issue.ActiveParentIssues` and a child iterator that respects sort order, soft-deletion, and (for next mode) the existing `HasActiveDescendants` predicate.

The adapter SHALL unwrap `GraphLayoutResult.Success` and SHALL throw `InvalidGraphException` (carrying the cycle) when the engine returns `CycleDetected`, since CLI rendering paths assume a clean DAG.

```csharp
public interface IIssueLayoutService
{
    GraphLayout<Issue> LayoutForTree(
        IReadOnlyList<Issue> issues,
        InactiveVisibility visibility = InactiveVisibility.Hide,
        string? assignedTo = null,
        GraphSortConfig? sort = null);

    GraphLayout<Issue> LayoutForNext(
        IReadOnlyList<Issue> issues,
        IReadOnlySet<string>? matchedIds = null,
        InactiveVisibility visibility = InactiveVisibility.Hide,
        string? assignedTo = null,
        GraphSortConfig? sort = null);
}
```

#### Scenario: LayoutForTree replaces BuildTaskGraphLayout for list --tree
- **WHEN** `list --tree` runs and the CLI resolves `IIssueLayoutService` from DI and calls `LayoutForTree(allIssues, visibility, assignedTo, sort)`
- **THEN** the returned `GraphLayout<Issue>` produces the same `(Row, Lane)` positions as the prior `Issues.BuildTaskGraphLayout` for the same inputs
- **AND** the prior `BuildTaskGraphLayout` and `BuildFilteredTaskGraphLayout` static methods are removed from `Issues.cs`

#### Scenario: LayoutForNext powers list --next with optional matched-id context
- **WHEN** `list --next` runs with a search query that resolves to a set of matched issue ids
- **AND** the CLI calls `LayoutForNext(allIssues, matchedIds: searchMatches, ...)`
- **THEN** the returned layout includes the matched issues plus their ancestors for context
- **AND** the layout's positions match the prior `BuildFilteredTaskGraphLayout` output for the same inputs

#### Scenario: Cycle in issue graph throws InvalidGraphException
- **WHEN** the issue graph contains a parent cycle (e.g. issue `A` has parent `B`, `B` has parent `A`)
- **AND** `LayoutForTree` is called with that issue set
- **THEN** the method throws `InvalidGraphException` whose message lists the cycle's issue ids
- **AND** no `GraphLayout` is returned

### Requirement: TaskGraphRenderer SHALL render exclusively from GraphLayout edges and occupancy

`Fleece.Cli/Output/TaskGraphRenderer` SHALL consume `GraphLayout<Issue>` and render by walking the occupancy matrix, drawing nodes from `OccupancyCell.Node` and connectors from `OccupancyCell.Edges`. Inline reconstruction of edge geometry from positions SHALL be removed.

#### Scenario: Renderer draws a connector from edge occupancy alone
- **WHEN** the renderer encounters cell `[row, lane]` with `Edges = [{ Vertical }]` and `Node = null`
- **THEN** it emits a vertical line glyph in that cell
- **AND** it does not consult the original `Edge` list, parent IDs, or sibling positions

#### Scenario: Renderer collapses multiple collinear edges into one glyph
- **WHEN** a cell has multiple `EdgeOccupancy` entries all with `Segment = Vertical`
- **THEN** the renderer emits a single vertical line glyph
- **AND** the cell's edge list is preserved in the layout for callers that need to differentiate (e.g. colouring per edge kind in a richer renderer)

### Requirement: Removed APIs SHALL no longer exist

After this change, the following SHALL be removed from `Fleece.Core`:

- `Issues.BuildTaskGraphLayout`
- `Issues.BuildFilteredTaskGraphLayout`
- `TaskGraph` (record)
- `TaskGraphNode` (record)
- The `RenderingParentId` concept (replaced by explicit edge construction)

External consumers of these types must migrate to `GraphLayout<Issue>` and the `IIssueLayoutService` interface. No compatibility shim is provided.

#### Scenario: Old types are absent from the public API
- **WHEN** a consumer references `Fleece.Core.Models.TaskGraph` or `TaskGraphNode`
- **THEN** the build fails with a missing-type error
- **AND** the migration path is documented in the change's `proposal.md` and `design.md`

#### Scenario: BuildTaskGraphLayout is gone from Issues.cs
- **WHEN** a test or caller invokes `Issues.BuildTaskGraphLayout`
- **THEN** the call fails to compile
- **AND** the equivalent functionality is reachable via `IIssueLayoutService.LayoutForTree`
