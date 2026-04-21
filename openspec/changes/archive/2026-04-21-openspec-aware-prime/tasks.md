## 1. Author the OpenSpec integration content

- [x] 1.1 Draft the `OpenSpecContent` string literal in `src/Fleece.Cli/Commands/PrimeCommand.cs` covering: the `openspec={change-name}` keyed-tag convention (including the "multiple allowed but discouraged" caveat), the single-change decision tree (branch `+<id>` path, open-unlinked fallback, ask-when-ambiguous, new-issue-as-last-resort), the multi-change hierarchy rules (one issue per change, flat fan-out default, intermediate parents only when necessary, lex-order and execution-mode usage), and the "never create issues per task or per phase" rule.
- [x] 1.2 Include a compact ASCII decision tree for the single-change flow in the content.
- [x] 1.3 Review the content for token size (target ~600 tokens); trim redundancy.

## 2. Wire the topic and detection into `PrimeCommand`

- [x] 2.1 Add `["openspec"] = OpenSpecContent` to the `Topics` dictionary in `PrimeCommand.cs`.
- [x] 2.2 Add `openspec` to the list of topics printed at the bottom of `OverviewContent` (the "## Detailed Help Topics" section).
- [x] 2.3 In `Execute`, after the existing `.fleece/` existence check, compute `openspecDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "openspec")` and a boolean `hasOpenSpec = Directory.Exists(openspecDirectoryPath)`.
- [x] 2.4 In the no-topic branch, when `hasOpenSpec` is true, write `OverviewContent` followed by a blank line and `OpenSpecContent`; otherwise write `OverviewContent` unchanged.
- [x] 2.5 Verify the unknown-topic path still prints the available-topics list and that `openspec` now appears in that list (it will automatically, since `Topics.Keys` is the source).

## 3. Unit tests

- [x] 3.1 Locate the existing `PrimeCommand` tests (or the nearest CLI command test host) under `tests/`; if no test exists for `PrimeCommand`, create `tests/Fleece.Cli.Tests/Commands/PrimeCommandTests.cs` following the pattern used for other CLI command tests.
- [x] 3.2 Test: with no `.fleece/` directory and no topic, output is empty and exit code is 0 (unchanged behaviour).
- [x] 3.3 Test: with `.fleece/` present and no `openspec/`, no-topic output equals the unchanged `OverviewContent` and does NOT contain the OpenSpec Integration heading string.
- [x] 3.4 Test: with `.fleece/` and `openspec/` both present, no-topic output contains both the base overview markers and the OpenSpec Integration heading, plus assertions for the key content phrases (tag convention `openspec=`, branch `+<id>` reference, "one issue per change", "never create issues per task").
- [x] 3.5 Test: `fleece prime openspec` emits `OpenSpecContent` when `.fleece/` is present, regardless of whether `openspec/` exists.
- [x] 3.6 Test: unknown topic prints the unknown-topic message and `openspec` appears in the available topics list in that error output.
- [x] 3.7 Tests that depend on CWD use a temp directory fixture (create + `Directory.SetCurrentDirectory`) and restore CWD afterwards so tests remain isolated and parallel-safe.

## 4. Manual verification

- [x] 4.1 Build the solution: `dotnet build`.
- [x] 4.2 Run all tests: `dotnet test`.
- [x] 4.3 In this repo (which has both `.fleece/` and `openspec/`), run `fleece prime` and confirm the OpenSpec Integration section appears at the end.
- [x] 4.4 Run `fleece prime openspec` and confirm the same integration content prints standalone.
- [x] 4.5 In a temp directory that has `.fleece/` but no `openspec/`, run `fleece prime` and confirm the integration section is absent.
- [x] 4.6 Run `fleece prime not-a-topic` and confirm `openspec` appears in the available-topics list.

## 5. Follow-up hygiene

- [x] 5.1 Link the Fleece issue tracking this change to the change via the `openspec=openspec-aware-prime` tag (per the new convention this change introduces) before opening the PR.
- [x] 5.2 Move the Fleece issue to `review` with `--linked-pr <pr>` when the PR is opened.
