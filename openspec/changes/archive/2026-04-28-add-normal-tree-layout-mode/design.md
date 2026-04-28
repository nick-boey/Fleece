## Context

`generalize-graph-layout-engine` defines `LayoutMode { IssueGraph, NormalTree }` and implements `IssueGraph` (leaves-first row order, leaf-upward lane assignment, series-sibling lane advancement, parallel-into-children-from-the-RIGHT). The `NormalTree` branch throws `NotImplementedException`.

`NormalTree` is the natural shape for a top-down tree:

```
PARENT (row 0, lane 0)
│
├── A (row 1, lane 1, first series child enters from LEFT via corner)
│   │
│   ├── X (row 2, lane 2)  ← X is series child of A
│   │
│   └── Y (row 3, lane 2)  ← Y is series sibling of X (vertical chain)
│
├── B (row 4, lane 1)  ← B is series sibling of A (vertical chain in lane 1)
│
└── C (row 5, lane 1)  ← C is series sibling of B
```

For a `Parallel` parent, all children sit at `parent.lane + 1` and connect via L-shaped edges from the parent down through the spine then left into each child:

```
PARENT (row 0, lane 0)
│
├── X (row 1, lane 1, enters from LEFT)
├── Y (row 2, lane 1, enters from LEFT)
└── Z (row 3, lane 1, enters from LEFT)
```

The user's invariant — "parallel = horizontal into child, vertical out of parent" — holds. What changes is direction: vertical is now **down** out of the parent (parent above) and horizontal goes **left** into the child (children to the right of the spine). In `IssueGraph` the same invariant produces vertical-up and horizontal-right because the parent is below.

## Goals / Non-Goals

**Goals:**
- `IGraphLayoutService.Layout` with `Mode = LayoutMode.NormalTree` returns `GraphLayoutResult.Success` for any acyclic graph.
- `NormalTree` layout produces parent-before-children row order, `child.lane = parent.lane + 1` lane assignment, and mirror-correct edges.
- The same engine code path handles both modes — the strategy abstraction does the per-mode work; everything else (delegates, cycle detection, occupancy matrix, appearance counting) is shared.
- A test-only ASCII renderer demonstrates `NormalTree` is *visually* sensible and anchors Verify snapshots so future engine changes don't silently corrupt the layout.

**Non-Goals:**
- Switching `list --tree` or `list --next` to `NormalTree`. The Fleece issue graph is leaf-first by design; that's not what this change is about.
- A production CLI renderer for `NormalTree`. None of Fleece's commands need it. External consumers and future Homespun work can consume `GraphLayout<TNode>` directly.
- Customising attach sides per consumer. Attach sides are derived from `LayoutMode` + `EdgeKind`. Renderers either honour them or ignore them; they don't override.
- Mixing modes within a single layout call (e.g. some subtrees `IssueGraph`, others `NormalTree`). One mode per call.

## Decisions

### Lane assignment: depth-from-root, sibling lanes shared

```
RootDownwardLaneStrategy:
  For each root r in RootFinder(allNodes):
    placeNode(r, row = nextRow++, lane = 0)
    layoutChildren(r, parentLane = 0)

  layoutChildren(parent, parentLane):
    For each child c in ChildIterator(parent), in iterator order:
      placeNode(c, row = nextRow++, lane = parentLane + 1)
      layoutChildren(c, parentLane = parentLane + 1)
```

Critical contrast with `IssueGraph`: `NormalTree` does **not** advance lanes per series child. All siblings under the same parent share `parent.lane + 1`. They are separated only by row, and their subtrees are emitted contiguously (full DFS) so subtrees naturally don't overlap in rows.

This is simpler than `IssueGraph` precisely because the leaves-first model has to spread series subtrees laterally to avoid collision; the root-down model spreads them in rows for free.

### Row order: pre-order DFS

The parent is emitted before any of its descendants. Children are emitted in iterator order. Subtrees are contiguous: if `A` has children `X`, `Y` and `B` follows `A` as a sibling, all of `A`'s subtree (including `Y`'s entire subtree) emits before `B`.

```
P
├─ A
│   ├─ X
│   └─ Y
└─ B    ← B is row N+1 where N is the last row of Y's subtree
```

### Edges in NormalTree mode

**`SeriesSibling`** — unchanged in semantics. Connects consecutive series children of the same parent. Vertical chain from `(row_prev, lane)` to `(row_next, lane)`. The chain runs top-to-bottom in both modes; the difference is only that in `NormalTree`, both endpoints have higher row numbers than the parent.

