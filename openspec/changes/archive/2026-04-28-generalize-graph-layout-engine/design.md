## Context

`Fleece.Core.FunctionalCore.Issues` currently exposes two layout entry points (`BuildTaskGraphLayout`, `BuildFilteredTaskGraphLayout`) that consume `Issue` directly and emit `TaskGraph { Nodes: TaskGraphNode[], TotalLanes }`. Each `TaskGraphNode` carries `(Row, Lane, IsActionable, ParentExecutionMode, RenderingParentId, AppearanceIndex, TotalAppearances)` — enough to render circles in the right cells, but not enough to draw the connectors. Consumers reverse-engineer the connectors from the node positions and parent-relationship metadata:

- `Fleece.Cli/Output/TaskGraphRenderer` walks `(Row, Lane)` and infers connector kind row-by-row.
- Homespun's `task-graph-layout.ts` (~1,150 lines) does the same in TypeScript: groups connected components, recomputes parents, derives `drawTopLine` / `drawBottomLine` / `seriesConnectorFromLane`, builds parallel-spine reservations, assigns multi-parent indices, handles hidden-parent cases.

Both renderers re-derive knowledge the layout already had during traversal. That's the wasted work this change removes.

The new design is a pure-functional **graph layout engine** keyed on a small `IGraphNode` interface, with delegate-driven topology and a strategy-driven lane assignment per `LayoutMode`. It emits a complete layout: positioned nodes, semantic edges with absolute endpoints and pivot lanes, and a per-cell occupancy matrix where each cell lists every edge that touches it. Renderers iterate cells.

## Goals / Non-Goals

**Goals:**
- Layout engine is generic over `TNode : IGraphNode` so external consumers (e.g. sub-tasks of issues) can plug in their own node types.
- Tree mode and Next mode share **one engine** — they differ only in the `ChildIterator` delegate's filtering.
- Edges are first-class output: source/target nodes, semantic kind, absolute `(row, lane)` endpoints, pivot lane.
- Occupancy matrix exposes every edge passing through every cell so renderers handle overlaps without losing information.
- Cycle detection is built in and surfaced via a `Result` discriminated type — `fleece verify` consumes it directly.
- `LayoutMode` is exposed in the public API now, with `IssueGraph` implemented and `NormalTree` deferred.
- Visible behaviour of `list --tree`, `list --next`, and the `next` graph output is unchanged. Verify snapshots are the regression net.

**Non-Goals:**
- Implementing `LayoutMode.NormalTree`. That belongs to the follow-up change `add-normal-tree-layout-mode`.
- Changing graph semantics (multi-parent flattening via duplication, actionable detection, terminal-with-active-descendants visibility).
- New CLI flags, new colours, new markup.
- Generic enough to handle non-tree graph layouts (force-directed, dot/Graphviz). The engine targets DAGs with a clear hierarchy.

## Decisions

### `IGraphNode` is intentionally small and Fleece-leaky

```csharp
namespace Fleece.Core.Models.Graph;

public interface IGraphNode
{
    string Id { get; }
    ChildSequencing ChildSequencing { get; }
}

public enum ChildSequencing { Series, Parallel }
```

`ChildSequencing` belongs on the interface. The alternative — putting it on the iterator's return type — saves no code (the iterator would just read the same property) and forces every consumer to encode it twice. We accept the abstraction leak because Series/Parallel sibling layout is the entire reason this engine is more complex than depth-from-root tree drawing.

`Issue : IGraphNode` is a one-line addition: `ChildSequencing => ExecutionMode == ExecutionMode.Series ? ChildSequencing.Series : ChildSequencing.Parallel`.

### Two delegates carry topology; `LayoutMode` carries strategy

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

The `RootFinder` and `ChildIterator` capture *what* the graph looks like (filtering, ordering, multi-parent fan-in). `LayoutMode` selects *how* lanes/rows are assigned. This split is what lets `LayoutForTree` and `LayoutForNext` share the engine — they pass different child iterators (full vs incomplete-only) but the same mode.

Setting `Mode = LayoutMode.NormalTree` in this change throws `NotImplementedException` from the engine. The enum value is reserved so callers can prepare; the implementation lands in the follow-up.

### Lane assignment strategy: `LeafUpwardLaneStrategy`

Internal interface:

```csharp
internal interface ILaneStrategy
{
    LayoutMode Mode { get; }
    void Layout<TNode>(LayoutContext<TNode> ctx) where TNode : IGraphNode;
}
```

`LeafUpwardLaneStrategy` reproduces the existing Fleece algorithm exactly:

1. DFS each root subtree.
2. For each node: compute children (via `ChildIterator`), recursively layout, then place this node at `max(child.lane) + 1`.
3. **Series sibling lane advancement**: under a `Series` parent, the *n*-th child's subtree starts at `currentLane + 1` (where `currentLane` is the previous child's max lane). The first child starts at `startLane`.
4. **Parallel sibling stacking**: under a `Parallel` parent, every child subtree starts at `startLane`. Max lane is the max across children.
5. Children are emitted in row order *before* the parent (children-first traversal).

