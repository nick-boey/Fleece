## 1. Config layer (Core)

- [x] 1.1 Add `Tracker` (string, nullable) to the `FleeceSettings` record; keep JSON source-gen contexts valid
- [x] 1.2 Add a `tracker` case to `SettingsService.SetSettingAsync` (accept `github`/`linear`, reject others; empty clears)
- [x] 1.3 Surface the effective tracker (default `github`) via `EffectiveSettings` so commands/DI can resolve it
- [x] 1.4 Unit tests: default is `github`, set/clear round-trips, invalid value rejected

## 2. Tracker-workflow seam (CLI)

- [x] 2.1 Define `ITrackerWorkflow { PromoteAsync, AbsorbAsync, AuthAsync }` with tracker-neutral result/payload types
- [x] 2.2 Implement `GitHubTrackerWorkflow` wrapping the existing `IGitHubService` (behaviour unchanged from today)
- [x] 2.3 Implement `LinearTrackerWorkflow` as CLI-local emit/record/guidance with zero network calls
- [x] 2.4 In `CliComposition`, resolve the effective tracker and register the matching `ITrackerWorkflow`

## 3. Command behaviour

- [x] 3.1 Add `--ref <ref>` to `PromoteSettings`
- [x] 3.2 Route `PromoteCommand` through `ITrackerWorkflow`; GitHub path unchanged; Linear path = emit (bare) then record (`--ref`), skipping already-promoted, no auth-gate; `--json` emits `{title, body, issueIds}` on emit
- [x] 3.3 Generalize the `promoted=` tag value to a string ref (GitHub stores the number's string form)
- [x] 3.4 Route `AbsorbCommand` through `ITrackerWorkflow`; GitHub path unchanged; Linear path prints `fleece create ... --tag absorbed-from=<ref>` guidance and makes no API call / no state change
- [x] 3.5 Route `AuthCommand` through `ITrackerWorkflow`; Linear path reports not-applicable, exits 0, and `--json` adds `tracker` + `applicable`

## 4. Install selection

- [x] 4.1 Add `--tracker github|linear` to `InstallSettings`
- [x] 4.2 Resolve the tracker in `InstallCommand`: explicit flag > interactive `SelectionPrompt` (TTY) > default `github`; persist to `.fleece/settings.json`
- [x] 4.3 Make `BuildClaudeMemoryBlock` take the tracker and emit the tracker-worded block (fix the "guidance above" wording to reference the repository's instructions)
- [x] 4.4 Ship the core skill references + `github.md` XOR `linear.md` (not both) + a tracker-named `SKILL.md`

## 5. Skill assets

- [x] 5.1 Apply the tracker-agnostic rewrite to `Skill/SKILL.md`, `references/commands.md`, `references/github.md`, `references/statuses.md`, `references/v4-migration.md`
- [x] 5.2 Add `Skill/references/linear.md` (Linear hand-off via MCP + `fleece promote --ref` / `fleece create --tag`)
- [x] 5.3 Register `linear.md` as an `<EmbeddedResource>` in `Fleece.Cli.csproj`

## 6. Tests

- [x] 6.1 Update `InstallScenarios`: ten skill references, `--tracker github|linear` matrix, tracker-worded CLAUDE block + selective `github.md`/`linear.md` assertions
- [x] 6.2 Add hermetic E2E for Linear `promote` (emit-then-record, idempotency, `--json` payload) with no fake GitHub service
- [x] 6.3 Add E2E for Linear `absorb` guidance and tracker-aware `auth` (text + `--json`)
- [x] 6.4 Regenerate affected Verify snapshots for the new/changed human-readable output (none affected — the only committed snapshots are `ListTreeExpanded*`; promote/absorb/auth/install output is asserted structurally, not snapshotted)

## 7. Verification

- [x] 7.1 `dotnet build` and `dotnet test` green across all test projects
- [x] 7.2 Manually run `fleece install --tracker linear` then `fleece install --tracker github` in a scratch repo; confirm the correct skill file, CLAUDE block, and `.fleece/settings.json` each time
