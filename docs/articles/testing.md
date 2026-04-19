# Testing

Fleece ships four test projects, each with a distinct scope and speed profile. Every layer of the code — from `Fleece.Core` services through CLI command wiring — has coverage designed to catch a specific class of regression.

## Test Projects

| Project | Scope | Filesystem | Console | Parallel | Typical runtime |
|---------|-------|------------|---------|----------|-----------------|
| `tests/Fleece.Core.Tests` | Unit tests for `Fleece.Core` services and models. | `MockFileSystem` or real, per test. | N/A | Yes | <2s |
| `tests/Fleece.Cli.Tests` | DI composition + command-resolution checks. Ensures every `CliComposition.Commands` entry constructs from DI. | Real temp dir. | N/A | Yes | <1s |
| `tests/Fleece.Cli.E2E.Tests` | In-process CLI scenarios driving `CliApp.RunAsync` with injected seams. Golden stdout via Verify. | `MockFileSystem`. | `TestConsole`. | Yes | ~2s |
| `tests/Fleece.Cli.Integration.Tests` | Real-disk + real-git scenarios for `commit`, `merge`. | Real temp dir + `git init` per fixture. | Process stdout. | No (`[NonParallelizable]`) | ~1s |

The separation is deliberate. Core tests cover business logic. Cli.Tests covers DI wiring. E2E.Tests runs the full `CommandApp` path — argument parsing, interceptor wiring, command execution, filesystem writes, stdout format — without shelling out. Integration.Tests exercises the one class of regression the mock suite cannot: real git subprocess behaviour.

## The Seams That Make This Possible

Three entry-point changes make the CLI fully testable in-process:

1. **`IFileSystem` abstraction in `Fleece.Core`.** All file I/O in services flows through `System.IO.Abstractions.IFileSystem`, injected via DI. Tests swap in `MockFileSystem` from `Testably.Abstractions.Testing` for deterministic, disk-free behaviour.

2. **`IAnsiConsole` injection in `Fleece.Cli`.** Every command constructor takes `Spectre.Console.IAnsiConsole`. No command calls `AnsiConsole.*` statics. Tests inject `Spectre.Console.Testing.TestConsole` and assert on `TestConsole.Output`. The only direct reference to `AnsiConsole.Console` is the default registration in `CliComposition.BuildServices`.

3. **`CliApp.RunAsync(args, basePath?, fileSystem?, console?)`.** `Program.cs` is a one-line shim. Tests call `CliApp.RunAsync` directly, passing a base path, a `MockFileSystem`, and a `TestConsole`. The whole command pipeline — including the `AutoMergeInterceptor` — runs against those seams.

## Writing E2E Scenarios

E2E scenarios inherit `CliScenarioTestBase`:

```csharp
[TestFixture]
[Category("create")]
[Category("show")]
public class CrudScenarios : CliScenarioTestBase
{
    [Test]
    public async Task Create_then_show_round_trips()
    {
        (await RunAsync("create", "-t", "Bug X", "-y", "bug")).Should().Be(0);
        var id = LoadIssues().Single().Id;

        (await RunAsync("show", id, "--json")).Should().Be(0);
        ParseJsonOutput().GetProperty("issue").GetProperty("id").GetString().Should().Be(id);
    }
}
```

The base fixture gives each test:

- `Fs` — a fresh `MockFileSystem` with `/project` as the base path.
- `Console` — a fresh `TestConsole` that captures Spectre output.
- `RunAsync(params string[] args)` — invokes `CliApp.RunAsync` with `Fs` and `Console` injected.
- `LoadIssues()` / `LoadTombstones()` — deserialise JSONL entries from the mock filesystem.
- `Stdout` — captures raw `Console.WriteLine` calls (used by JSON-output code paths that bypass Spectre).
- `ParseJsonOutput()` — parses `Stdout` as JSON for structural assertions.
- `AssertStdoutSnapshot()` — a Verify helper for human-readable snapshots.

### Assertion strategy

- **Exit code** — always assert.
- **Filesystem state** — assert via `LoadIssues()` / `LoadTombstones()`. Structural, never snapshotted.
- **`--json` output** — parse with `ParseJsonOutput()` and assert on structure. Never snapshotted (fragile to key ordering).
- **Human-readable output** — assert on substrings via `Console.Output.Should().Contain(...)`, or snapshot with Verify for stable output.

## Command Coverage Meta-Test

`CommandCoverageMetaTest.Every_command_in_CliComposition_has_at_least_one_scenario` enforces a simple rule: **every entry in `CliComposition.Commands` must appear in at least one `[Category("command-name")]` attribute on a scenario fixture.**

Adding a new CLI command to `CliComposition.Commands` without a corresponding scenario fails this meta-test, which fails CI. The meta-test names the uncovered command so the fix is obvious.

## Verify Snapshots

`tests/Fleece.Cli.E2E.Tests/Snapshots/` holds committed `*.verified.txt` files produced by [`Verify.NUnit`](https://github.com/VerifyTests/Verify). Snapshots cover stable human-readable stdout only (not `--json`, not anything containing timestamps or random IDs unless scrubbed).

### Regenerating after an intentional output change

1. Delete the relevant `*.verified.txt` file.
2. Run `dotnet test tests/Fleece.Cli.E2E.Tests`. The test fails and Verify produces `*.received.txt` next to the missing verified file.
3. Compare the two by hand or with the Verify CLI / IDE tooling.
4. If the new output is correct, rename `*.received.txt` → `*.verified.txt`.
5. Commit the new snapshot.

### Reviewing snapshot diffs in a PR

Treat `.verified.txt` diffs the same as any user-facing UX change. A reviewer should read the diff and confirm the new output is intended. Any churn signals a change in what users see.

## Integration Suite Conventions

`tests/Fleece.Cli.Integration.Tests` uses real disk + real git because the mock filesystem cannot simulate `git init`, `git commit`, or conflict markers. Every fixture:

- Inherits `GitTempRepoFixture`.
- Creates a per-test temp directory with `git init --initial-branch=main`.
- Sets `user.name`, `user.email`, disables `commit.gpgsign`.
- Tears down the temp directory on `[TearDown]`.
- Is marked `[NonParallelizable]` — concurrent git processes in the same cwd race.

Because git subprocesses are slow and non-parallelisable, keep this suite small. Prefer the mock-FS E2E suite for everything that doesn't genuinely need git.

## Running Tests

```bash
# Everything
dotnet test

# One project
dotnet test tests/Fleece.Cli.E2E.Tests

# One scenario class
dotnet test tests/Fleece.Cli.E2E.Tests --filter "FullyQualifiedName~CrudScenarios"

# One test
dotnet test tests/Fleece.Cli.E2E.Tests --filter "FullyQualifiedName~Create_then_show_round_trips"
```

CI (`.github/workflows/ci.yml`) runs each project as a separate step so CI logs show per-suite pass/fail.

## When to Add Which Kind of Test

| Change | Add test to |
|--------|-------------|
| New Core service method or bug fix in Core | `Fleece.Core.Tests` |
| New command registered in `CliComposition.Commands` | `Fleece.Cli.E2E.Tests` (meta-test will fail otherwise) |
| New command flag with user-visible output change | `Fleece.Cli.E2E.Tests` — scenario + Verify snapshot if human-readable |
| Change to `commit`, `merge`, or anything that shells out to git | `Fleece.Cli.Integration.Tests` |
| Change to DI composition | `Fleece.Cli.Tests` |

Keep scenarios small and narrative. One assertion on exit code, one on filesystem state or stdout. Fixtures should be deterministic — no real disk, no real time, no shared mutable state.