This matches the current algorithm bit-for-bit, which is what keeps Verify snapshots green.

### Cycle detection: path-tracking, not visited-tracking

The current algorithm uses a single `visited` set and emits duplicate leaf nodes when a node is revisited. That handles multi-parent flattening but cannot distinguish a legitimate multi-parent from a cycle. The new engine tracks two structures:

- `pathStack` — the current DFS recursion path. Re-entry to a node already on the stack is a **cycle**, not a multi-parent.
- `appearanceCounts` — how many times a node has been emitted. Increments on every emission, including re-entry from a different parent (legitimate multi-parent).

When `pathStack` contains the candidate node, the engine returns `GraphLayoutResult.CycleDetected(cycle)` where `cycle` is the slice of `pathStack` from the prior occurrence to the candidate. No partial layout is returned.

### Edge model

```csharp
public sealed record Edge<TNode> where TNode : IGraphNode
{
    public required string Id { get; init; }                    // stable key for occupancy lookups
    public required TNode From { get; init; }                   // logical source (parent or prior sibling)
    public required TNode To { get; init; }                     // logical target
    public required EdgeKind Kind { get; init; }
    public required GridPosition Start { get; init; }           // (row, lane) of source endpoint
    public required GridPosition End { get; init; }             // (row, lane) of target endpoint
    public int? PivotLane { get; init; }                        // for L-shaped paths; null for pure verticals
}

public enum EdgeKind
{
    SeriesSibling,           // vertical: previous series sibling → next series sibling
    SeriesCornerToParent,    // L-shape: last series sibling → parent (vertical-then-horizontal)
    ParallelChildToSpine     // L-shape: parallel child → parent (horizontal-then-vertical)
}

public readonly record struct GridPosition(int Row, int Lane);
```

The renderer derives line characters from `Start`, `End`, and `PivotLane`. `Kind` is metadata for callers that want to colour, label, or selectively hide edge classes.

The engine emits exactly these three kinds in `IssueGraph` mode:

- For each pair of consecutive series children under the same `Series` parent → one `SeriesSibling` edge.
- For each `Series` parent → one `SeriesCornerToParent` edge from the last series-child subtree's max-lane node down-then-right to the parent.
- For each child of a `Parallel` parent → one `ParallelChildToSpine` edge from the child rightward to the parent's lane, then down to the parent.

The first child of a `Series` parent does **not** generate an edge. Its connection to the parent is implicit through the chain that ends at the corner. Renderers that want a visible parent-to-first-child stub can synthesize it from positions; the engine intentionally does not because the existing renderer doesn't draw one and Verify snapshots would fail.

### Occupancy matrix

```csharp
public sealed record GraphLayout<TNode> where TNode : IGraphNode
{
    public required IReadOnlyList<PositionedNode<TNode>> Nodes { get; init; }
    public required IReadOnlyList<Edge<TNode>> Edges { get; init; }
    public required OccupancyCell[,] Occupancy { get; init; }   // [Row, Lane]
    public required int TotalRows { get; init; }
    public required int TotalLanes { get; init; }
}

public sealed record OccupancyCell
{
    public PositionedNode<IGraphNode>? Node { get; init; }
    public required IReadOnlyList<EdgeOccupancy> Edges { get; init; }
}

public sealed record EdgeOccupancy
{
    public required string EdgeId { get; init; }
    public required EdgeSegmentKind Segment { get; init; }
}

public enum EdgeSegmentKind
{
    Vertical, Horizontal,
    CornerNE, CornerNW, CornerSE, CornerSW,
    JunctionT_East, JunctionT_West, JunctionT_North, JunctionT_South
}
```

The matrix is computed deterministically after edges are known. For each edge, the engine walks every cell along its path and appends an `EdgeOccupancy` entry. **A cell may contain a node and edges simultaneously** (e.g. a node sits at the parallel spine pivot). **A cell may contain multiple edges** (parallel siblings sharing a vertical spine). The renderer chooses how to combine — typically collapsing collinear segments to a single line glyph.

`OccupancyCell.Node` is typed as `PositionedNode<IGraphNode>?` (covariant) for ergonomics; tests downcast safely via the original `Nodes` list.

### Adapter: `IIssueLayoutService`

The Fleece-specific adapter composes delegates and routes into the engine:

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

Both methods build `GraphLayoutRequest<Issue>` with `Mode = LayoutMode.IssueGraph` and call `IGraphLayoutService.Layout`. The differences:

