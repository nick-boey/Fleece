## Why

The `fleece create` and `fleece edit` commands currently spawn an external text editor (vim, nano, notepad, macOS `open -W -t`) when invoked without sufficient arguments. This adds complexity, OS-specific fragility, a YAML round-trip, and a poor AI/agent experience. Fleece is a thin CLI that should behave predictably in scripted, headless, and agent-driven workflows — not drop users into an editor.

## What Changes

- **BREAKING**: `fleece create` no longer launches an external editor when invoked with no `--title`/`--type`. It instead errors with a clear message listing required flags.
- **BREAKING**: `fleece edit <id>` no longer launches an external editor when invoked with only an ID. It instead errors with a clear message listing available field flags.
- Add `--linked-pr <PR>` as a first-class flag on `fleece create` and `fleece edit` (previously only settable via keyed tag `hsp-linked-pr=N`). Wired through to `IFleeceService.CreateAsync`/`UpdateAsync`.
- Remove `Fleece.Cli/Services/EditorService.cs` in its entirety (template creation, `OpenEditor`, YAML parsing, cleanup, `IssueTemplate` DTO).
- Remove the `EditWithEditorAsync` path from `EditCommand` and the `CreateWithEditorAsync` path from `CreateCommand`.
- Remove the `~/.fleece/templates/` scratch directory usage.
- Drop the YAML dependency on `YamlDotNet` from the CLI project if it is no longer used elsewhere.
- Update `fleece prime` topics and any user-facing help/docs that reference external-editor behavior.

## Capabilities

### New Capabilities
- `issue-authoring`: Defines how `fleece create` and `fleece edit` accept issue data exclusively through CLI flags, and the error behavior when required flags are missing.

### Modified Capabilities
<!-- None — no existing specs in openspec/specs/ -->

## Impact

- Affected code: `src/Fleece.Cli/Services/EditorService.cs` (deleted), `src/Fleece.Cli/Commands/CreateCommand.cs`, `src/Fleece.Cli/Commands/EditCommand.cs`, `src/Fleece.Cli/Program.cs`, `src/Fleece.Cli/Commands/PrimeCommand.cs`.
- Affected dependencies: `YamlDotNet` package reference in `Fleece.Cli.csproj` if unused post-removal.
- Affected tests: any CLI tests exercising editor-based flows must be removed or rewritten to cover the new error messages.
- Affected users: anyone relying on `fleece create` / `fleece edit <id>` with no args must migrate to flag-based invocation. Document in release notes.
- Out of scope: `Fleece.Core` services (no business-logic change); other commands that never used the editor.
