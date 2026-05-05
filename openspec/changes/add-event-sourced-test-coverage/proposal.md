## Why

The event-sourced storage change (PR #124) shipped with integration tests covering lifecycle, squash, and project scenarios, but several critical paths remain untested. The commit-order tiebreak — designed for the "last-commit-wins" semantic when two branches edit the same issue property — is wired to `NullEventGitContext` in DI and was never given a real git-backed implementation. No test exercises a real `git merge` of two branches that edited the same issues, and the rich legacy fixtures in `tests/examples/` are unused. These gaps risk undiscovered bugs in the ordering and migration logic, especially as external consumers (Homespun) adopt the new storage layer.

## What Changes

- Implement a real git-backed `IEventGitContext` (referenced as a TODO in the interface since PR 1) and register it in DI, making commit-order tiebreaks operational.
- Add integration tests using real `git merge` of two branches that edit the same issues, verifying correct property resolution after merge.
- Add integration tests for the `fleece migrate` pipeline using the real fixtures in `tests/examples/diff-issues/` and `tests/examples/nested-issues/`.
- Add integration tests that verify commit-order tiebreak behavior (later-committed events win over earlier-committed ones when no follows relationship exists).
- Add an integration test for the complete "two machines, one branch, merge" scenario using actual git merge rather than manually copying files.

## Capabilities

### New Capabilities

- `event-sourced-integration-tests`: additional integration and E2E tests for the event-sourced storage layer covering real git merge scenarios, migration from real legacy fixtures, and commit-order tiebreak behavior.

### Modified Capabilities

None. This change adds test coverage and fixes a DI wiring gap; no existing specification requirements change.

## Impact

**Code:**
- `src/Fleece.Core/EventSourcing/Services/` — new `GitEventContext` class implementing `IEventGitContext` with git-log-based commit-ordinal lookups.
- `src/Fleece.Core/Extensions/ServiceCollectionExtensions.cs` — register `GitEventContext` instead of `NullEventGitContext.Instance` when a git repo is detected.
- `tests/Fleece.Cli.Integration.Tests/EventSourcedLifecycleTests.cs` — new test methods for merge scenarios.
- `tests/Fleece.Cli.E2E.Tests/Scenarios/MigrateScenarios.cs` — new test methods using real fixtures.

**Dependencies:** Depends on the `event-sourced-issues` change (PR #124) being merged. All new tests target behavior already specified in `event-sourced-issues/specs/`.

**Out of scope:** Implementing the `GitEventContext` for anything beyond commit-ordinal lookups. The existing `NullEventGitContext` fallback path (when not in a git repo) is preserved.
