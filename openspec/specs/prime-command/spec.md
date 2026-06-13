# prime-command Specification

## Purpose

The `fleece prime` command provides AI agents and humans with concise onboarding content about how to use Fleece in the current repository. It emits an overview of issue workflow, types, statuses, hierarchy, filtering, and JSON output by default, and can surface dedicated topic guidance (for example, OpenSpec integration) when invoked with a topic argument. The command adapts its output based on which directories are present in the current working directory (`.fleece/`, `openspec/`).
## Requirements
### Requirement: Silent exit when Fleece is not initialised

The `fleece prime` command SHALL produce no output and exit successfully when the current working directory does not contain a `.fleece/` directory. This applies regardless of whether an `openspec/` directory is present or a topic argument was provided.

#### Scenario: No .fleece directory with no topic
- **WHEN** `fleece prime` is invoked in a directory that does not contain a `.fleece/` folder
- **THEN** the command writes no output to stdout and returns exit code 0

#### Scenario: No .fleece directory with topic
- **WHEN** `fleece prime openspec` is invoked in a directory that does not contain a `.fleece/` folder
- **THEN** the command writes no output to stdout and returns exit code 0

### Requirement: Overview output when Fleece is initialised

When the current working directory contains a `.fleece/` directory, `fleece prime` (invoked with no topic) SHALL emit onboarding content describing Fleece as **branch-local, ephemeral agent working memory** — not a durable tracker. The output SHALL state that issues must be resolved (`Complete`/`Closed`), `promote`d to a GitHub issue, or `seal`ed before a PR merges, and that the CI gate fails a PR while live issues remain. The output SHALL include the current count of incomplete (active) issues and guidance to commit `.fleece/` changes. It SHALL describe the v4 status set (with `Promoted`, without `Draft`) and type set (without `Idea`).

#### Scenario: Overview reflects the ephemeral model
- **WHEN** `fleece prime` runs in an initialised repo
- **THEN** output describes Fleece as ephemeral branch-local memory
- **AND** states issues must be resolved, promoted, or sealed before a PR merges
- **AND** includes the count of active issues

### Requirement: OpenSpec integration section when openspec/ is present

`fleece prime` SHALL NOT emit the legacy OpenSpec-per-change issue-linking section. Guidance for linking one Fleece issue per OpenSpec change is removed; OpenSpec dependency visualisation is provided by the separate `fleece openspec dependencies` command rather than by prime onboarding text.

#### Scenario: No per-change linking guidance
- **WHEN** `fleece prime` runs in a repo containing an `openspec/` directory
- **THEN** the output contains no instruction to create or link one Fleece issue per OpenSpec change

### Requirement: Unknown topic handling is unchanged

When a topic argument is supplied that is not in the known topics dictionary, `fleece prime` SHALL continue to emit an "Unknown topic" message followed by the list of available topics and return a non-zero exit code. The addition of the `openspec` topic MUST be reflected in the list of available topics printed in this message.

#### Scenario: Unknown topic lists openspec among available topics
- **WHEN** `fleece prime not-a-real-topic` is invoked in a directory containing `.fleece/`
- **THEN** the command writes a message indicating the topic is unknown
- **AND** the printed list of available topics contains `openspec`
- **AND** the command returns a non-zero exit code

### Requirement: Dedicated github topic

`fleece prime github` SHALL emit guidance on the GitHub round-trip workflow: how to `promote` one or many Fleece issues into a single GitHub issue, how to `absorb` a GitHub issue (`#<number>`) into Fleece, and how to check credentials with `fleece auth`.

#### Scenario: github topic explains promote and absorb
- **WHEN** `fleece prime github` runs
- **THEN** output explains `promote`, `absorb #<number>`, and `auth`

### Requirement: Dedicated v4-migration topic

`fleece prime v4-migration` SHALL emit instructions for migrating a repository that still holds a legacy durable `.fleece/issues.jsonl` snapshot. The instructions SHALL direct the user to **first** run `fleece migrate` to convert the durable layout (`.fleece/issues.jsonl` + `.fleece/changes/`) into v4 per-issue logs, because the durable issues are not otherwise visible to `fleece list`. The instructions SHALL then direct the user to review the converted issues (`fleece list --all`), `promote` the long-running ones to GitHub Issues, resolve or close the rest, and `seal` to archive and clear remaining inactive issues.

#### Scenario: v4-migration topic guides conversion before review
- **WHEN** `fleece prime v4-migration` runs
- **THEN** output instructs the user to convert the durable snapshot layout into per-issue logs before listing issues
- **AND** output instructs the user to promote long-running legacy issues to GitHub Issues and then seal

