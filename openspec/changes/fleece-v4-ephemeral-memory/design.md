## Context

Fleece's event-sourced storage (snapshot + per-commit change files + `follows`-DAG + merge markers + replay cache) was built to solve exactly one problem: letting issue data survive a squash-merge into a long-lived `.fleece/issues.jsonl` snapshot on `main` without merge conflicts. Fleece v4 removes the long-lived snapshot entirely — issues are branch-scoped working memory that is sealed (archived + cleared) before a PR merges. Once nothing merges issue data into `main`, every justification for the DAG machinery disappears.

This design covers the four interlocking decisions that the rest of the implementation depends on: the per-issue storage layout, the GitHub-integration boundary, the seal/CI-gate lifecycle, and the command-surface churn. It assumes `Fleece.Core`'s purity contract (no I/O statics, fully mockable via `MockFileSystem`) is preserved so the E2E suite stays hermetic.

## Goals / Non-Goals

**Goals:**
- Replace the change-file DAG with the simplest storage that supports concurrent writers within a branch's life.
- Make "a mergeable branch has no live Fleece issues" a mechanically enforceable invariant with a tool-free CI check.
- Add GitHub round-tripping (`promote`/`absorb`/`auth`) without compromising Core purity or test hermeticity.
- Delete more code than is added — the simplification is the feature.

**Non-Goals:**
- Cross-branch merging of issue data (explicitly impossible by design; issues die at the branch boundary).
- Lossless round-trip fidelity between Fleece issues and GitHub issues (promotion/absorption are agent-driven and intentionally lossy).
- An enforced (un-gameable) archive guarantee — the `.fleece/archive/` audit log is a convention, not a hard invariant.
- Re-deriving per-property `*LastUpdate`/`*ModifiedBy` history (already dropped in the lean `Issue` shape; git history is the record).

## Decisions

### D1: One append-only event log per issue (`.fleece/issues/<id>.jsonl`)

Each issue is a single append-only JSONL file: first line `create`, subsequent lines `set`/`add`/`remove`. **File order is truth** — there is no cross-file ordering, no `follows` pointer, no topological replay. Reads enumerate `.fleece/issues/*.jsonl` and replay each file independently.

