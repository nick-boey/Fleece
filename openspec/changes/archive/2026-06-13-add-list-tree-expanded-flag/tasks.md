## 1. Core: extend IIssueLayoutService

- [x] 1.1 Add optional `LayoutMode mode = LayoutMode.IssueGraph` parameter to `IIssueLayoutService.LayoutForTree` (`src/Fleece.Core/Services/Interfaces/IIssueLayoutService.cs`).
- [x] 1.2 Update `IssueLayoutService.LayoutForTree` (`src/Fleece.Core/Services/GraphLayout/IssueLayoutService.cs`) to accept and forward `mode` to `RunEngine`.
- [x] 1.3 Update `IssueLayoutService.RunEngine` to set `GraphLayoutRequest<Issue>.Mode = mode` instead of hardcoding `LayoutMode.IssueGraph`.
- [x] 1.4 Verify `LayoutForNext`'s callsite to `LayoutForTree` (the matched-ids-null fallback) still produces `IssueGraph` output via the default — adjust if necessary so `--next` is byte-for-byte unchanged.

## 2. Core: unit tests for the new parameter

- [x] 2.1 In `tests/Fleece.Core.Tests/Services/GraphLayout/IssueLayoutServiceTests.cs`, add a test asserting that `LayoutForTree(issues)` (no `mode`) produces an `IssueGraph` layout (parent at `max(child.lane) + 1`).
- [x] 2.2 Add a test asserting `LayoutForTree(issues, mode: LayoutMode.NormalTree)` produces NormalTree positions (root at lane 0, children at `parent.lane + 1`, parent-first row order).
- [x] 2.3 Add a test asserting cycle detection still throws `InvalidGraphException` when `mode: LayoutMode.NormalTree` is supplied.

## 3. CLI: add the --expanded flag

- [x] 3.1 Add `Expanded` boolean property with `[CommandOption("--expanded")]` and `[Description(...)]` to `src/Fleece.Cli/Settings/ListSettings.cs`. Description should explain the flag is only meaningful with `--tree` and routes through the engine in NormalTree mode.
- [x] 3.2 In `ListCommand.ExecuteAsync` (`src/Fleece.Cli/Commands/ListCommand.cs`), reject `--expanded` without `--tree` (return exit 1 with a clear `red` markup error).
- [x] 3.3 Reject `--next --expanded` with the same pattern.
- [x] 3.4 Reject `--tree --expanded --json` (and `--tree --expanded --json-verbose`) with the same pattern.

## 4. CLI: route --tree --expanded through the engine

- [x] 4.1 Add an early branch in `ListCommand.ExecuteAsync` (after the validation block, before the `Next` branch and before `ExecuteTreeMode`) that triggers when `Tree && Expanded`.
- [x] 4.2 Inside the new branch, load issues via `LoadIssuesWithDiagnosticsAsync` (matching the `--next` plumbing) and apply hierarchy scoping when `IssueId` / `--children` / `--parents` is supplied (re-use the same resolver logic as the existing `--next` branch).
- [x] 4.3 Apply search/sort/visibility/assignment options from `ListSettings` consistent with the `--next` branch's handling.
- [x] 4.4 Call `IIssueLayoutService.LayoutForTree(allIssues, visibility, assignedTo, sortConfig, mode: LayoutMode.NormalTree)`.
- [x] 4.5 Compute `actionableIds` via `ComputeActionableIds` (already used by `--next`) and call `TaskGraphRenderer.Render(console, graph, actionableIds, matchedIds: null)`.
- [x] 4.6 Confirm the existing `ExecuteTreeMode` path remains untouched and is still reached when `Tree && !Expanded`.

## 5. Cycle and error surfacing

- [x] 5.1 Catch `InvalidGraphException` from `LayoutForTree` in the new branch and print the cycle's issue ids before returning exit 1, matching the existing `--next` cycle behaviour.

## 6. CLI: composition + DI

- [x] 6.1 Verify `IIssueLayoutService` is already registered in `CliComposition` (it must be — `--next` uses it). No new DI registrations expected. Document in the PR if any change is needed.

## 7. E2E snapshot tests

- [x] 7.1 In `tests/Fleece.Cli.E2E.Tests/`, add a new test class `ListTreeExpandedTests` (or analogous) with a fixture covering: (a) a series chain, (b) a parallel fan, (c) a mixed-depth tree, (d) a multi-parent fan-in, (e) a tree containing terminal-status nodes that have active descendants (with `--show-inactive=if-active-children`).
- [x] 7.2 For each fixture, run `list --tree --expanded` and capture the Verify snapshot in `tests/Fleece.Cli.E2E.Tests/Snapshots/`.
- [x] 7.3 Add a regression test that `list --tree` (no `--expanded`) snapshot is byte-for-byte identical to a snapshot captured before this change for the same fixture (or rely on existing snapshots if present).
- [x] 7.4 Add error-path tests asserting non-zero exit + expected stderr content for: `--expanded` alone, `--next --expanded`, `--tree --expanded --json`.

## 8. Manual validation pass

- [x] 8.1 Run `dotnet build` and `dotnet test` — all suites green.
- [x] 8.2 Run `fleece list --tree --expanded` against the local repository's issue set; visually confirm the layout is sensible (roots at the top, lanes increasing with depth, edge L-shapes connecting parents to children).
- [x] 8.3 Run `fleece list --tree --expanded` against an issue with multi-parent fan-in (if the repo contains one); confirm the rendering does not produce broken or overlapping edges. If broken, raise a follow-up issue documenting the failing fixture and add a `[Skip]` or `[Ignore]` marker on the affected snapshot pending a `TaskGraphRenderer` enhancement.

## 9. Documentation polish

- [x] 9.1 Confirm the `[Description]` on `ListSettings.Expanded` reads cleanly in `fleece list --help` output.
- [x] 9.2 Update `CLAUDE.md` only if necessary (this change is an additive flag and should not require new contributor guidance).
