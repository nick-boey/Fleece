## ADDED Requirements

### Requirement: GitHub access SHALL be isolated behind `IGitHubService`

All GitHub API access SHALL be mediated by an `IGitHubService` abstraction whose OctoKit-based implementation lives outside `Fleece.Core`, so that `Fleece.Core` carries no OctoKit dependency and the in-process E2E suite can substitute a fake. The implementation SHALL resolve a token in the order `gh auth token`, then `GH_TOKEN`/`GITHUB_TOKEN` environment variables, then a config-stored PAT, and SHALL determine the target repository by parsing `git remote get-url origin`.

#### Scenario: Core remains free of OctoKit
- **WHEN** `Fleece.Core` is compiled
- **THEN** it has no reference to OctoKit and no direct GitHub network calls

#### Scenario: Token resolution order
- **WHEN** a GitHub operation needs a token and `gh auth token` returns a value
- **THEN** that token is used without consulting environment variables or config
- **AND WHEN** `gh auth token` yields nothing but `GH_TOKEN` is set, the environment token is used
- **AND WHEN** neither is available but a config PAT exists, the config PAT is used

#### Scenario: Target repository inferred from origin remote
- **WHEN** a GitHub operation runs in a repo whose `origin` remote is `git@github.com:owner/name.git`
- **THEN** the operation targets the `owner/name` repository

### Requirement: `fleece auth` SHALL report GitHub authentication status

The `fleece auth` command SHALL check GitHub authentication via `IGitHubService` and report whether a usable token was resolved, which source provided it, and the authenticated login. It SHALL exit non-zero when no usable credentials are found.

#### Scenario: Authenticated
- **WHEN** `fleece auth` runs and a valid token resolves
- **THEN** it prints the authenticated login and token source and exits zero

#### Scenario: Unauthenticated
- **WHEN** `fleece auth` runs and no usable token resolves
- **THEN** it prints guidance on authenticating and exits non-zero

### Requirement: `fleece promote` SHALL escalate Fleece issues into a single GitHub issue

The `fleece promote <id> [<id>...]` command SHALL create exactly one GitHub issue representing the supplied Fleece issues, using the root issue's title and a body composed as a task list of the bundled issues. On success it SHALL set each supplied issue's status to `Promoted` and add the keyed tag `promoted=<github-#>`. The command SHALL be idempotent: an issue that already carries a `promoted=` tag SHALL be skipped with a warning rather than re-promoted.

#### Scenario: Promote a bundle of issues
- **WHEN** `fleece promote a1b2c3 d4e5f6` runs with valid credentials
- **THEN** one GitHub issue is created whose body lists both issues as a task list
- **AND** both issues are set to status `Promoted` with tag `promoted=<github-#>`

#### Scenario: Promotion is idempotent
- **WHEN** `fleece promote a1b2c3` runs and `a1b2c3` already has a `promoted=` tag
- **THEN** no new GitHub issue is created for it and a warning is printed

### Requirement: `fleece absorb` SHALL convert a GitHub issue into a Fleece issue

The `fleece absorb #<github-#>` command SHALL create a Fleece issue from the referenced GitHub issue (GitHub title → Fleece title, GitHub body → Fleece description) and add the keyed tag `absorbed-from=<github-#>`. It SHALL add a comment to the GitHub issue noting it was absorbed into Fleece issue `<id>` on branch `<branch>`, and SHALL assign the GitHub issue to the current user **without closing it**. A reference supplied without a leading `#` SHALL perform no action.

#### Scenario: Absorb a GitHub issue
- **WHEN** `fleece absorb #123` runs with valid credentials
- **THEN** a Fleece issue is created from issue 123 with tag `absorbed-from=123`
- **AND** GitHub issue 123 receives a comment naming the new Fleece issue id and current branch
- **AND** GitHub issue 123 is assigned to the current user and remains open

#### Scenario: Missing `#` performs no action
- **WHEN** `fleece absorb 123` runs without a leading `#`
- **THEN** no Fleece issue is created and no GitHub call is made
- **AND** a warning instructs the user to re-run as `fleece absorb #123`