- **`LayoutForTree`**: child iterator returns *all* children of an issue (subject to filters). Root finder returns issues with no displayed parent. Mirrors current `BuildTaskGraphLayout`.
- **`LayoutForNext`**: child iterator returns only children with active descendants (the existing `HasActiveDescendants` predicate). Root finder still returns no-displayed-parent issues. Mirrors current `BuildFilteredTaskGraphLayout` ancestor-context behaviour when `matchedIds` is supplied.

Both methods unwrap `GraphLayoutResult.Success` directly. A `CycleDetected` result throws an `InvalidGraphException` carrying the cycle — the existing CLI commands assume a clean DAG, and `fleece verify` is the proper place to surface cycles structurally.

### What about appearance counts and rendering parent IDs?

The current `TaskGraphNode` carries `RenderingParentId` to handle series cascades — when a series sibling subtree's first leaf needs to "connect" to a node that isn't its real parent. This was a renderer-driven hack to avoid recomputing connection points.

In the new design, `RenderingParentId` is **gone**. Edges are explicit; the engine emits the right `SeriesSibling` and `SeriesCornerToParent` edges with correct endpoints, and the renderer draws them directly. The cascade is a property of edge construction, not a per-node hint.

`AppearanceIndex` and `TotalAppearances` move to `PositionedNode` and are computed by the same post-pass as today.

### CLI migration

- `Fleece.Cli/Commands/ListCommand.cs` and `NextCommand.cs` swap `Issues.BuildTaskGraphLayout` / `BuildFilteredTaskGraphLayout` calls for `IIssueLayoutService.LayoutForTree` / `LayoutForNext`, resolved via DI.
- `Fleece.Cli/Output/TaskGraphRenderer.cs` is rewritten to consume `GraphLayout.Edges` and `GraphLayout.Occupancy`. The new render loop iterates rows; for each row it iterates lanes; for each cell it asks `Occupancy[row, lane]` what to draw. The current edge-inference helpers are deleted.
- `TreeRenderer.cs` (legacy hierarchical view) is left untouched in this change — it operates on a different data shape and is not on the critical path. It can be migrated or removed in a later cleanup.
- DI registration: `IGraphLayoutService` and `IIssueLayoutService` registered in `Fleece.Core` composition (whatever module the CLI imports — same place as `IFleeceService`).

### Cycle handling contract

`IGraphLayoutService.Layout` returns `GraphLayoutResult<TNode>`:

```csharp
public abstract record GraphLayoutResult<TNode> where TNode : IGraphNode
{
    public sealed record Success(GraphLayout<TNode> Layout) : GraphLayoutResult<TNode>;
    public sealed record CycleDetected(IReadOnlyList<string> Cycle) : GraphLayoutResult<TNode>;
}
```

`IIssueLayoutService` adapters unwrap `Success` and throw `InvalidGraphException` on `CycleDetected`. A separate change can wire `fleece verify` to call the engine directly and report cycles without throwing — out of scope here.

## Risks / Trade-offs

- **Public API churn.** `TaskGraph` / `TaskGraphNode` are removed. Any external Core consumer (currently only Homespun) needs a one-time migration. This is intentional — the new shape is the whole point. We do **not** add a compatibility shim.
- **Verify snapshots are the regression net.** The new engine must reproduce the existing layout cell-for-cell. The CLI E2E suite's `*.verified.txt` files for `list --tree` and `list --next` are the contract. Any drift is a behaviour bug, not an output update.
- **Multi-parent vs cycle distinction is subtle.** Path-stack tracking is the right answer, but the test surface needs to cover: (a) legitimate multi-parent (node appears under two parents, no back-edge to ancestor) — produces duplicate leaves; (b) true cycle (back-edge to ancestor) — returns `CycleDetected`; (c) self-loop — returns `CycleDetected` with single-element cycle.
- **`LayoutMode.NormalTree` placeholder.** Throwing `NotImplementedException` from a public API value is an explicit signal, not a bug. The follow-up change implements it. Tests assert the throw to lock the contract.
- **Engine genericity vs concrete `Issue`.** `IGraphLayoutService` is generic; `IIssueLayoutService` is the Fleece-specific concrete adapter. CLI consumers only see `IIssueLayoutService`. The genericity is for external library consumers and the future sub-task case.
- **Edge IDs must be stable within a layout call.** We generate them as `$"{From.Id}->{To.Id}:{Kind}"` with appearance-aware suffixes when needed (multi-parent duplicates). Stable identifiers let occupancy lookups join back to the edge list.
- **Performance.** Building the occupancy matrix is O(rows × lanes + Σ edge path lengths). For typical Fleece graphs (≤200 issues, ≤8 lanes) this is negligible. Worth re-checking if external consumers feed in 10k-node graphs, but premature to optimize now.
- **Renderer rewrite scope.** `TaskGraphRenderer` is touched substantially. Any rendering edge case currently handled by ad-hoc logic must be expressible via edge kinds + occupancy. Unknown unknowns surface only via Verify snapshots — budget for a few rounds of snapshot diff review.
