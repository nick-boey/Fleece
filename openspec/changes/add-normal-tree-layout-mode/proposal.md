## Why

`generalize-graph-layout-engine` introduces `LayoutMode.NormalTree` as a reserved enum value that throws `NotImplementedException`. This change implements it.

`LayoutMode.IssueGraph` (the only working mode after the prior change) places **children above their parent** with lanes increasing toward the root — the right shape for Fleece's leaf-first dependency view, where actionable work surfaces at the top-left and parents accumulate at the bottom-right. That's wrong for any consumer wanting a conventional top-down tree: parent at the top, children fanning down, root-on-left/leaves-on-right.

The concrete driver is **sub-task graphs**: the user wants to display a sub-task tree underneath a Fleece issue (a separate `IGraphNode` implementation supplied via the existing delegate hooks). For that view, `IssueGraph` orientation is wrong — sub-tasks should descend from the issue, not surface above it. External library consumers hitting `IGraphLayoutService` directly have the same need.

`NormalTree` is the **mirror image** of `IssueGraph` over the row axis. Same engine. Same edge kinds. Same occupancy matrix. Same delegate-driven topology. What flips:

- **Row order**: parent emitted *before* its children (pre-order DFS).
- **Lane direction**: root at lane 0; each child at `parent.lane + 1`.
- **Sibling lane behaviour**: every child of the same parent shares `parent.lane + 1` regardless of `ChildSequencing`. Lateral subtree separation is handled by row sequencing alone (subtrees never collide horizontally because they advance in rows, not lanes).
- **Series corner attaches at the chain TOP**: parent → first series child via L-shape (vertical-then-horizontal-left).
- **Parallel children enter from the LEFT side** (parent's spine is to their left): each parallel child receives a horizontal stub from `parent.lane`.

The user's invariant "parallel = horizontal into child, vertical out of parent" stays constant — only the rendering side flips because the parent is now above instead of below.

## What Changes

- **Implement `LayoutMode.NormalTree` in `GraphLayoutService`.** Replace the `NotImplementedException` branch with a `RootDownwardLaneStrategy` (or equivalent inline implementation) that:
  - Visits each root subtree pre-order (parent first, then children in iterator order).
  - Assigns `child.lane = parent.lane + 1` for every child.
  - Emits children rows after the parent's row, in iterator order.
  - For Series children: sibling lane stays the same; rows advance per child; chain order matches iterator order.
  - For Parallel children: same lane, advancing rows; no special lateral spread.

- **Mirror edge construction for `NormalTree`:**
  - `SeriesSibling` edges still connect consecutive series children of the same parent (vertical chain — same as `IssueGraph`, since the chain is in row order).
  - `SeriesCornerToParent` is **renamed semantically** by orientation — but the engine emits a single `EdgeKind.SeriesCornerToParent` either way, with the chain's *parent-adjacent end* as the source. In `NormalTree` mode, that's the **first** series child instead of the last. The renderer infers chain direction from `Start.Row` vs `End.Row`.
  - `ParallelChildToSpine` edges go from parent's `(parent.row, parent.lane)` outward (vertical down to child's row, horizontal left to child's lane). Pivot lane equals `parent.lane`.

- **Add `EdgeAttachSide` enum on `Edge<TNode>`** to make the visual side explicit so renderers don't have to deduce it from row arithmetic. Values: `Top`, `Bottom`, `Left`, `Right`. Engine populates `SourceAttach` and `TargetAttach` based on mode + edge kind.

- **No new public API surface beyond `EdgeAttachSide`.** Callers that already passed `LayoutMode.NormalTree` (and got a thrown exception) now receive a working `GraphLayout<TNode>`.

- **No CLI changes.** `list --tree` and `list --next` continue using `LayoutMode.IssueGraph` via `IIssueLayoutService`. `NormalTree` is reachable only through `IGraphLayoutService` directly with `Mode = LayoutMode.NormalTree`.

- **Add a renderer for `NormalTree` in the test layer**, not in `Fleece.Cli`. The test renderer is a thin Spectre-based ASCII view used to anchor Verify snapshots for the new mode. It establishes that `NormalTree` produces a renderable, sensible layout without committing the CLI to a new visible surface.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `graph-layout`: extends the existing capability with the implementation of `LayoutMode.NormalTree` (parent-first row order, root-downward lane assignment, mirrored edge emission, `EdgeAttachSide` metadata).

## Impact

- **Code:** new strategy `RootDownwardLaneStrategy` in `src/Fleece.Core/Services/GraphLayout/`; updated edge construction to consult `LayoutMode` and emit correct attach sides; `EdgeAttachSide` enum added to `Edge<TNode>` with values populated for both modes.
- **Public API:** `LayoutMode.NormalTree` becomes functional. `Edge<TNode>` gains `SourceAttach` and `TargetAttach` properties. Callers that don't read them are unaffected.
- **Tests:** new `NormalTreeLayoutTests.cs` covering pre-order row assignment, depth-from-root lane assignment, edge attach sides, parallel-from-left and series-corner-at-top geometry. Reuses the generic `IGraphNode` test fixtures from the prior change.
- **Renderer:** a test-only ASCII renderer (`tests/Fleece.Core.Tests/Services/GraphLayout/Helpers/NormalTreeAsciiRenderer.cs`) plus Verify snapshots anchor the visual contract.
- **Behaviour:** zero change to any existing CLI command, since none use `NormalTree`. The only observable effect is that calling `IGraphLayoutService.Layout` with `Mode = LayoutMode.NormalTree` no longer throws.
