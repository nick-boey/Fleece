## MODIFIED Requirements

### Requirement: `fleece auth` SHALL report GitHub authentication status

When the active durable tracker is `github`, the `fleece auth` command SHALL check GitHub
authentication via `IGitHubService` and report whether a usable token was resolved, which source
provided it, and the authenticated login. It SHALL exit non-zero when no usable credentials are found.
When the active tracker is not `github`, the GitHub authentication check SHALL NOT run (see the
`durable-tracker-selection` capability for tracker-aware `auth` behaviour).

#### Scenario: Authenticated
- **WHEN** `fleece auth` runs with `tracker=github` and a valid token resolves
- **THEN** it prints the authenticated login and token source and exits zero

#### Scenario: Unauthenticated
- **WHEN** `fleece auth` runs with `tracker=github` and no usable token resolves
- **THEN** it prints guidance on authenticating and exits non-zero

### Requirement: `fleece promote` SHALL escalate Fleece issues into a single GitHub issue

When the active durable tracker is `github`, the `fleece promote <id> [<id>...]` command SHALL create
exactly one GitHub issue representing the supplied Fleece issues, using the root issue's title and a
body composed as a task list of the bundled issues. It SHALL first verify GitHub authentication and
fail without creating anything when unauthenticated. On success it SHALL set each supplied issue's
status to `Promoted` and add the keyed tag `promoted=<ref>`, where `<ref>` is the created GitHub issue
number rendered as a string. The command SHALL be idempotent: an issue that already carries a
`promoted=` tag SHALL be skipped with a warning rather than re-promoted.

#### Scenario: Promote a bundle of issues
- **WHEN** `fleece promote a1b2c3 d4e5f6` runs with `tracker=github` and valid credentials
- **THEN** one GitHub issue is created whose body lists both issues as a task list
- **AND** both issues are set to status `Promoted` with tag `promoted=<github-#>`

#### Scenario: Promotion is idempotent
- **WHEN** `fleece promote a1b2c3` runs and `a1b2c3` already has a `promoted=` tag
- **THEN** no new GitHub issue is created for it and a warning is printed

#### Scenario: Auth-gate is GitHub-only
- **WHEN** `fleece promote a1b2c3` runs with `tracker=github` and no usable GitHub token resolves
- **THEN** the command fails with guidance to run `fleece auth` and creates nothing

### Requirement: `fleece absorb` SHALL convert a GitHub issue into a Fleece issue

When the active durable tracker is `github`, the `fleece absorb #<github-#>` command SHALL create a
Fleece issue from the referenced GitHub issue (GitHub title → Fleece title, GitHub body → Fleece
description) and add the keyed tag `absorbed-from=<github-#>`. It SHALL add a comment to the GitHub
issue noting it was absorbed into Fleece issue `<id>` on branch `<branch>`, and SHALL assign the
GitHub issue to the current user **without closing it**. A reference supplied without a leading `#`
SHALL perform no action. When the active tracker is not `github`, `absorb` SHALL NOT call GitHub (see
the `durable-tracker-selection` capability for tracker-aware `absorb` behaviour).

#### Scenario: Absorb a GitHub issue
- **WHEN** `fleece absorb #123` runs with `tracker=github` and valid credentials
- **THEN** a Fleece issue is created from issue 123 with tag `absorbed-from=123`
- **AND** GitHub issue 123 receives a comment naming the new Fleece issue id and current branch
- **AND** GitHub issue 123 is assigned to the current user and remains open

#### Scenario: Missing `#` performs no action
- **WHEN** `fleece absorb 123` runs without a leading `#`
- **THEN** no Fleece issue is created and no GitHub call is made
- **AND** a warning instructs the user to re-run as `fleece absorb #123`
