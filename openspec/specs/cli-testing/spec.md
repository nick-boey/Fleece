# cli-testing

## Purpose

Codifies end-to-end coverage expectations for the Fleece CLI. Every command in `CliComposition.Commands` must be exercised by at least one scenario test that invokes the real `CliApp.RunAsync` entry point. Fast-suite scenarios run in-process against a `MockFileSystem` and `TestConsole`; git-dependent commands are covered by a separate real-filesystem integration suite.

## Requirements

### Requirement: Every CLI command SHALL have at least one end-to-end scenario test

Every entry in `CliComposition.Commands` SHALL be exercised by at least one test in the CLI E2E test suite that invokes the real `CliApp.RunAsync` entry point with representative arguments and asserts the command's observable effects.

#### Scenario: New command without a scenario fails the meta-test
- **WHEN** a developer adds a new entry to `CliComposition.Commands` and does not add a corresponding scenario
- **THEN** the E2E suite's meta-test fails with a message naming the uncovered command
- **AND** the CI build fails

#### Scenario: Existing command covered
- **WHEN** `list`, `create`, `edit`, `delete`, `show`, `search`, `clean`, `config`, `merge`, `commit`, `dependency`, `move`, `next`, `validate`, `diff`, `migrate`, `install`, `prime`, or any status alias (`open`, `progress`, `review`, `complete`, `archived`, `closed`) is invoked in a test scenario
- **THEN** the suite asserts both exit code and at least one of (filesystem state, stdout content)

### Requirement: In-process scenarios SHALL run against a mock filesystem and test console

Fast-suite scenarios SHALL invoke `CliApp.RunAsync(args, basePath, IFileSystem, IAnsiConsole)` with a `MockFileSystem` and a `TestConsole` supplied by the test fixture. Scenarios SHALL NOT touch the real filesystem or the process-global console.

#### Scenario: Scenario uses mock filesystem
- **WHEN** a scenario calls `RunAsync("create", "-t", "Bug", "-y", "bug")`
- **THEN** the resulting `.fleece/issues_*.jsonl` entry is readable from the injected `MockFileSystem`
- **AND** no file is created on the real disk

#### Scenario: Scenario captures stdout through TestConsole
- **WHEN** a scenario asserts on command output
- **THEN** assertions read from `TestConsole.Output`
- **AND** no ANSI escape codes leak into the real process stdout during the test run

### Requirement: Scenario tests SHALL run safely in parallel

The in-process E2E suite SHALL be safe to execute with NUnit parallelism enabled. Tests SHALL NOT share mutable state via static fields, the global `AnsiConsole.Console`, or the real filesystem.

#### Scenario: Two parallel scenarios produce independent results
- **WHEN** two scenarios run concurrently on separate threads
- **THEN** each sees only the output and filesystem state produced by its own command invocations
- **AND** neither observes artifacts from the other

### Requirement: Git-dependent commands SHALL be covered by a separate real-filesystem integration suite

Commands that shell out to git (`commit`, `merge` as-in git-merge flows, `sync`) SHALL be exercised by an integration test suite that runs against a real temp directory with a real git repository initialised per test.

#### Scenario: Integration suite initialises real git repo
- **WHEN** an integration test starts
- **THEN** its setup creates a temp directory, runs `git init`, and sets `user.name` / `user.email` for that repo
- **AND** the teardown deletes the temp directory

#### Scenario: Integration suite is non-parallelisable
- **WHEN** the integration suite executes
- **THEN** its fixtures are marked `[NonParallelizable]` so concurrent git processes do not collide

### Requirement: Human-readable command output SHALL be covered by approved golden snapshots

Stdout from commands that emit human-readable output (e.g. `list` without `--json`, `show`, `--help`) SHALL be asserted via Verify snapshots committed to the repository so UX regressions surface as reviewable diffs in pull requests.

#### Scenario: Output change produces snapshot diff
- **WHEN** a developer modifies the output format of `list`
- **THEN** the corresponding `*.verified.txt` snapshot file diffs
- **AND** the PR review must approve the snapshot diff before merge

#### Scenario: JSON output uses structural assertions not snapshots
- **WHEN** a command is invoked with `--json`
- **THEN** the test parses the JSON and asserts on its structure
- **AND** the test does NOT rely on a Verify snapshot for the JSON body

### Requirement: Program.cs SHALL be a thin shim over CliApp.RunAsync

The CLI entry point (`Program.cs`) SHALL contain no logic beyond delegating to `CliApp.RunAsync(args)` so the entirety of CLI composition and execution is exercisable from tests without subprocess invocation or reflection.

#### Scenario: Program.cs body is minimal
- **WHEN** a reviewer inspects `src/Fleece.Cli/Program.cs`
- **THEN** its only executable line calls `CliApp.RunAsync(args)` and returns its result
