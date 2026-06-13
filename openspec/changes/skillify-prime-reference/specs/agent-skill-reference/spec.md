## ADDED Requirements

### Requirement: Install provisions the Fleece reference skill

`fleece install` SHALL write an installable Claude skill into the project under `.claude/skills/fleece/`. It SHALL write `SKILL.md` containing the workflow overview and an index of available topics, and SHALL write one `references/<topic>.md` file for each of the nine reference topics: `hierarchy`, `commands`, `statuses`, `sync`, `json`, `next`, `tree`, `github`, and `v4-migration`. The skill content SHALL be sourced from an embedded markdown resource compiled into the CLI.

#### Scenario: Install writes the skill and its references
- **WHEN** `fleece install` runs in a project directory
- **THEN** `.claude/skills/fleece/SKILL.md` exists and contains the workflow overview and a topic index
- **AND** `.claude/skills/fleece/references/` contains a file for each of `hierarchy`, `commands`, `statuses`, `sync`, `json`, `next`, `tree`, `github`, and `v4-migration`

### Requirement: SKILL.md carries a discoverable description

The generated `SKILL.md` SHALL include frontmatter with a `name` and a `description` that identifies the skill as the reference for using Fleece (commands, hierarchy, statuses, workflow, and GitHub round-trip), so a pull-based agent can match it by relevance.

#### Scenario: SKILL.md description matches Fleece reference intent
- **WHEN** `fleece install` writes `.claude/skills/fleece/SKILL.md`
- **THEN** the file contains frontmatter with a `name` and a `description`
- **AND** the description identifies the skill as Fleece command/workflow reference

### Requirement: Skill files are overwritten wholesale on re-install

`fleece install` SHALL overwrite the skill's `SKILL.md` and `references/*.md` files wholesale on each run so that reference content stays current with the installed Fleece version. Each generated file SHALL carry a header indicating it is managed by `fleece install` and that manual edits will be overwritten.

#### Scenario: Re-install refreshes managed skill content
- **WHEN** `fleece install` runs in a project where `.claude/skills/fleece/SKILL.md` already exists with stale content
- **THEN** the file is rewritten with the current generated content
- **AND** the file contains a header indicating it is managed by `fleece install`

### Requirement: CLAUDE.md memory block states philosophy and points at the skill

`fleece install` SHALL write (or refresh in place, between its marker comments) a CLAUDE.md memory block that states the Fleece philosophy — issues are branch-local working memory that must be resolved, promoted, or sealed before a PR merges — and that conveys the decision rule that work blocking the current PR belongs in Fleece while non-blocking follow-ups, new features, and otherwise durable work belong in GitHub Issues. The block SHALL point the reader at the installed `fleece` skill for commands and workflow detail, and SHALL NOT duplicate the skill's detailed reference content.

#### Scenario: Memory block conveys philosophy and skill pointer
- **WHEN** `fleece install` writes the CLAUDE.md memory block
- **THEN** the block states that Fleece issues are branch-local working memory cleared before a PR merges
- **AND** conveys that non-blocking follow-ups and durable work go to GitHub Issues rather than Fleece
- **AND** points the reader at the installed `fleece` skill for commands and workflow

#### Scenario: Re-install refreshes the block in place
- **WHEN** `fleece install` runs in a project whose CLAUDE.md already contains the fleece-managed memory block
- **THEN** the block is replaced in place between its marker comments without duplicating it or clobbering surrounding user content
