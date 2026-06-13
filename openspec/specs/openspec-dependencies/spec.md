# openspec-dependencies Specification

## Purpose
TBD - created by archiving change fleece-v4-ephemeral-memory. Update Purpose after archive.
## Requirements
### Requirement: `fleece openspec dependencies` SHALL render a DAG of OpenSpec changes

The `fleece openspec dependencies` command SHALL scan `openspec/changes/<name>/dependencies.md` files, parse the YAML frontmatter field `depends-on` (a list of OpenSpec change names) to build a directed graph with an edge from each change to each change it depends on, and render that graph using the existing graph-layout `next` renderer. Soft dependencies written in the HTML-comment body of `dependencies.md` SHALL be ignored. A change folder with no `dependencies.md`, or with `depends-on: []`, SHALL appear as a node with no outgoing dependency edges.

#### Scenario: Build edges from depends-on frontmatter
- **WHEN** `openspec/changes/change-b/dependencies.md` declares `depends-on: [change-a]`
- **THEN** the rendered graph contains an edge from `change-b` to `change-a`

#### Scenario: Empty or missing dependencies render as standalone nodes
- **WHEN** a change folder has `depends-on: []` or no `dependencies.md`
- **THEN** that change is rendered as a node with no outgoing dependency edges

#### Scenario: Soft dependencies in comments are ignored
- **WHEN** `dependencies.md` lists change names only inside an HTML comment body
- **THEN** those names produce no edges in the rendered graph

### Requirement: `fleece openspec dependencies` SHALL warn on circular dependencies

The command SHALL reuse the dependency cycle detection used by `fleece validate` and SHALL emit a warning identifying the changes involved when the `depends-on` graph contains a cycle.

#### Scenario: Cycle detected
- **WHEN** the `depends-on` graph contains a cycle (e.g. `a → b → a`)
- **THEN** the command prints a warning naming the changes in the cycle

