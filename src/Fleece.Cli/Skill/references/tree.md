<!-- This file is managed by `fleece install`. Manual edits will be overwritten on the next install. -->

# Tree View

Display issues in a tree view based on parent-child relationships.

## Usage

- `fleece list --tree` - Display issues as parent-child tree
- `fleece list --next` - Display as a task graph showing approximate execution ordering
- `fleece list --tree --json` - Get hierarchy as JSON
- `fleece list <id>` - Show an issue with its entire parent and child hierarchy
- `fleece list <id> --parents` - Show an issue with only its parent hierarchy
- `fleece list <id> --children` - Show an issue with only its child hierarchy
- `fleece list <id> --tree` - Show issue hierarchy in tree format
- `fleece list <id> --next` - Show issue hierarchy in task graph format

## Deprecated Options

- `fleece list --tree --tree-root <id>` - [DEPRECATED] Use `fleece list <id> --children` instead

## Filtering

- `fleece list --tree -s <status>` - Filter by status
- `fleece list --tree -y <type>` - Filter by type
- `fleece list --tree -a` - Show all issues including terminal statuses

## Task Graph

The `--next` flag displays issues in a bottom-up task graph that shows the approximate
ordering of tasks based on their dependencies and execution mode (series/parallel). This is
useful for understanding what needs to be done and in what order.
