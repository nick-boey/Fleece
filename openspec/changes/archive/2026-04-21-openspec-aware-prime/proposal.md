## Why

When Fleece is used in projects that also use OpenSpec, agents currently have no guidance on how to connect the two systems. This leads to ad-hoc or missing links between Fleece issues and OpenSpec changes, dual tracking (per-task Fleece issues mirroring OpenSpec task lists), and inconsistent branching/tagging conventions. The `fleece prime` command is the natural place to surface this guidance because it already runs automatically at session start via the Claude Code hook installed by `fleece install`.

## What Changes

- `fleece prime` gains awareness of OpenSpec. When an `openspec/` directory exists as a sibling of `.fleece/`, the default (no-topic) output is extended with an "OpenSpec Integration" section explaining how to link Fleece issues to OpenSpec changes.
- A new dedicated `openspec` topic is added so `fleece prime openspec` prints the integration guidance regardless of whether `openspec/` is present. This keeps the topic discoverable and testable independently of filesystem state.
- The topics list printed at the bottom of the overview gains `openspec` as an available topic.
- The guidance content itself establishes conventions:
  - Link a Fleece issue to an OpenSpec change using the keyed tag `openspec={change-name}`.
  - Resolve which issue to link via a decision tree that considers the current branch name (`+<id>` suffix), open unlinked issues, and relevance to the change being proposed.
  - When multiple changes are created in one session, use Fleece hierarchy (parent/child, lex-order, execution-mode) to organise one issue per change.
  - Never create Fleece issues per task or per phase of an OpenSpec change.

No breaking changes. Existing `fleece prime` output is unchanged in repositories that do not have an `openspec/` directory.

## Capabilities

### New Capabilities
- `prime-command`: The behaviour of the `fleece prime` command, including its overview output, topic dispatch, and conditional content based on which sibling directories (e.g. `.fleece/`, `openspec/`) are detected in the current working directory.

### Modified Capabilities
<!-- None. openspec/specs/ is empty; this is the first spec in the project. -->

## Impact

- **Code**: `src/Fleece.Cli/Commands/PrimeCommand.cs` is the only source file touched. A new `OpenSpecContent` constant is added, registered in the `Topics` dictionary, and conditionally appended to `OverviewContent` when `openspec/` is detected. The topics list near the end of `OverviewContent` gains `openspec`.
- **Tests**: New unit tests for `PrimeCommand` covering: topic lookup for `openspec`, overview output when `openspec/` is absent (unchanged), overview output when `openspec/` is present (contains the integration section), and `.fleece/` absent (silent exit, unchanged).
- **Downstream agents**: Any Claude Code session started in a repo with `openspec/` + `.fleece/` will receive the new guidance at session start via the existing `SessionStart` hook. No agent-side configuration required.
- **No API or data model changes**. The multi-value keyed tag semantics the guidance relies on (`openspec=foo`, `openspec=bar` on one issue; `--tag openspec` matches by key) are already supported by the existing tag system and have been verified by probe.
- **No dependency changes**.
