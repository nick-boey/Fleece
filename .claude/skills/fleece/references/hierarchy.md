<!-- This file is managed by `fleece install`. Manual edits will be overwritten on the next install. -->

# Issue Hierarchy

Break down complex work using parent-child relationships.

## Creating Sub-issues

`fleece create -t <title> -y <issue-type> --parent-issues <parent-id>[:<lex-order>]`

`lex-order` is an optional string used for lexical ordering of issues, e.g. "aaa", "bbb". Use a minimum of three characters by default.

Multiple parents are comma delimited: `--parent-issues "id-1,id-2"`

## Managing Dependencies

Use `fleece dependency` to add, remove, or reorder parent-child relationships on existing issues.

### Add Dependency

`fleece dependency --parent <parent-id> --child <child-id>`

### Positioning

Control sibling order when adding:
- `--first` - Place at beginning
- `--last` - Place at end (default)
- `--after <sibling-id>` - Place after a sibling
- `--before <sibling-id>` - Place before a sibling

Example: `fleece dependency --parent abc123 --child def456 --after ghi789`

### Remove Dependency

`fleece dependency --parent <parent-id> --child <child-id> --remove`

### When to Use

- **`fleece create --parent-issues`** - When creating a new issue with known parent(s)
- **`fleece edit --parent-issues`** - When replacing all parents at once
- **`fleece dependency`** - When adding/removing individual parent relationships or when precise sibling ordering is needed

## Viewing Hierarchy

- `fleece list --tree` - Display issues as parent-child tree
- `fleece list --next` - Display issues as a task graph, with next tasks shown next
- `fleece list --tree --json` - Get hierarchy as JSON
- `fleece list <id>` - Show an issue with its entire parent and child hierarchy
- `fleece list <id> --parents` - Show an issue with only its parent hierarchy
- `fleece list <id> --children` - Show an issue with only its child hierarchy

## Hierarchy Workflow

1. Create child issues with `--parent-issues` pointing to parent
2. Use `fleece list --tree` to visualize work breakdown
3. Complete children before marking parent complete
4. Run `fleece validate` to check for circular dependencies

## Execution order

An issue's children may be executed in parallel or series. This is denoted by the execution order field, which may be given by
`fleece edit <id> --execution-order [series|parallel]`. The task graph in `fleece list --next` orders the tasks appropriately.
