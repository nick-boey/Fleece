## Why

`LayoutMode.NormalTree` was implemented in the graph layout engine to support top-down tree rendering (parent-first row order, root-at-lane-0, depth-from-root lanes), but it has no production code path. The only consumer is a test-only `NormalTreeAsciiRenderer` that anchors Verify snapshots; the CLI never exercises NormalTree against real issue data. That leaves a gap: there is no way to validate that the engine's NormalTree output renders correctly through the production renderer (`TaskGraphRenderer`) on real graphs with real `Issue` topology — multi-parent fan-in, mixed Series/Parallel subtrees, terminal-status nodes, and so on.

Separately, `list --tree` is the only `list` view still using the old `TreeRenderer` (one issue per line, hand-rolled parent-child ASCII traversal) rather than the graph layout engine. Adding an opt-in expanded view that routes through the engine closes both gaps in one move: it gives operators a way to inspect NormalTree output on real data, and it brings `--tree` partially onto the engine without disrupting the compact view that exists today.

## What Changes

- **Add `--expanded` boolean flag** to `fleece list` (declared on `ListSettings`). The flag is only meaningful in combination with `--tree`.
- **`list --tree --expanded` SHALL render through the graph layout engine** in `LayoutMode.NormalTree`, using the existing `TaskGraphRenderer`. Output style matches `--next`: one issue every two rows, gap rows for edges, full edge connections (`SeriesSibling`, `SeriesCornerToParent`, `ParallelChildToSpine`) drawn from the engine-emitted `Edge<Issue>` collection.
- **`list --tree` (without `--expanded`) is unchanged.** The existing `TreeRenderer` (one issue per line, parent-child ASCII tree) continues to handle that path.
- **Extend `IIssueLayoutService.LayoutForTree`** with an optional `LayoutMode` parameter (default `LayoutMode.IssueGraph`, preserving current behaviour for `--next`). When `--tree --expanded` calls it, the parameter is set to `LayoutMode.NormalTree`.
- **Validate flag combinations**:
  - `--expanded` without `--tree` → error.
  - `--tree --expanded --json` → error (no JSON shape for the engine-rendered view; parallel to the existing `--next --json` rejection).
  - `--tree --expanded --one-line` → already covered by the existing `--tree`/`--next` vs `--one-line` rule; `--expanded` does not change it.
- **No changes to `--next`, `--tree` (compact), `--tree-root`, hierarchy filtering (`<id>`, `--children`, `--parents`), search, sort, or any other existing flag.** The compact `TreeRenderer` path is preserved verbatim.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `graph-layout`: extends the `IIssueLayoutService` contract so `LayoutForTree` accepts an optional `LayoutMode` argument and supports `LayoutMode.NormalTree` end-to-end (engine → adapter → CLI renderer). Adds requirements covering the `list --tree --expanded` rendering surface and the flag-combination validation rules.

## Impact

- **Code (`Fleece.Core`):** `IIssueLayoutService.LayoutForTree` gains an optional `LayoutMode` parameter; `IssueLayoutService.RunEngine` propagates it to `GraphLayoutRequest<Issue>.Mode`. No changes to `GraphLayoutService` itself — `NormalTree` mode already exists.
- **Code (`Fleece.Cli`):** `ListSettings` gains `Expanded`. `ListCommand.ExecuteAsync` adds the flag-combination guards, and `ExecuteTreeMode` (or a new branch before it) routes to `IIssueLayoutService.LayoutForTree(..., mode: NormalTree)` + `TaskGraphRenderer.Render` when `Expanded` is true. Existing `--tree` path unchanged.
- **Public API:** `IIssueLayoutService.LayoutForTree` signature gains an optional parameter — additive, source-compatible. No CLI flag removals or renames.
- **Tests:** new E2E snapshot coverage in `tests/Fleece.Cli.E2E.Tests/` for `--tree --expanded` against representative fixtures (series chain, parallel fan, mixed depth, multi-parent fan-in). Existing snapshots for `--tree` (compact) and `--next` MUST remain byte-for-byte identical. New unit coverage in `IssueLayoutServiceTests` for the `mode` parameter passthrough.
- **Behaviour:** zero change to any existing command invocation. The only observable effects are the new `--expanded` flag, the new error messages for invalid flag combinations, and the new rendering surface when both `--tree` and `--expanded` are passed.
- **Documentation:** the `--expanded` flag's purpose (validation of engine output, intentional duplication of `--tree` for an alternative view) belongs in the proposal/design — there is no CLI doc set requiring updates beyond the flag's own `[Description("...")]` attribute.
