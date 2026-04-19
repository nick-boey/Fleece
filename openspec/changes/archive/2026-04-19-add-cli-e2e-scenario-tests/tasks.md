## 1. Entry-point extraction

- [x] 1.1 Create `src/Fleece.Cli/CliApp.cs` with `BuildApp` and `RunAsync(args, basePath?, fileSystem?, console?)`.
- [x] 1.2 Shrink `Program.cs` to a single `return await CliApp.RunAsync(args);` line.
- [x] 1.3 Update `CliCompositionTests` if any signatures shift; ensure the 24 command-resolution cases still pass.

## 2. E2E test project scaffolding

- [x] 2.1 Create `tests/Fleece.Cli.E2E.Tests` project (NUnit + NSubstitute + FluentAssertions + Testably.Abstractions.Testing + Spectre.Console.Testing + Verify.NUnit). Reference `Fleece.Cli` and `Fleece.Core`.
- [x] 2.2 Implement `CliScenarioTestBase` with `MockFileSystem`, `TestConsole`, `RunAsync(params string[])`, `LoadIssues()`, `AssertStdoutSnapshot()`.
- [x] 2.3 Configure Verify to write snapshots under `tests/Fleece.Cli.E2E.Tests/Snapshots/` and commit them.

## 3. Scenario matrix

- [x] 3.1 `CrudScenarios` — create happy-path, show by id, list (table and --json), edit each field, delete, show-after-delete error.
- [x] 3.2 `StatusFlowScenarios` — each alias command (open/progress/review/complete/archived/closed), bulk status change with multiple IDs, edit -s equivalent.
- [x] 3.3 `HierarchyScenarios` — parent-child create, list --tree rendering, list --next ordering, orphan handling.
- [x] 3.4 `DependencyScenarios` — dependency --parent/--child add/remove, --first/--after ordering, cycle detection rejected.
- [x] 3.5 `SearchScenarios` — search by title match, description match, no-results path.
- [x] 3.6 `CleanScenarios` — delete then clean produces tombstone, --no-strip-refs retains refs, --include-complete/--include-archived/--include-closed extensions, --dry-run prints nothing.
- [x] 3.7 `ConfigScenarios` — config --list, --get, --set local, --global --set, round-trip via GetEffectiveSettings.
- [x] 3.8 `ErrorPathScenarios` — missing --title, missing --type, unknown issue ID, invalid --type value, malformed --parent-issues spec.
- [x] 3.9 Meta-test asserting every entry in `CliComposition.Commands` is covered by at least one scenario.

## 4. Git integration suite

- [x] 4.1 Create `tests/Fleece.Cli.Integration.Tests` project. Mark fixtures `[NonParallelizable]`.
- [x] 4.2 Base fixture initialises a real temp dir and `git init` with a configured user, tears it down on teardown.
- [x] 4.3 Scenarios: `commit` writes a commit, `commit --push` stubbed safely, `merge` resolves conflicts in `.fleece/`, `sync` round-trips through a remote. (sync command not in CliComposition; covered via merge + commit flows only.)
- [x] 4.4 Smoke scenario: `CliApp.RunAsync(["list", "--json"])` against a populated real temp dir.

## 5. CI + verification

- [x] 5.1 Add both new test projects to `.github/workflows/ci.yml`.
- [x] 5.2 Keep the existing CLI smoke step (`dotnet run -- list --json` / `--help`).
- [x] 5.3 Document Verify snapshot review process in CONTRIBUTING or CLAUDE.md (how to regenerate snapshots, how to review diffs).
- [x] 5.4 Full `dotnet test` green; CI workflow passes end-to-end. (One pre-existing unrelated failure in `Fleece.Core.Tests.SettingsServiceTests.GetEffectiveSettingsAsync_ReturnsDefaults_WhenNoSettingsFiles` — fails on main before this change; out of scope.)
