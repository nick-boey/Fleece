## Context

Fleece is a local-first issue tracker whose CLI is designed as a thin wrapper over `Fleece.Core`. Two commands — `create` and `edit` — currently contain a fallback path that writes a YAML template to `~/.fleece/templates/`, spawns an external editor via `$VISUAL`/`$EDITOR` (or a platform default), waits for exit, then parses the result back with `YamlDotNet`.

This path lives in `src/Fleece.Cli/Services/EditorService.cs` and is invoked from:
- `CreateCommand.CreateWithEditorAsync` — triggered when neither `--title` nor `--type` is supplied.
- `EditCommand.EditWithEditorAsync` — triggered when only an ID is supplied.

The behavior is fragile (platform-specific editor discovery, `which` shell-out, quiet failure modes), adds a YAML dependency, creates scratch files on disk, and is hostile to AI agents and non-interactive usage. The `fleece` binary has no other interactive TUI surface (a broken one was removed in commit b32230f).

## Goals / Non-Goals

**Goals:**
- Remove all code paths that spawn an external editor.
- Make `create` and `edit` error clearly when required flags are missing, so scripts and agents get actionable feedback.
- Delete dead code: `EditorService`, `IssueTemplate`, template-directory management.
- Drop the `YamlDotNet` CLI dependency if no other CLI code uses it.

**Non-Goals:**
- No changes to `Fleece.Core` services or storage format.
- No new interactive prompt / TUI substitute. If the user wants guided authoring, that is a separate change.
- No migration shim — the editor fallback has no persistent state to migrate (template files are created on demand and deleted on exit).

## Decisions

### Decision 1: Error, do not fall back

When required flags are missing, fail fast with a clear error that lists the flags the user needs.

- **Chosen**: Print an error to stderr, exit non-zero. Example: `Error: --title is required. See 'fleece create --help'.`
- **Rejected — silent no-op**: Silent exit would confuse users coming from the editor-based workflow.
- **Rejected — interactive prompt**: Reintroduces interactivity; breaks agents and non-TTY contexts.
- **Rejected — read YAML from stdin**: Adds a new input surface; not requested; can be proposed later as a separate change.

### Decision 2: Delete `EditorService` entirely rather than deprecate

- **Chosen**: Delete the file, remove all references, remove the `YamlDotNet` using directives, and remove the `IssueTemplate` DTO (only consumed by the deleted service).
- **Rejected — keep as internal utility**: No other caller. Dead code rots.

### Decision 3: Drop `YamlDotNet` dependency if unused

After deletion, grep `src/Fleece.Cli` for any remaining `YamlDotNet` reference. If none, remove the `<PackageReference>` from `Fleece.Cli.csproj`. Keep it if `Fleece.Core` or any other CLI file still needs it (verify during implementation, do not assume).

### Decision 4: Error-message ergonomics for `edit`

`edit` has ~11 optional field flags. The "no flags" error message should list them (or at least point to `--help`) rather than leaving the user guessing. Choose: `Error: edit requires at least one field flag (e.g. --title, --description, --status, --type, --priority, --linked-issues, --assigned-to, --tags, --working-branch-id, --execution-mode, --linked-pr). See 'fleece edit --help'.`

### Decision 5: `--json`/`--json-verbose` alone should not count as "has options"

The current `HasNoOptions` helper in `EditCommand` treats `--json` and `--json-verbose` as "has options", which silently bypassed the editor. After this change, an invocation of `fleece edit abc123 --json` with no field flags must also error — JSON formatting without an update has no semantic meaning. Update `HasNoOptions` (or its replacement) to only check real field flags.

## Risks / Trade-offs

- **Risk**: Users with muscle memory for `fleece create` / `fleece edit <id>` dropping them into an editor will see an error until they adapt.
  → **Mitigation**: Error messages name the required flags; release notes + updated `fleece prime` topics call out the removal.
- **Risk**: Third-party docs or scripts (e.g., README, wiki) still document editor-based workflow.
  → **Mitigation**: Grep the repo for "editor" / "EDITOR" references in markdown and CLI help strings; update during implementation.
- **Trade-off**: Removing the editor path is a net-negative for interactive humans who liked it, and a net-positive for agents/scripts. Matches the direction of the codebase (broken TUI already removed; fleece is CLI-first).

## Migration Plan

1. Land the change in a single PR. No feature flag — the behavior is deterministic and the fallback is cosmetic.
2. Release notes: call out the BREAKING change and give the flag-based equivalent for each previous invocation pattern.
3. No data migration needed; `~/.fleece/templates/` only contains transient files and can be left alone on existing installs (they are already deleted after each editor session).

## Open Questions

- Should `fleece create` with no args (and no `--title`/`--type`) point users at `fleece prime commands` instead of just `--help`? Defer to implementation; either is acceptable.
- Is `YamlDotNet` used elsewhere in the CLI? Verify during implementation and drop the dependency only if truly unused.
