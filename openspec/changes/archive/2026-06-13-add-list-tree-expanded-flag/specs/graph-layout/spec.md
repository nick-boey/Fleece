## MODIFIED Requirements

### Requirement: Fleece.Core SHALL provide IIssueLayoutService as the Fleece-specific adapter

`Fleece.Core` SHALL expose `IIssueLayoutService` with `LayoutForTree` and `LayoutForNext` methods that wrap `IGraphLayoutService` and supply Fleece-specific delegates: a root finder over `Issue.ActiveParentIssues` and a child iterator that respects sort order, soft-deletion, and (for next mode) the existing `HasActiveDescendants` predicate.

The adapter SHALL unwrap `GraphLayoutResult.Success` and SHALL throw `InvalidGraphException` (carrying the cycle) when the engine returns `CycleDetected`, since CLI rendering paths assume a clean DAG.

`LayoutForTree` SHALL accept an optional `LayoutMode mode` parameter (default `LayoutMode.IssueGraph`) and SHALL forward it to the underlying `GraphLayoutRequest<Issue>.Mode`. The default value preserves prior behaviour for every call site that does not opt in.

```csharp
public interface IIssueLayoutService
{
    GraphLayout<Issue> LayoutForTree(
        IReadOnlyList<Issue> issues,
        InactiveVisibility visibility = InactiveVisibility.Hide,
        string? assignedTo = null,
        GraphSortConfig? sort = null,
        LayoutMode mode = LayoutMode.IssueGraph);

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

#### Scenario: LayoutForTree defaults to IssueGraph when mode is omitted
- **WHEN** a caller invokes `LayoutForTree(issues)` or `LayoutForTree(issues, visibility, assignedTo, sort)` without supplying the `mode` argument
- **THEN** the engine receives `GraphLayoutRequest<Issue>.Mode = LayoutMode.IssueGraph`
- **AND** the returned `(Row, Lane)` positions are byte-for-byte identical to the layout produced before the parameter was introduced

#### Scenario: LayoutForTree forwards LayoutMode.NormalTree to the engine
- **WHEN** a caller invokes `LayoutForTree(issues, mode: LayoutMode.NormalTree)`
- **THEN** the engine receives `GraphLayoutRequest<Issue>.Mode = LayoutMode.NormalTree`
- **AND** the returned `GraphLayout<Issue>` exhibits the parent-first row order, root-at-lane-0 lane assignment, and edge attach sides defined by the `LayoutMode.NormalTree` requirement

## ADDED Requirements

### Requirement: list --tree --expanded SHALL render the tree view through LayoutMode.NormalTree

`fleece list --tree --expanded` SHALL produce a graph rendering by calling `IIssueLayoutService.LayoutForTree` with `mode: LayoutMode.NormalTree` and passing the resulting `GraphLayout<Issue>` to `Fleece.Cli.Output.TaskGraphRenderer.Render`. The output SHALL match the `--next` visual style: one issue per node row with gap rows between nodes, full edge connections (`SeriesSibling`, `SeriesCornerToParent`, `ParallelChildToSpine`) drawn from the engine-emitted `Edge<Issue>` collection.

`fleece list --tree` (without `--expanded`) SHALL continue to use the existing `Fleece.Cli.Output.TreeRenderer.RenderTree` compact view (one issue per line, parent-child ASCII traversal). The compact view's behaviour, output, and snapshot tests SHALL NOT change.

The expanded view SHALL respect the same hierarchy-scoping options as the compact view: `<id>` (with optional `--children`/`--parents`), `--tree-root` (deprecated), `--status`, `--type`, `--priority`, `--assigned`, `--tag`, `--linked-pr`, `--search`, `--all`, `--strict`, `--sync-status`, and `--show-inactive`.

#### Scenario: --tree --expanded routes through the engine in NormalTree mode
- **WHEN** the user runs `fleece list --tree --expanded` against an issue set with parent-child relationships
- **THEN** `ListCommand` resolves `IIssueLayoutService` from DI and invokes `LayoutForTree(allIssues, ..., mode: LayoutMode.NormalTree)`
- **AND** the resulting `GraphLayout<Issue>` is passed to `TaskGraphRenderer.Render`
- **AND** the rendered output places roots at the top-left, descends into children below, and assigns each child's lane as `parent.lane + 1`

#### Scenario: --tree (compact) is unchanged
- **WHEN** the user runs `fleece list --tree` without `--expanded`
- **THEN** `ListCommand` invokes `TreeRenderer.RenderTree` with the filtered issue list
- **AND** the rendered output is byte-for-byte identical to the snapshot captured before this change
- **AND** `IIssueLayoutService.LayoutForTree` is NOT called for this path

#### Scenario: --tree --expanded with hierarchy scoping
- **WHEN** the user runs `fleece list <id> --tree --expanded` for an existing issue id
- **THEN** the displayed issues are limited to `<id>` plus its parents and children (the same scoping `--tree` applies today)
- **AND** the engine receives only the scoped set as the input to `LayoutForTree`
- **AND** the rendered graph contains exactly the scoped nodes

#### Scenario: --tree --expanded surfaces engine cycle errors
- **WHEN** the user runs `fleece list --tree --expanded` against an issue set containing a parent cycle
- **THEN** the command exits non-zero
- **AND** the error message names the cycle's issue ids (the same surface `--next` produces today via `InvalidGraphException`)

### Requirement: --expanded SHALL be rejected outside --tree

The `--expanded` flag SHALL be meaningful only when combined with `--tree`. Any other combination SHALL be rejected with a clear error before issue loading occurs.

#### Scenario: --expanded without --tree errors
- **WHEN** the user runs `fleece list --expanded` (no `--tree`, no `--next`)
- **THEN** the command exits with a non-zero status code
- **AND** stderr contains a message naming `--expanded` and explaining that it requires `--tree`

#### Scenario: --next --expanded errors
- **WHEN** the user runs `fleece list --next --expanded`
- **THEN** the command exits with a non-zero status code
- **AND** stderr contains a message that `--expanded` cannot be used with `--next`

#### Scenario: --tree --expanded --json errors
- **WHEN** the user runs `fleece list --tree --expanded --json`
- **THEN** the command exits with a non-zero status code
- **AND** stderr contains a message that `--tree --expanded` cannot be used with `--json` (parallel to the existing `--next --json` rejection)
