## ADDED Requirements

### Requirement: A repository SHALL have a single configured durable tracker

Fleece SHALL support a per-repository durable-tracker setting named `tracker` with the values
`github` or `linear`, persisted in the tracked `.fleece/settings.json`. When the setting is absent it
SHALL default to `github`, so a repository that has never chosen a tracker behaves exactly as before
this change. The setting SHALL be readable and writable via `fleece config`, and exactly one tracker
SHALL be active at a time (there is no per-command tracker override).

#### Scenario: Default when unset
- **WHEN** the effective settings are resolved in a repository with no `tracker` value
- **THEN** the active tracker is `github`

#### Scenario: Configured value is honoured
- **WHEN** `.fleece/settings.json` sets `tracker` to `linear`
- **THEN** the active tracker resolves to `linear`

#### Scenario: Set via config
- **WHEN** `fleece config --set tracker=linear` runs
- **THEN** `.fleece/settings.json` records `tracker=linear` and subsequent commands see `linear`

### Requirement: `fleece install` SHALL select and persist the durable tracker

The `fleece install` command SHALL accept `--tracker github|linear`. When the option is supplied it
SHALL persist that value as the repository's `tracker` setting. When the option is omitted and the
session is interactive (stdin is a TTY), install SHALL prompt the user to choose a tracker and persist
the choice. When the option is omitted and the session is non-interactive, install SHALL default the
tracker to `github` without prompting.

#### Scenario: Explicit selection is persisted
- **WHEN** `fleece install --tracker linear` runs
- **THEN** the repository's `tracker` setting is persisted as `linear`

#### Scenario: Non-interactive default
- **WHEN** `fleece install` runs with no `--tracker` and no TTY
- **THEN** the tracker is set to `github` and no prompt is shown

### Requirement: `fleece install` SHALL emit tracker-appropriate skill references and CLAUDE block

For the selected tracker, `fleece install` SHALL write a CLAUDE memory block worded for that tracker
and SHALL install the core skill references plus exactly one tracker-specific reference —
`references/github.md` when the tracker is `github`, `references/linear.md` when the tracker is
`linear` — and a `SKILL.md` whose durable-tracking section names the selected tracker. The
tracker-specific reference for the non-selected tracker SHALL NOT be written.

#### Scenario: Linear install ships the Linear reference
- **WHEN** `fleece install --tracker linear` runs
- **THEN** `.claude/skills/fleece/references/linear.md` is written
- **AND** `.claude/skills/fleece/references/github.md` is not written
- **AND** the CLAUDE memory block and `SKILL.md` name Linear as the durable tracker

#### Scenario: GitHub install ships the GitHub reference
- **WHEN** `fleece install --tracker github` runs
- **THEN** `.claude/skills/fleece/references/github.md` is written
- **AND** `.claude/skills/fleece/references/linear.md` is not written
- **AND** the CLAUDE memory block names GitHub as the durable tracker

### Requirement: Durable-tracker commands SHALL resolve behaviour from the configured tracker

The `promote`, `absorb`, and `auth` commands SHALL resolve their behaviour from the configured
tracker through a single tracker-workflow abstraction that has one implementation per supported
tracker. The GitHub implementation SHALL delegate to the existing `IGitHubService`; the Linear
implementation SHALL perform only local operations and SHALL make no network calls, so its behaviour
is exercised without any GitHub credential or fake GitHub service.

#### Scenario: GitHub tracker delegates to IGitHubService
- **WHEN** a durable-tracker command runs with `tracker=github`
- **THEN** it uses the GitHub-backed workflow that calls `IGitHubService`

#### Scenario: Linear tracker makes no network calls
- **WHEN** a durable-tracker command runs with `tracker=linear`
- **THEN** the command completes using only local state and never calls a GitHub or Linear API

### Requirement: `fleece promote` SHALL emit-then-record when the tracker is Linear

When the active tracker is `linear`, `fleece promote <id> [<id>...]` invoked **without** `--ref` SHALL
change no issue state and SHALL emit the escalation payload — the bundle's root title and task-list
body — for the agent to create the Linear issue via its own tooling, printing the exact `--ref` re-run
command; with `--json` it SHALL emit `{title, body, issueIds}`. Invoked **with** `--ref <ref>`, it
SHALL record the keyed tag `promoted=<ref>` (an arbitrary non-empty string such as a Linear identifier
or URL) on each supplied issue and set each to status `Promoted`. Idempotency SHALL be preserved: an
issue already carrying a `promoted=` tag SHALL be skipped with a warning. No GitHub authentication
check SHALL run in Linear mode.

#### Scenario: Bare promote emits the payload and changes nothing
- **WHEN** `fleece promote a1b2c3 d4e5f6` runs with `tracker=linear` and no `--ref`
- **THEN** the bundle title and task-list body are printed along with the `--ref` re-run command
- **AND** neither issue's status or tags change

#### Scenario: Promote with --ref records the external reference
- **WHEN** `fleece promote a1b2c3 --ref ENG-42` runs with `tracker=linear`
- **THEN** `a1b2c3` is set to status `Promoted` with tag `promoted=ENG-42`
- **AND** no GitHub or Linear API is called

#### Scenario: Linear promote skips already-promoted issues
- **WHEN** `fleece promote a1b2c3 --ref ENG-42` runs and `a1b2c3` already has a `promoted=` tag
- **THEN** `a1b2c3` is skipped with a warning and its existing tag is unchanged

### Requirement: `fleece absorb` SHALL print guidance when the tracker is Linear

When the active tracker is `linear`, `fleece absorb` SHALL NOT call any API. It SHALL print guidance
directing the user to create the Fleece issue with `fleece create ... --tag absorbed-from=<ref>` and
to perform the Linear-side comment/assignment with the agent's Linear tooling. It SHALL make no change
to Fleece state itself.

#### Scenario: Linear absorb prints guidance
- **WHEN** `fleece absorb ENG-42` runs with `tracker=linear`
- **THEN** guidance to run `fleece create ... --tag absorbed-from=ENG-42` is printed
- **AND** no Fleece issue is created and no GitHub or Linear API is called

### Requirement: `fleece auth` SHALL be tracker-aware for non-GitHub trackers

When the active tracker is `linear`, `fleece auth` SHALL report that the tracker is `linear` and that
the Fleece CLI does not authenticate to Linear — hand-off is performed by the agent's Linear tooling —
and SHALL exit zero. Its `--json` output SHALL include the active `tracker` and an `applicable` flag
that is `false` for Linear.

#### Scenario: Linear auth reports not-applicable and exits zero
- **WHEN** `fleece auth` runs with `tracker=linear`
- **THEN** it reports that the CLI does not authenticate to Linear and exits zero

#### Scenario: Linear auth JSON carries tracker metadata
- **WHEN** `fleece auth --json` runs with `tracker=linear`
- **THEN** the JSON includes `"tracker": "linear"` and `"applicable": false`
