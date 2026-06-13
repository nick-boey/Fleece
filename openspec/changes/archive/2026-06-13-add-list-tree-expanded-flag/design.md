## Context

The graph layout engine (`IGraphLayoutService` in `Fleece.Core/Services/GraphLayout/GraphLayoutService.cs`) supports two modes: `LayoutMode.IssueGraph` (leaves-first row order, leaf-upward lanes — the shape behind `list --next`) and `LayoutMode.NormalTree` (parent-first row order, root-at-lane-0, depth-from-root lanes). The Fleece-specific adapter `IIssueLayoutService` exposes two methods, `LayoutForTree` and `LayoutForNext`, both of which currently hardcode `LayoutMode.IssueGraph` when calling the engine. NormalTree's only consumer is the test-only `NormalTreeAsciiRenderer` (`tests/Fleece.Core.Tests/Services/GraphLayout/Helpers/NormalTreeAsciiRenderer.cs`), which walks the occupancy matrix to anchor Verify snapshots.

The CLI's `list --tree` path bypasses the engine entirely. `ListCommand.ExecuteTreeMode` calls the older `TreeRenderer.RenderTree` (`Fleece.Cli/Output/TreeRenderer.cs`), which builds its own parent-child traversal and emits one issue per line. `list --next`, in contrast, was migrated to the engine-driven path (`IIssueLayoutService.LayoutForNext` → `TaskGraphRenderer.Render`), which uses doubled rows (one issue every two rows, gap rows for edges).

This change adds a single boolean flag `--expanded` to `list`. Only `list --tree --expanded` is meaningful; the flag opts the tree view into engine-driven NormalTree rendering through the existing `TaskGraphRenderer`. The motivation is two-fold: (1) the operator can validate engine NormalTree output on real production graphs, not just synthetic test fixtures; (2) it surfaces NormalTree as a first-class production code path without removing the compact `TreeRenderer` view that today's users rely on.

## Goals / Non-Goals

**Goals:**

- Add `--expanded` to `ListSettings`, gated to `--tree`.
- Route `list --tree --expanded` through `IIssueLayoutService.LayoutForTree` with `Mode = LayoutMode.NormalTree`, then through the existing `TaskGraphRenderer.Render`.
- Keep `list --tree` (without `--expanded`) byte-for-byte identical to today (compact `TreeRenderer` output).
- Keep `list --next` byte-for-byte identical to today (engine + `TaskGraphRenderer` in `IssueGraph` mode).
- Reject invalid flag combinations explicitly (`--expanded` without `--tree`; `--tree --expanded --json`).
- Anchor the new rendering surface with E2E snapshot tests in `Fleece.Cli.E2E.Tests`.

**Non-Goals:**

- Migrating the compact `--tree` view onto the engine. The compact `TreeRenderer` stays.
- Adding a JSON shape for `--tree --expanded`. JSON output for the engine-rendered view is out of scope (matches the existing `--next --json` rejection).
- Modifying the `GraphLayoutService` engine. NormalTree mode is already implemented and well-tested at the engine layer.
- Modifying `TaskGraphRenderer`. The renderer's per-`EdgeKind` geometry happens to work for both modes (verified by manual trace over `SeriesChain` and `ParallelFan` fixtures); we rely on that and snapshot-test against real layouts to catch any divergence.
- Adding a `--next --normal-tree` or similar mode-selection flag for `--next`. `--next`'s leaves-first orientation is the documented contract for that view.
- Retiring or deprecating `TreeRenderer`.

## Decisions

### Use `LayoutMode.NormalTree`, not `LayoutMode.IssueGraph`, for `--tree --expanded`

NormalTree gives the orientation users expect when they read the word "tree": root at top, children fanning down, depth increasing left-to-right by lane. `IssueGraph` is leaf-first — actionable leaves at top-left, root at bottom-right — which is the right shape for `list --next`'s "what can I work on" surface but the wrong shape for a tree view.

NormalTree also exists today specifically to be the top-down counterpart to `IssueGraph` (see `openspec/changes/archive/2026-04-28-add-normal-tree-layout-mode/proposal.md`). The mode was implemented but never wired to a CLI surface. `--tree --expanded` is the natural production consumer.

**Alternative considered:** use `IssueGraph` mode (matching `--next`). Rejected because the user goal includes "validate that the graph layout algorithm's NormalTree mode is rendering correctly" — using `IssueGraph` would defeat the validation purpose and leave NormalTree without a production consumer.

### Reuse `TaskGraphRenderer` as-is

`TaskGraphRenderer` derives edge geometry from `Edge.Start` / `Edge.End` plus a per-`EdgeKind` pivot heuristic. Manual trace over `NormalTree` edges confirms the heuristic produces correct L-shapes in both modes:

```
SeriesCornerToParent (NormalTree):
  Start = parent (R, L), End = first-child (R+1, L+1)
  pivotCol = startCol = L*2          ← parent's lane
  Vertical at lane L from row R*2 to (R+1)*2
  Horizontal at row (R+1)*2 from L*2 to (L+1)*2
  Result: L-shape going down-then-right (correct for parent→first-child)

ParallelChildToSpine (NormalTree):
  Start = parent (R, L), End = child (R+k, L+1)
  pivotCol = endCol = (L+1)*2        ← child's lane
  Horizontal at row R*2 from L*2 to (L+1)*2
  Vertical at lane (L+1) from row R*2 to (R+k)*2
  Result: L-shape going right-then-down (correct for parent→parallel-child)

SeriesSibling (both modes):
  Start = prev (Rp, L+1), End = curr (Rc, L+1)
  Vertical-only segment, lane shared
```

