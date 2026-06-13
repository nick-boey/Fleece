<!-- This file is managed by `fleece install`. Manual edits will be overwritten on the next install. -->

# Next Issues

Find issues that are ready to be worked on based on dependencies, execution mode, and status.

## Usage

- `fleece next` - Show all actionable issues
- `fleece next --parent <id>` - Show next issues only under a specific parent
- `fleece next --json` - Output as JSON

## How It Works

The `next` command evaluates the task graph to find issues that are unblocked and ready for work.
It considers parent-child relationships, execution mode (series/parallel), and current status
to determine which issues can be picked up next.
