## Why

Commit `b32230f` shipped a CLI where every command failed at startup, and the test suite reported all green. The recently-added `CliCompositionTests` covers dependency-resolution regressions, but nothing exercises the full CommandApp path: argument parsing, interceptor wiring, command execution, filesystem writes, stdout format. Without that coverage, future refactors carry the same blind spot — any wiring-level regression or behavioural change in a command can merge silently.

A scenario-driven E2E suite run in-process against `CliApp.RunAsync(args, ...)` with a mock filesystem and test console gives fast, parallelisable coverage of real user flows. A separate real-filesystem integration suite covers git-dependent commands (`commit`, `merge`, `sync`) that cannot run against a mock.

Shipping this change requires the two preceding refactors (`abstract-filesystem-access`, `abstract-ansi-console`) to be merged first.

## What Changes

- Extract the body of `Program.cs` into `CliApp.BuildApp(IServiceProvider)` and `CliApp.RunAsync(string[] args, string? basePath, IFileSystem? fs, IAnsiConsole? console)`. `Program.cs` becomes a three-line shim that delegates to `CliApp.RunAsync(args)`.
- Add a new `cli-testing` capability with specs defining E2E coverage expectations so future commands are held to the same bar.
- Add `Verify.NUnit` to enable golden-output snapshotting of human-readable stdout.
- Add `tests/Fleece.Cli.E2E.Tests` project (or a namespaced folder under the existing CLI test project — pick one during implementation) containing:
  - `CliScenarioTestBase` — NUnit fixture that builds a fresh `MockFileSystem`, `TestConsole`, and service provider per test, exposes `RunAsync(params string[] args)`, and gives helpers for reading `.fleece/*.jsonl` entries out of the mock FS.
  - Scenario test classes organised by capability:
    - `CrudScenarios` — create/show/list/edit/delete happy paths.
    - `StatusFlowScenarios` — open→progress→review→complete transitions.
    - `HierarchyScenarios` — parent/child creation, `list --tree`, `list --next`.
    - `DependencyScenarios` — add/remove dependencies, cycle detection.
    - `SearchScenarios` — text search across titles/descriptions.
    - `CleanScenarios` — delete + clean → tombstones + ref stripping.
    - `ConfigScenarios` — config set/get/list round trips.
    - `ErrorPathScenarios` — missing flags, bad IDs, invalid types.
- Add `tests/Fleece.Cli.Integration.Tests` (real filesystem + real git repo in temp dir) covering git-dependent flows: `commit`, `merge`, `sync`, plus smoke coverage of the full CLI binary via `CliApp.RunAsync` against a real disk.
- Extend `.github/workflows/ci.yml` to run both test projects (the existing smoke step from the DI-registration fix stays).

## Capabilities

### New Capabilities
- `cli-testing`: codifies what level of E2E coverage every CLI command must have, how tests are structured, and which flows use mock vs real filesystem.

### Modified Capabilities
<!-- none — issue-authoring stays as-is; this change adds the cli-testing capability spec -->

## Impact

- **Code**: new `src/Fleece.Cli/CliApp.cs`; `src/Fleece.Cli/Program.cs` shrinks to a shim; new test projects (or namespaced folders).
- **Dependencies**: `Verify.NUnit` in the E2E test project. Test-only; no production change.
- **CI**: workflow gains two test invocations (mock-FS suite + real-FS integration suite) plus the existing smoke step.
- **Coverage target**: every CLI command in `CliComposition.Commands` must have at least one scenario in the E2E suite (enforced by a meta-test that fails when a command has no matching scenario).
- **Consumers**: none external — all changes are in the CLI layer and test projects.