The renderer never reads `LayoutMode`; it consumes only the `GraphLayout<Issue>` data structure. Both modes produce edges with `startRow < endRow`, both produce L-shapes that match the renderer's pivot rules, and node markers overwrite junction glyphs at node cells. So the renderer is mode-agnostic enough for our needs.

**Alternative considered:** port `NormalTreeAsciiRenderer` to `Fleece.Cli` as a second renderer specialized for NormalTree (occupancy-matrix walking). Rejected because (a) the existing renderer works, (b) duplicating renderers doubles the maintenance surface, and (c) the test-renderer's compact output (one row per node) does not match the user's stated preference for the `--next`-style spaced layout.

### Add an optional `LayoutMode` parameter to `IIssueLayoutService.LayoutForTree`

The signature gains a trailing `LayoutMode mode = LayoutMode.IssueGraph` parameter. The default preserves all existing behaviour (the `--next` path with no matched ids falls through to `LayoutForTree(...)`, which today implicitly uses `IssueGraph` and must continue to). `--tree --expanded` passes `LayoutMode.NormalTree`.

**Alternative considered:** add a new method `LayoutForExpandedTree`. Rejected because the only difference is the mode argument — adding a method when a parameter suffices grows the interface unnecessarily, and the engine itself already exposes mode selection on `GraphLayoutRequest`. The adapter's job is to forward, not to fork.

**Alternative considered:** thread the mode through `LayoutForNext` too. Rejected for scope: `--next` does not need NormalTree, and adding a parameter we never set would be premature surface area.

### Place the new branch in `ListCommand.ExecuteAsync`, not `ExecuteTreeMode`

`ExecuteTreeMode` today loads issues via `FilterAsync`/`SearchWithFiltersAsync` *then* hands a `List<Issue>` to `TreeRenderer`. The engine path needs the full unfiltered issue list (for ancestor context inside `LayoutForTree`'s `CollectIssuesToDisplay` traversal), the same `LoadIssuesWithDiagnosticsAsync` call the `--next` path makes, plus visibility/sort/assignment options. Reusing the `--next` plumbing is cleaner than retrofitting `ExecuteTreeMode`.

The chosen shape: detect `Tree && Expanded` early in `ExecuteAsync`, take a branch parallel to the existing `Next` branch (lines 91–234), and call `LayoutForTree` directly with `mode: NormalTree`. The compact `--tree` path remains in `ExecuteTreeMode` untouched.

**Alternative considered:** introduce a third execution path inside `ExecuteTreeMode`. Rejected: it would require duplicating the load-issues-and-build-graph plumbing that already lives in the `Next` branch.

### Reject `--tree --expanded --json` explicitly

Parallel to `--next --json` (rejected at `ListCommand.cs:42`), the engine-rendered surface has no canonical JSON shape, and inventing one is out of scope. Producing `TreeRenderer.RenderJsonTree` output here would be misleading because that JSON reflects the compact tree's parent-child traversal, not the engine's positioned-node graph.

### Reject `--expanded` without `--tree`

The flag is only meaningful with `--tree`. We could silently no-op when `--expanded` is passed alone, but explicit rejection prevents users from typing `list --expanded` and being confused by the unchanged default output.

## Risks / Trade-offs

- **[Risk]** `TaskGraphRenderer`'s manual L-shape heuristic might fail on a NormalTree edge case we haven't traced (e.g. a node with multiple parents whose parent-rows are not contiguous, or a deeply nested mixed Series/Parallel tree). → **Mitigation:** E2E snapshot tests over multiple fixture shapes (series chain, parallel fan, mixed depth, multi-parent fan-in, terminal-status mix). If a real graph reveals broken geometry, the fix lands in `TaskGraphRenderer` (likely consulting `SourceAttach`/`TargetAttach` rather than re-deriving the pivot from `EdgeKind`), not in this change. The trace performed during exploration covered the dominant edge shapes.

- **[Risk]** Two tree-rendering code paths long-term: the compact `TreeRenderer` and the expanded engine-driven path. Behaviour drift is possible. → **Mitigation:** the compact path is stable and well-snapshotted today; `--expanded` is opt-in and additive, so changes to one path do not silently leak into the other. Long-term consolidation can be a separate change if appetite arises.

- **[Risk]** Wide trees push title text far right because NormalTree puts the deepest leaf at the highest lane. On a tree with depth 8, the title column starts ≈16 characters in. → **Mitigation:** acceptable; users opt into `--expanded` knowing it is a graph view. If readability becomes a real complaint, future work can introduce title wrapping or lane compression.

- **[Trade-off]** `--tree --expanded` is opt-in, so the validation value (operators inspecting engine output) only materializes when someone actively chooses to run it. We are not making NormalTree the default tree view. The trade-off is that we keep the compact view available for everyday use.

- **[Risk]** `LayoutForTree`'s default-parameter change is source-compatible but technically affects the `IIssueLayoutService` interface. Any external consumer mocking the interface needs to update their mock signature. → **Mitigation:** `IIssueLayoutService` is internal to the Fleece codebase today; the only test mocks live in `IssueLayoutServiceTests`. Acceptable.
