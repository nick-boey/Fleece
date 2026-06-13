## 1. Author the embedded skill content

- [x] 1.1 Create the skill source tree in the repo (e.g. `src/Fleece.Cli/Skill/SKILL.md` + `src/Fleece.Cli/Skill/references/<topic>.md`) with a `name`/`description` frontmatter on `SKILL.md` that identifies it as the Fleece command/workflow reference
- [x] 1.2 Port the workflow overview (minus the dynamic active-issue line) from `PrimeCommand.BuildOverview` into `SKILL.md`, plus an index of the nine topics
- [x] 1.3 Port each of the nine topic consts (`hierarchy`, `commands`, `statuses`, `sync`, `json`, `next`, `tree`, `github`, `v4-migration`) from `PrimeCommand` into `references/<topic>.md`, rewriting any `fleece prime <topic>` cross-references to point at the sibling reference files
- [x] 1.4 Add a "managed by `fleece install` — manual edits will be overwritten" header to each generated file (baked into the source files)
- [x] 1.5 Register the skill files as embedded resources in `Fleece.Cli.csproj`

## 2. Slim down `fleece prime`

- [x] 2.1 Remove the `[topic]` argument and its description from `PrimeSettings`
- [x] 2.2 In `PrimeCommand`, delete `BuildOverview`'s static body and the nine topic consts; keep the `.fleece/`-absent silent-exit guard and `CountActiveIssuesAsync`
- [x] 2.3 Implement the new output: silent (exit 0) when `activeCount == 0`; when `activeCount > 0` emit the count, the resolve/promote/seal rule, the CI-gate consequence, and a pointer to the `fleece` skill
- [x] 2.4 Remove the unknown-topic / available-topics code path

## 3. Provision the skill from `fleece install`

- [x] 3.1 Add `InstallClaudeSkillAsync` to `InstallCommand` that writes `.claude/skills/fleece/SKILL.md` and `references/*.md` from the embedded resources, overwriting wholesale, and call it from `ExecuteAsync`
- [x] 3.2 Rewrite `BuildClaudeMemoryBlock` to state the philosophy + the blocks-this-PR → Fleece / non-blocking-or-durable → GitHub decision rule, point at the installed `fleece` skill, and drop content now owned by the skill (keep the marker-delimited in-place refresh)
- [x] 3.3 Confirm `InstallClaudeHooksAsync` still registers `fleece prime` unchanged (no hook rewiring)

## 4. Update tests

- [x] 4.1 In `PrimeCommandTests`, remove the three topic tests (`KnownTopic`, `UnknownTopic`, `NoFleeceDirectory_WithTopic`) and update the overview tests: clean branch emits nothing; active issues emit the count + skill pointer and no static reference
- [x] 4.2 Update `MiscCommandScenarios` prime coverage for the new clean-vs-dirty behavior
- [x] 4.3 Add `InstallCommand` test coverage: install writes `SKILL.md` (with frontmatter description + managed header) and the nine `references/*.md`; re-install overwrites stale content; the CLAUDE.md memory block states the philosophy, the GitHub decision rule, and the skill pointer

## 5. Verify

- [x] 5.1 `dotnet build` and `dotnet test` pass (2 pre-existing `MigrateScenarios` failures are unrelated — the fixture helper checks `Directory.Exists(".git")`, which fails in a git worktree)
- [x] 5.2 Manual smoke: `fleece install` in a scratch repo writes the skill + references and the rewritten memory block; `fleece prime` is silent on a clean branch and emits the signal with an active issue; `fleece prime github` no longer resolves
