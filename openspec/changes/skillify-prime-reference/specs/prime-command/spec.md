## MODIFIED Requirements

### Requirement: Silent exit when Fleece is not initialised

The `fleece prime` command SHALL produce no output and exit successfully when the current working directory does not contain a `.fleece/` directory.

#### Scenario: No .fleece directory
- **WHEN** `fleece prime` is invoked in a directory that does not contain a `.fleece/` folder
- **THEN** the command writes no output to stdout and returns exit code 0

### Requirement: Overview output when Fleece is initialised

When the current working directory contains a `.fleece/` directory, `fleece prime` SHALL emit **only the dynamic active-issue signal**, not static reference content. When one or more issues are active (`Open`/`Progress`/`Review`), the output SHALL state the current count of active issues, that issues must be resolved (`Complete`/`Closed`), `promote`d, or `seal`ed before a PR merges, that the CI gate fails a PR while live issues remain, and SHALL point the reader at the installed `fleece` skill for commands and workflow. When zero issues are active, `fleece prime` SHALL produce no output and return exit code 0.

#### Scenario: Active issues present emits the dynamic signal
- **WHEN** `fleece prime` runs in an initialised repo with one or more active issues
- **THEN** output includes the count of active issues
- **AND** states issues must be resolved, promoted, or sealed before a PR merges
- **AND** points the reader at the `fleece` skill for commands and workflow
- **AND** does not include the static workflow/types/statuses reference content

#### Scenario: Clean branch emits nothing
- **WHEN** `fleece prime` runs in an initialised repo with zero active issues
- **THEN** the command writes no output to stdout and returns exit code 0

## REMOVED Requirements

### Requirement: OpenSpec integration section when openspec/ is present
**Reason**: `fleece prime` no longer emits any static reference content (including OpenSpec guidance); its output is limited to the dynamic active-issue signal. OpenSpec dependency visualisation remains available via `fleece openspec dependencies`.
**Migration**: None required — the legacy per-change linking guidance was already removed; prime simply no longer renders openspec-conditional onboarding text.

### Requirement: Unknown topic handling is unchanged
**Reason**: The `[topic]` argument is removed from `fleece prime`, so there is no unknown-topic path. The nine topics now live in the installed `fleece` skill.
**Migration**: Invoke the `fleece` skill (installed at `.claude/skills/fleece/`) for the content formerly served by `fleece prime <topic>`.

### Requirement: Dedicated github topic
**Reason**: `fleece prime github` is removed; the GitHub round-trip guidance moves to the skill at `.claude/skills/fleece/references/github.md`.
**Migration**: Read `references/github.md` in the installed `fleece` skill, or run `fleece auth`/`promote`/`absorb -h` for usage.

### Requirement: Dedicated v4-migration topic
**Reason**: `fleece prime v4-migration` is removed; the migration guidance moves to the skill at `.claude/skills/fleece/references/v4-migration.md`.
**Migration**: Read `references/v4-migration.md` in the installed `fleece` skill.
