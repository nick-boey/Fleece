## Context

The two preceding refactors make this change possible:

- `abstract-filesystem-access` gives tests a `MockFileSystem` they can inject.
- `abstract-ansi-console` gives each test a dedicated `TestConsole`.

With both seams in place, a test can call `CliApp.RunAsync(args, tmpPath, mockFs, testConsole)` and assert on:

- The returned exit code.
- The contents of `mockFs.GetFile(".fleece/issues_*.jsonl")` (structural).
- `testConsole.Output` either via structural parsing (for `--json` commands) or a Verify snapshot (for human-readable output).

`Program.cs` is a top-level-statements file today. Those compile to an `internal` `Program.<Main>` that can only be invoked via `InternalsVisibleTo`. Extracting into `CliApp` is cleaner, makes the entry-point testable without reflection, and matches the pattern already established by `CliComposition`.

## Goals / Non-Goals

**Goals:**
- Every command in `CliComposition.Commands` has ≥1 happy-path scenario test.
- Tests run in parallel with no shared mutable state.
- Assertions cover both filesystem state and stdout output.
- `Program.cs` ceases to contain untested wiring.
- Git-dependent commands have coverage via a real-filesystem integration suite.

**Non-Goals:**
- Subprocess-based tests against the compiled binary (the existing CI smoke step covers that class of regression).
- Approval tests for every JSON output (structural assertions are preferred for structured data).
- Performance benchmarks.
- Coverage of internal helpers that are already unit-tested in `Fleece.Core.Tests`.

## Decisions

**Entry-point extraction.**

```csharp
// src/Fleece.Cli/CliApp.cs
public static class CliApp
{
    public static CommandApp BuildApp(IServiceProvider serviceProvider) { ... }

    public static Task<int> RunAsync(
        string[] args,
        string? basePath = null,
        IFileSystem? fileSystem = null,
        IAnsiConsole? console = null)
    {
        var services = CliComposition.BuildServices(basePath, fileSystem);
        if (console is not null) services.AddSingleton(console);
        var registrar = new TypeRegistrar(services);
        var app = BuildApp(registrar.BuildServiceProvider());
        return app.RunAsync(args);
    }
}

// src/Fleece.Cli/Program.cs (now trivial)
return await Fleece.Cli.CliApp.RunAsync(args);
```

**Fixture shape.**

```csharp
public abstract class CliScenarioTestBase
{
    protected MockFileSystem Fs { get; private set; } = null!;
    protected TestConsole Console { get; private set; } = null!;

    [SetUp] public void BaseSetUp() { Fs = new MockFileSystem(); Console = new TestConsole(); }

    protected Task<int> RunAsync(params string[] args)
        => CliApp.RunAsync(args, basePath: "/project", Fs, Console);

    protected IReadOnlyList<Issue> LoadIssues() { ... }   // parses JSONL from Fs
    protected void AssertStdoutSnapshot() => Verify(Console.Output);
}
```

**Scenario style.** Each test is small, narrative, asserts both FS and stdout where relevant:

```csharp
[Test]
public async Task Create_then_list_json_returns_issue()
{
    (await RunAsync("create", "-t", "Bug X", "-y", "bug")).Should().Be(0);
    (await RunAsync("list", "--json")).Should().Be(0);

    LoadIssues().Should().ContainSingle(i => i.Title == "Bug X");
    JsonDocument.Parse(Console.Output).RootElement.GetArrayLength().Should().Be(1);
}
```

**Golden vs structural assertions.**
- `--json` outputs → parse and assert structure (robust to key ordering with a canonicaliser).
- Human-readable outputs (`list` without `--json`, `show`, `--help`) → `Verify(Console.Output)` snapshots.
- FS state → always structural (read JSONL via `Fs.File.ReadAllLines`, deserialise, assert on models).

**Meta-test.** A single test enumerates `CliComposition.Commands` and asserts each command name appears in at least one `[Test]`-annotated method's name or attribute across the E2E assembly. Keeps the coverage bar honest without a full CLI-reflection pass.

**Git integration suite.** `tests/Fleece.Cli.Integration.Tests` uses a real temp directory and a real git repo initialised per test (`git init`, `git config user.*`). Tests here call `CliApp.RunAsync(args, tmpDir)` with `fileSystem = null` so the real filesystem is used. `[NonParallelizable]` at the fixture level because real git processes fight over the working directory.

**Verify config.** `Verify.NUnit` with `UseDirectory("Snapshots")` and `.verified.txt` files committed to source control. Diff-on-failure so PR reviewers see snapshot churn explicitly.

## Risks / Trade-offs

- **Snapshot churn.** Any stdout tweak triggers a Verify diff. Mitigation: only snapshot stable human-readable outputs; avoid snapshotting timestamps, IDs, or anything non-deterministic (scrub via Verify settings).
- **Mock/real divergence.** `MockFileSystem` is faithful but not identical to real disks (edge cases: symlinks, long paths). Mitigation: git integration suite covers the real-disk path for the commands most likely to hit edge cases.
- **Parallel safety regressions.** A future command that reintroduces a static filesystem or console call leaks across tests. Mitigation: post-refactor ripgrep invariants documented in the preceding proposals plus the meta-test above.
- **Test runtime.** ~50 in-process tests against mock FS should run in <2s locally. The integration suite is slower (real git). Acceptable for a CLI of this size.
- **Meta-test brittleness.** Enumerating commands and asserting scenario coverage relies on a naming convention. If it gets noisy, swap for a `[Category("command-name")]` attribute enumerated against `CliComposition.Commands`.
