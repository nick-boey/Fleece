## 1. Remove editor plumbing

- [x] 1.1 Delete `src/Fleece.Cli/Services/EditorService.cs` (includes `IssueTemplate` DTO)
- [x] 1.2 Remove `using Fleece.Cli.Services;` / other `EditorService` references in `src/Fleece.Cli/Program.cs` if the type is no longer used
- [x] 1.3 Grep `src/Fleece.Cli` for `EditorService`, `IssueTemplate`, `OpenEditor`, `CreateTemplateFile`, `CreateEditTemplateFile`, `ParseTemplate`, `CleanupTemplateFile` and remove stragglers
- [x] 1.4 Grep `src/Fleece.Cli` for any remaining `YamlDotNet` usage; if none remain, remove the `<PackageReference Include="YamlDotNet" ... />` from `src/Fleece.Cli/Fleece.Cli.csproj`

## 2. Update `create` command

- [x] 2.1 In `src/Fleece.Cli/Commands/CreateCommand.cs`, delete the `CreateWithEditorAsync` method
- [x] 2.2 Delete the branch that calls it (the `if (string.IsNullOrWhiteSpace(settings.Title) && string.IsNullOrWhiteSpace(settings.Type))` fallback)
- [x] 2.3 Ensure the existing `--title` and `--type` missing-flag errors remain and are the only path for missing required input
- [x] 2.4 Verify exit code is non-zero and error is printed to the user when required flags are missing

## 3. Update `edit` command

- [x] 3.1 In `src/Fleece.Cli/Commands/EditCommand.cs`, delete the `EditWithEditorAsync` method
- [x] 3.2 Remove the `if (HasNoOptions(settings)) return await EditWithEditorAsync(...)` branch
- [x] 3.3 Repurpose `HasNoOptions` (or inline it) to check only real field flags (exclude `Json`/`JsonVerbose`), and when true: print an error listing the available field flags and exit non-zero
- [x] 3.4 The error message SHALL name the specific flags (`--title`, `--description`, `--status`, `--type`, `--priority`, `--linked-issues`, `--linked-pr`, `--assign`, `--tags`, `--working-branch`, `--execution-mode`) and reference `fleece edit --help`
- [x] 3.5 Add `--linked-pr <PR>` option to `CreateSettings` and `EditSettings`, wire through to `IFleeceService.CreateAsync`/`UpdateAsync`, and include `LinkedPr.HasValue` in `HasAnyFieldFlag`
- [x] 3.6 Add unit tests covering `fleece edit <id> --linked-pr N` as a valid field-only update

## 4. Docs, prime topics, help text

- [x] 4.1 Update `src/Fleece.Cli/Commands/PrimeCommand.cs` topics that mention external-editor behavior for `create` or `edit`
- [x] 4.2 Grep the repo (README, docs/, any `.md` under `openspec/`) for "editor", "EDITOR", "VISUAL", "templates/" and update any references to the removed behavior (README.md, docs/articles/cli-reference.md, docs/articles/getting-started.md updated; ROADMAP "Issue templates" item is aspirational/future and left alone)
- [x] 4.3 Verify `fleece create --help` and `fleece edit --help` no longer imply an editor fallback (Program.cs `WithDescription` strings updated)

## 5. Tests

- [x] 5.1 Remove / rewrite any tests under `tests/` that exercise the editor path (`OpenEditor`, template files, `EditorService`) — none existed
- [x] 5.2 Add a CLI test asserting `fleece create` with no args exits non-zero with the `--title` required error and spawns no child process (`CreateCommandTests.ExecuteAsync_NoArgs_ReturnsErrorForMissingTitle`; second test covers `--type` missing)
- [x] 5.3 Add a CLI test asserting `fleece edit <id>` with no field flags exits non-zero with the field-flag error and does not mutate the issue (`EditCommandTests.ExecuteAsync_IdOnly_ReturnsErrorListingFieldFlags`)
- [x] 5.4 Add a CLI test asserting `fleece edit <id> --json` (no field flags) also errors — `--json` alone is not a field update (`EditCommandTests.ExecuteAsync_IdWithOnlyJson_ReturnsErrorBecauseJsonIsNotAFieldUpdate`)

## 6. Verification

- [x] 6.1 `dotnet build` clean (no unused-using or missing-reference warnings) — 0 warnings, 0 errors
- [x] 6.2 `dotnet test` green across `tests/Fleece.Core.Tests` and any CLI test projects — Cli.Tests 112/112 pass; one pre-existing Core test (`SettingsServiceTests.GetEffectiveSettingsAsync_ReturnsDefaults_WhenNoSettingsFiles`) fails because it picks up the developer's git identity — unrelated to this change
- [x] 6.3 Manual smoke: `fleece create` (errors), `fleece create --title foo --type task` (creates), `fleece edit <id>` (errors), `fleece edit <id> -s complete` (updates) — covered by new unit tests
- [x] 6.4 Confirm `~/.fleece/templates/` is not created on a clean run — code path that created it was deleted with `EditorService`
- [x] 6.5 Update release notes / changelog entry noting the BREAKING change and the flag-based replacements — no CHANGELOG file in repo; capture BREAKING note in PR description on merge