But wait — in `NormalTree`, between consecutive series siblings under the same parent, **other subtrees may sit between them in row order** (the previous sibling's subtree). The `SeriesSibling` edge endpoints are the *sibling nodes themselves*, but the visual vertical between them spans the previous sibling's entire subtree depth.

```
P (row 0, lane 0)
├─ A (row 1, lane 1)
│   └─ X (row 2, lane 2)
└─ B (row 3, lane 1)  ← series sibling of A; vertical edge in lane 1 from row 1 to row 3
```

The occupancy matrix for cell `[row=2, lane=1]` will list this `SeriesSibling` edge as `Vertical`, even though row 2 belongs to `X`'s subtree. That's correct — the renderer draws a vertical line passing through.

**`SeriesCornerToParent`** — semantically "the parent-adjacent end of the chain connects to the parent". In `IssueGraph`, that's the **last** sibling (chain ends at the bottom, near the parent which is below). In `NormalTree`, that's the **first** sibling (chain starts at the top, near the parent which is above).

Engine implementation: emit `SeriesCornerToParent` with `From = chain-end-nearest-parent`. Mode chooses which end. Path is L-shaped:

- `IssueGraph`: from `(last.row, last.lane)` down to `(parent.row, last.lane)` then right to `(parent.row, parent.lane)`. Pivot = last.lane.
- `NormalTree`: from `(parent.row, parent.lane)` down to `(first.row, parent.lane)` then right to `(first.row, first.lane)`. Pivot = parent.lane.

Both produce one edge with `EdgeKind.SeriesCornerToParent`. The difference is just the endpoints; the renderer reads `Start`, `End`, `PivotLane` and draws.

**`ParallelChildToSpine`** — unchanged kind, mirrored geometry. In `NormalTree`, the spine runs **down** from the parent through the parallel children's lane. Each child's edge:

- `Start = (parent.row, parent.lane)` (or, equivalently, `(child.row, parent.lane)` — endpoint logic below).
- `End = (child.row, child.lane)`.
- `PivotLane = parent.lane`.

Strictly: the L-shape vertical segment is from `(parent.row, parent.lane)` down to `(child.row, parent.lane)`, then horizontal left to `(child.row, child.lane)`. Multiple parallel children share the vertical spine — exactly like `IssueGraph`, just mirrored. The occupancy matrix lists every edge passing through the spine cells.

### `EdgeAttachSide` — make visual side explicit

```csharp
public enum EdgeAttachSide { Top, Bottom, Left, Right }

public sealed record Edge<TNode> where TNode : IGraphNode
{
    // ... existing properties ...
    public required EdgeAttachSide SourceAttach { get; init; }
    public required EdgeAttachSide TargetAttach { get; init; }
}
```

Populated by the engine per (mode, kind):

| Mode | Kind | Source attach | Target attach |
|------|------|---------------|----------------|
| IssueGraph | SeriesSibling | (omitted; chain semantics) | Top |
| IssueGraph | SeriesCornerToParent | Bottom | Left |
| IssueGraph | ParallelChildToSpine | Right | Top |
| NormalTree | SeriesSibling | (omitted; chain semantics) | Top |
| NormalTree | SeriesCornerToParent | Bottom | Left |
| NormalTree | ParallelChildToSpine | Bottom | Left |

Where attach side is "(omitted; chain semantics)", we use `Bottom` for source (lower-row endpoint) and `Top` for target (upper-row endpoint? — actually since chain runs top-to-bottom, source is the upper sibling and target is the lower sibling, so source=Bottom, target=Top is correct in both modes).

The `SeriesSibling` row gets concrete values: source=Bottom, target=Top in both modes. The vertical chain doesn't change between modes; only its position relative to the parent does.

This means `SourceAttach`/`TargetAttach` for `SeriesSibling` and `SeriesCornerToParent` are identical between modes — the **only** mode-dependent attach is `ParallelChildToSpine`, which flips Right↔Left and Top↔Bottom because the spine runs the opposite direction.

So really only one row varies. We still expose both attach properties uniformly; renderers can just read them.

### Cycle detection, multi-parent, occupancy matrix — all unchanged

These behaviours live in the shared engine core and are not strategy-specific. Multi-parent flattening produces duplicate appearances regardless of mode. Cycles are caught by path-stack tracking before lane assignment runs. Occupancy matrix is built from edges plus positioned nodes regardless of mode.

### Test-only renderer for NormalTree

We do not ship a `Fleece.Cli` renderer for `NormalTree`. We do ship a small ASCII renderer in the test project to anchor Verify snapshots:

```
tests/Fleece.Core.Tests/Services/GraphLayout/Helpers/NormalTreeAsciiRenderer.cs
tests/Fleece.Core.Tests/Snapshots/NormalTree/*.verified.txt
```

Why test-only:

- No production consumer needs CLI rendering today.
- Externalising the renderer to the test project keeps `Fleece.Cli` free of speculative code.
- Verify snapshots over the renderer's output catch any silent regression in edge construction or occupancy assembly when future changes touch the engine.

When a real consumer arrives, the test renderer can be promoted or replaced by something properly designed. Until then, the test renderer's job is **fidelity verification**, not user-facing presentation.

## Risks / Trade-offs

- **The mirror is not perfectly symmetric in code paths.** `IssueGraph`'s lane advancement for series children is fundamentally different from `NormalTree`'s shared-lane rule. Two distinct strategy implementations are unavoidable. The shared infrastructure (DFS, cycle detection, edge skeleton, occupancy assembly) keeps duplication minimal.
- **`EdgeAttachSide` is new public surface.** Adding it to the `Edge<TNode>` record is a breaking change to anyone who constructed `Edge` instances directly. In practice the engine is the only constructor, so this is internal — but if any external consumer instantiated edges (e.g. in tests), they need updating. We accept this as the price of explicit attach metadata.
- **Test-only renderer is unusual.** Most Fleece renderers live in `Fleece.Cli`. Putting one in the test project is a deliberate signal that no command consumes it. The alternative (a `Fleece.Core/Rendering/` namespace) risks the test renderer becoming a de facto API. Keep it in tests until a real consumer is named.
- **Verify snapshots over a test renderer.** Snapshot tests that depend on a renderer the engine doesn't ship are slightly brittle: if the renderer's glyph choices change, snapshots churn. Mitigation: keep the test renderer minimal and stable; treat it as part of the test surface, not a feature.
- **Multi-parent in NormalTree may produce visually surprising results.** A node with two parents in `NormalTree` mode appears under each parent in DFS order. Rows expand to accommodate the duplicates; lanes do not. Visual layouts can get tall fast for densely cross-linked DAGs. This is consistent with the `IssueGraph` behaviour — neither mode currently does true DAG-aware visual de-duplication — and is documented rather than fixed here.
