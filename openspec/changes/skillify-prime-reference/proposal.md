## Why

`fleece prime` bundles two unrelated things into one SessionStart hook that fires every session: (1) **static reference** — the workflow overview plus nine detailed topics — and (2) a **dynamic CI-gate tripwire** — the live count of active issues that warns an agent the branch must be clean before a PR. The static half costs ~2k tokens on every session, including clean branches where it teaches nothing actionable, while the reference also duplicates the CLAUDE.md memory block. The natural home for pull-based reference is a Claude skill, but a skill cannot carry the tripwire: an agent that does not already know active issues exist has no trigger to invoke it. So we split the surfaces by **why / what / how** rather than swapping one for the other.

## What Changes

- **Move the static reference into an installable skill.** `fleece install` writes `.claude/skills/fleece/SKILL.md` (overview + index) plus `references/<topic>.md` for the nine topics (hierarchy, commands, statuses, sync, json, next, tree, github, v4-migration), sourced from an embedded markdown resource and overwritten wholesale on each install with a "managed by fleece install" header.
- **Shrink `fleece prime` to dynamic state only.** With a `.fleece/` directory present, it emits a short active-issue line and a pointer to the skill when one or more issues are active, and emits **nothing** when zero are active (clean branch → ~0 tokens). It stays silent with exit 0 when `.fleece/` is absent. The SessionStart hook registration is unchanged — it still invokes `fleece prime`.
- **BREAKING: remove `fleece prime <topic>`.** The `[topic]` argument and the nine topic subcommands (`fleece prime github`, etc.) are removed; the skill is the single source of truth for that content. The "Unknown topic" path is removed with them.
- **Rewrite the CLAUDE.md memory block** to be pure philosophy plus a pointer to the skill: branch-local working memory in, issues resolved/promoted/sealed before a PR, and the decision rule — *blocks this PR → Fleece; non-blocking follow-up / new feature / durable work → GitHub issue*.

## Capabilities

### New Capabilities
- `agent-skill-reference`: `fleece install` provisions a pull-based Claude skill (`.claude/skills/fleece/SKILL.md` + `references/`) carrying the full static Fleece reference, and rewrites the CLAUDE.md memory block to state the philosophy and point at that skill.

### Modified Capabilities
- `prime-command`: `fleece prime` becomes a slim, state-aware hook that emits only the dynamic active-issue signal (silent when zero active or no `.fleece/`); the `[topic]` argument and all topic content are removed.

## Impact

- **Code**: `src/Fleece.Cli/Commands/PrimeCommand.cs` (sheds `BuildOverview` static body + nine topic consts; keeps the active-issue count query and slim output), `src/Fleece.Cli/Settings/PrimeSettings.cs` (drops `[topic]`), `src/Fleece.Cli/Commands/InstallCommand.cs` (new `InstallClaudeSkillAsync` step + rewritten `BuildClaudeMemoryBlock`), new embedded `SKILL.md` + `references/*.md` resource(s).
- **Tests**: `tests/Fleece.Cli.Tests/Commands/PrimeCommandTests.cs` (remove the three topic-behavior tests, adjust the overview/clean-branch expectations), `tests/Fleece.Cli.E2E.Tests/Scenarios/MiscCommandScenarios.cs`, and new install-writes-skill coverage.
- **Behavior**: `fleece prime <topic>` stops working (intended). Cross-references in the slim hook output and CLAUDE.md now point at the skill instead of `fleece prime <topic>`.
- **Tokens**: clean sessions drop from ~2k to ~0; dirty sessions keep the same CI-gate signal in two lines.