- **Why over session-sharded `issues_<hash>.jsonl`** (the original brief's shape): session-sharding spreads one issue's events across multiple files (created session A, edited session B), which *reintroduces* cross-file ordering — the exact complexity being deleted. Per-issue files make each issue's log self-contained, so the DAG genuinely disappears.
- **Why over a single current-state object per issue** (`<id>.json`, rewrite-on-edit): append-only writes merge more cleanly under git, and editing never rewrites a whole record. The within-branch provenance is a bonus, though the durable record now lives in `.fleece/archive/`.
- **Conflict behaviour**: distinct issues = distinct files = no conflict. The same issue edited on two branches conflicts on that one file — which is semantically correct and rare (branch-scoped issues are usually edited by one branch).

**Cascade deletions** (all lose their reason to exist under D1):
| Artifact | Why it dies |
|---|---|
| `follows`-DAG / merge markers | no cross-merge topology to preserve |
| `link` command + `link --merge` | nothing to link |
| `.active-change` pointer | writes append to `<id>.jsonl` directly |
| `.replay-cache` | per-issue files are tiny; replay is cheap |
| `tombstones.jsonl` + id-collision retry | no durable snapshot for ids to collide on; `delete` just removes the file |
| `project` / `merge` / old `clean` / `diff` | superseded by `seal`, or obsolete |

### D2: `seal` and the CI gate invariant

`seal` is the "finish the branch" operation. It refuses unless **every** issue is inactive (`Complete`/`Closed`/`Promoted`); on refusal it lists the offending active issues. On success it writes `.fleece/archive/issues_<contenthash>.jsonl` (content hash, since the archive is immutable) and removes all `.fleece/issues/*.jsonl`.

The CI gate is a cross-platform script (bash + PowerShell, no fleece binary on the runner) that **fails the PR iff `.fleece/issues/` contains any `.jsonl`**. The states chain cleanly:

```
active issues exist → seal refuses → live files remain → CI fails
all inactive        → seal archives + clears → dir empty → CI passes
```

- **Why a file-existence check over status-parsing in shell**: robust and tool-free; parsing JSONL statuses in portable shell is fragile. Accepted trade-off: a manual `rm` of the files passes CI while skipping the archive. The archive is a convention; if hard enforcement is ever needed, the gate can shell out to `fleece` instead.
- **Archive lands on `main`** as a deliberate, openspec-archive-style audit log. It is the *only* Fleece data permitted on `main`.

### D3: GitHub integration behind `IGitHubService` in a separate assembly

OctoKit, network calls, and auth are isolated behind `IGitHubService` in a new `Fleece.GitHub` assembly. `Fleece.Core` never references OctoKit, so it stays pure and the E2E suite drives a fake `IGitHubService`.

- **Token resolution order**: `gh auth token` → `GH_TOKEN`/`GITHUB_TOKEN` env → config-stored PAT. **Target repo** parsed from `git remote get-url origin`.
- **`promote <id> [<id>...]`**: the CLI is a thin primitive — "render these N Fleece issues into one GitHub issue (title from the root, body = task list of the bundle) and mark them all `Promoted` with `promoted=<github-#>`." The *bundling intelligence* (which subtree to promote) lives in the agent via `prime`, not in the command. Idempotent: skip/warn if a `promoted=` tag is already present.
- **`absorb #<github-#>`**: create a Fleece issue from the GitHub issue (title, body→description), tag `absorbed-from=<github-#>`, comment on the GitHub issue ("absorbed into Fleece issue `<id>` on branch `<branch>`"), and assign it to the current user **without closing it**. Bare `absorb 123` (no `#`) performs **no action** and warns to re-run as `absorb #123` — making the Fleece-id vs GitHub-number namespace split explicit at the point of confusion.

### D4: Status / type model and command surface

- Statuses: remove `Draft`; `Archived` → `Promoted`. `Promoted` is terminal and distinct from `Complete`/`Closed` — it means "escaped to durable GitHub storage" and carries `promoted=<#>`. `DoneStatuses`/`TerminalStatuses`/inactive-set definitions update accordingly.
- Types: remove `Idea`. Keep `Task`/`Bug`/`Chore`/`Feature`/`Verify`.
- `next`/`dependency`/`move`/`ExecutionMode` are **kept as-is** for now — agents may use them to decompose work and pick the next item in a fresh context window. Usage will be observed before deciding whether to trim.

### D5: `openspec dependencies` reuses existing engines

Pure visualizer over `openspec/changes/<name>/dependencies.md`: parse YAML frontmatter `depends-on: [change-names]` (ignore soft deps in the HTML-comment body), build nodes=changes / edges=depends-on, render via the existing graph-layout `next` renderer, and reuse `validate`'s cycle detection to warn on circular deps. Implemented as a command **group** (`fleece openspec dependencies`) to leave room for future OpenSpec tooling. Orthogonal to issues; couples Fleece to the OpenSpec folder layout by design.

## Risks / Trade-offs

- **CI gate is gameable** (manual `rm` skips the archive) → accepted; archive is a convention. Escalation path: shell out to `fleece` for status-aware checking if enforcement is needed.
- **Same-issue cross-branch edits conflict** in JSONL → acceptable; it is a true semantic conflict and rare under branch-scoped usage. Append-only line structure keeps most concurrent edits auto-mergeable.
- **GitHub coupling adds a network/auth failure surface** → contained in `Fleece.GitHub`; Core and E2E remain offline. `auth` gives users a fast pre-flight check.
- **Promotion is lossy** (hierarchy/dependencies/tags don't map cleanly to a GitHub issue) → mitigated by agent-driven bundling rendered as a task list; the Fleece side keeps the `promoted=<#>` linkage.
- **Existing repos have durable `.fleece/issues.jsonl` on `main`** → not silently migrated; a detection warning routes users to `fleece prime v4-migration`, which instructs the agent to promote long-running issues to GitHub. Avoids destructive auto-migration.
- **Breaking command surface** (`project`/`merge`/`diff`/`link`/`clean` removed) → gated behind the major v4 bump; `migrate`/`prime` help text reframes the model.

## Migration Plan

1. Ship behind the **v4 major bump**; document breaking changes in the changelog.
2. On any `fleece` invocation, detect a legacy `.fleece/issues.jsonl` snapshot and emit the `fleece prime v4-migration` warning (non-destructive).
3. `fleece prime v4-migration` instructs the agent to `promote` long-running legacy issues to GitHub Issues, then `seal` to archive and clear.
4. `fleece install` (re-run) replaces the daily projection Action + merge-marker hooks with the CI gate Action + refreshed SessionStart block.
5. Rollback: pin to the latest v3 release; v3 and v4 on-disk layouts are distinct (`changes/` + snapshot vs `issues/` per-issue logs), so a repo is unambiguously one or the other.

## Open Questions

- Should `seal`'s archive be content-hashed over the canonicalised issue set (stable across reorderings) or raw file bytes? (Leaning canonicalised, so identical logical state yields one archive file.)
- Does the optional pre-commit active-issue-count hook warn only, or also print the seal hint? (Leaning warn + hint, never block.)
- `Fleece.GitHub` as a brand-new assembly vs a folder in `Fleece.Cli` — assembly keeps Core's purity contract crisp but adds a project. (Leaning separate assembly.)
