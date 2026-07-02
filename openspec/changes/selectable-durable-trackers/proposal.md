## Why

Fleece hard-codes GitHub Issues as the one durable home for promoted work — the wording, the
commands (`promote`/`absorb`/`auth`), the installed skill, and the CLAUDE memory block all assume
GitHub. Teams that track durable work in Linear (or elsewhere) have no first-class path: today they
must hand-edit the installed skill on every `fleece install`. We want the durable tracker to be a
real, per-repository choice — selected at install and honoured by the CLI — while keeping Fleece
itself free of any tracker SDK. Linear integration is **agent-realized**: the CLI never calls Linear
(its MCP tooling is reachable only by the agent, not the `fleece` process), so a config field names
the active tracker and the commands adapt their behaviour to it.

## What Changes

- Add a per-repo `tracker` setting (`github` | `linear`, default `github`) stored in the existing
  tracked `.fleece/settings.json`. Existing repos are unaffected (default preserves today's behaviour).
- `fleece install` gains `--tracker github|linear` (interactive `SelectionPrompt` when a TTY and
  unset). Install persists the choice, writes a **tracker-worded** CLAUDE memory block, and ships the
  core skill references plus `github.md` **XOR** `linear.md` and a SKILL.md that names the chosen tracker.
- Introduce a CLI-layer `ITrackerWorkflow` seam with two implementations — `GitHubTrackerWorkflow`
  (wraps today's `IGitHubService` unchanged) and `LinearTrackerWorkflow` (CLI-local, zero API calls).
  `promote`/`absorb`/`auth` resolve the workflow from the configured tracker.
- `promote` in Linear mode is **emit-then-record**: a bare invocation emits the bundle title/body
  (and `--json`) for the agent to push into Linear and changes no state; `--ref <linear-id-or-url>`
  records `promoted=<ref>` and sets `Promoted`. The GitHub auth-gate runs only when `tracker=github`.
- `absorb` stays GitHub-native; in Linear mode it prints guidance to run
  `fleece create ... --tag absorbed-from=<ref>` (the agent performs the Linear-side comment/assign via MCP).
- `auth` becomes tracker-aware: in Linear mode it reports that the CLI does not authenticate to Linear
  and exits `0`; `--json` gains `tracker` and `applicable`.
- **BREAKING (data shape):** the `promoted=<#>` keyed tag value generalizes from an integer to a
  string ref so Linear identifiers (`ENG-42`) or URLs fit. GitHub promotions still store the number.
- Add a `references/linear.md` skill reference and apply the tracker-agnostic rewrite to the existing
  skill files; register `linear.md` as an embedded resource.

## Capabilities

### New Capabilities
- `durable-tracker-selection`: a per-repository, install-selected durable issue tracker — its
  persisted `tracker` setting and default, the `fleece install --tracker` selection + prompt +
  persistence, the tracker-worded CLAUDE memory block and tracker-specific skill shipping, the
  `ITrackerWorkflow` seam, and the Linear-mode behaviours of `promote` (emit/record), `absorb`
  (guidance), and `auth` (not-applicable).

### Modified Capabilities
- `github-integration`: the `promote`/`absorb`/`auth` requirements are scoped to "when the active
  tracker is `github`", the promote auth-gate is qualified as GitHub-only, and the `promoted=` tag
  value is generalized from `<github-#>` to a string ref (GitHub stores the number).

## Impact

- **Config/Core**: `FleeceSettings.Tracker` (+ JSON source-gen already covers `FleeceSettings`);
  `SettingsService.SetSettingAsync` gains a `tracker` case. No new Core dependency.
- **CLI**: new `ITrackerWorkflow` + `GitHubTrackerWorkflow` + `LinearTrackerWorkflow`;
  `CliComposition` selects by settings; `PromoteCommand`/`AbsorbCommand`/`AuthCommand` (+ `PromoteSettings`
  gains `--ref`); `InstallCommand` (`BuildClaudeMemoryBlock(tracker)` + selective skill shipping) +
  `InstallSettings` (`--tracker`).
- **Skill/assets**: `src/Fleece.Cli/Skill/*.md` rewrite + new `references/linear.md`;
  `Fleece.Cli.csproj` embeds `linear.md`.
- **Tests**: `InstallScenarios` (ten refs, tracker-worded block, `--tracker` matrix); new
  promote/absorb/auth Linear-mode E2E + snapshots — the Linear path is hermetic (`MockFileSystem`
  only, no fake GitHub service required).
- **Explicitly out of scope**: no `Fleece.Linear` assembly / GraphQL / new NuGet dep; no
  `IGitHubService`→`IIssueTrackerService` rename; no per-command `--tracker` flag; no `.fleece/config.yaml`.
