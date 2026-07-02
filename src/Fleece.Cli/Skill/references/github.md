<!-- This file is managed by `fleece install`. Manual edits will be overwritten on the next install. -->

# GitHub Round-trip

This reference is installed because the repository's durable tracker is **GitHub**
(`fleece config --get tracker` → `github`). Fleece is ephemeral branch memory; GitHub Issues
are the durable home for long-running work. Three commands move work across that boundary.

## Authentication

`fleece auth` reports the resolved GitHub login and which source supplied the token.
Token resolution order:

1. `gh auth token` (the GitHub CLI, if installed and logged in)
2. `GH_TOKEN` / `GITHUB_TOKEN` environment variables
3. A personal access token stored in Fleece config

A non-zero exit means no usable credential was found — log in with `gh auth login` or set
a token before using `promote`/`absorb`.

## Promote: Fleece → GitHub

`fleece promote <id> [<id>...]`

Escalates one or more Fleece issues into a SINGLE GitHub issue. The GitHub issue takes the
first (root) issue's title; its body is a task list where each bundled issue contributes a
checklist item (id + title + type/priority) followed by that issue's FULL description. Each
promoted Fleece issue is set to `promoted` and tagged `promoted=<github-#>` (the issue number
as a string). Already-promoted issues are skipped with a warning.

### Write a complete description BEFORE you promote

The GitHub issue is only as useful as the descriptions it carries — a promoted issue with a
bare title produces a near-empty GitHub issue that tells a future reader nothing about what
needs doing. Before promoting, make sure every issue in the bundle has a description that
stands on its own outside this branch:

1. `fleece show <id> --json` and read the `description`.
2. If it is missing or thin, write a real one: the problem/goal, what "done" looks like, and
   any constraints or context a reader without this branch's history would need.
3. Derive it from the issue title and the work you did on the branch. If the intent is
   genuinely ambiguous and you cannot infer it confidently, ASK THE USER to clarify rather
   than promoting a vague stub.
4. Persist it with `fleece edit <id> -d "<full description>"`, then promote.

Use this during a PR for any issue that must outlive the branch.

## Absorb: GitHub → Fleece

`fleece absorb #<number>`

Creates a new Fleece issue from an existing GitHub issue, tags it `absorbed-from=<#>`, and
comments on + assigns (does NOT close) the GitHub issue. The `#` is required: a bare
`fleece absorb 123` performs no action and warns.

Use this to pull a GitHub issue into the current branch's working memory.
