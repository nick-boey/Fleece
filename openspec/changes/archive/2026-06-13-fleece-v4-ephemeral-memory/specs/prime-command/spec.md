## MODIFIED Requirements

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

## REMOVED Requirements

### Requirement: Dedicated openspec topic
**Reason**: The OpenSpec-per-change linking workflow is removed; OpenSpec is now surfaced via the `fleece openspec dependencies` command, not a prime topic.
**Migration**: Use `fleece openspec dependencies` to view OpenSpec change relationships.

## ADDED Requirements

### Requirement: Dedicated github topic

`fleece prime github` SHALL emit guidance on the GitHub round-trip workflow: how to `promote` one or many Fleece issues into a single GitHub issue, how to `absorb` a GitHub issue (`#<number>`) into Fleece, and how to check credentials with `fleece auth`.

#### Scenario: github topic explains promote and absorb
- **WHEN** `fleece prime github` runs
- **THEN** output explains `promote`, `absorb #<number>`, and `auth`

### Requirement: Dedicated v4-migration topic

`fleece prime v4-migration` SHALL emit instructions for migrating a repository that still holds a legacy durable `.fleece/issues.jsonl` snapshot: how to review the legacy issues and `promote` the long-running ones to GitHub Issues, then `seal` to archive and clear remaining inactive issues.

#### Scenario: v4-migration topic guides legacy migration
- **WHEN** `fleece prime v4-migration` runs
- **THEN** output instructs the agent to promote long-running legacy issues to GitHub Issues and then seal
