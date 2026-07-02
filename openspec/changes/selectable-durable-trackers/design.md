## Context

Fleece is branch-local ephemeral working memory; durable work "escapes" to an external tracker via
`fleece promote`, and `fleece absorb`/`fleece auth` complete the round-trip. Today every one of those
surfaces — command behaviour, the installed skill references, and the `fleece install`-managed CLAUDE
memory block — assumes GitHub Issues. All GitHub access is already isolated behind `IGitHubService`
(OctoKit impl in `Fleece.GitHub`, registered via `AddFleeceGitHub()` in `CliComposition`), and there
is a tracked per-repo settings file (`.fleece/settings.json`, `FleeceSettings` record, `fleece config`
read/write) plus a JSON-source-generated serialization context.

The decisive constraint: **the `fleece` CLI process cannot talk to Linear.** Linear tooling in this
environment is exposed as MCP tools that only the *agent* can call, not a spawned CLI process. So a
"real" second provider cannot be a Linear API client living in the CLI; it must be realized by the
agent, with the CLI adapting its behaviour to a configured tracker.

## Goals / Non-Goals

**Goals:**
- Make the durable tracker a per-repository choice (`github` | `linear`), selected at `fleece install`
  and honoured by `promote`/`absorb`/`auth`, defaulting to `github` so existing repos are unchanged.
- Keep Fleece free of any tracker SDK for Linear — Linear hand-off is performed by the agent (MCP);
  the CLI only emits payloads and records agent-supplied refs.
- Ship tracker-appropriate skill references and a tracker-worded CLAUDE block from `fleece install`.
- Keep the Linear command paths hermetically testable (no network, no fake GitHub service).

**Non-Goals:**
- A `Fleece.Linear` assembly, a GraphQL client, or any new NuGet dependency.
- Renaming `IGitHubService` to a provider-neutral Core interface.
- Supporting multiple active trackers simultaneously, or a per-command `--tracker` flag.
- A new `.fleece/config.yaml` file.
- Native Linear `absorb` (comment/assign on the source Linear issue) — that is the agent's job via MCP.

## Decisions

### D1 — Linear is agent-realized; the CLI never calls Linear
The `tracker` setting selects behaviour; when `tracker=linear` the CLI performs only local operations
(emit a payload, record a ref, print guidance). The agent creates/updates Linear issues through its
MCP tools.
- *Alternative — native `Fleece.Linear` GraphQL client:* rejected. Large surface (API-key + workspace
  **team** resolution that GitHub never needed), a reflection-based YAML/GraphQL stack fighting the
  codebase's AOT/source-gen discipline, and redundant with the agent's existing Linear MCP access.

### D2 — Store the tracker in `.fleece/settings.json` (`tracker` key)
Add `Tracker` to `FleeceSettings` and a `tracker` case to `SetSettingAsync`. The file is already
tracked (rides with the repo → visible to teammates and to the commands), already has a read/write
command, and is JSON-source-generated.
- *Alternative — new `.fleece/config.yaml`:* rejected. Adds a second config surface and a YAML parser
  (no YAML library exists in the repo; `openspec dependencies` hand-parses a tiny frontmatter block)
  into an AOT-disciplined codebase.

### D3 — A CLI-layer `ITrackerWorkflow` strategy, resolved from settings
`ITrackerWorkflow { PromoteAsync, AbsorbAsync, AuthAsync }` with `GitHubTrackerWorkflow` (delegates to
the unchanged `IGitHubService`) and `LinearTrackerWorkflow` (CLI-local). `CliComposition` reads the
effective `tracker` and registers the matching implementation; `PromoteCommand`/`AbsorbCommand`/
`AuthCommand` inject `ITrackerWorkflow` and stay thin.
- *Alternative — Core `IIssueTrackerService` rename:* rejected; Linear has no Core implementation, so
  the rename is churn without payoff. *Alternative — `if (tracker==...)` inside each command:*
  rejected; procedural, and the Linear path would drag the fake GitHub service into its tests.

### D4 — `promote` in Linear mode is emit-then-record
Bare `fleece promote <ids>` emits the bundle title/body (reusing the existing `BuildIssueBody`) as
human text and via `--json` `{title, body, issueIds}`, and changes no state. `fleece promote <ids>
--ref <linear-id-or-url>` records `promoted=<ref>` and sets `Promoted`. The auth-gate runs only for
GitHub.
- *Alternative — record-only `--ref`:* rejected; the agent would reimplement the bundle body the CLI
  already formats. Emit-then-record mirrors the existing "bare `absorb` is a guided no-op" pattern.

### D5 — `absorb` stays GitHub-native; Linear mode prints guidance
GitHub `absorb` earns its keep by commenting on and assigning the source issue — side-effects the CLI
cannot perform on Linear. In Linear mode `absorb` prints guidance (`fleece create ... --tag
absorbed-from=<ref>`) and the `linear.md` skill carries the recipe; the agent does the Linear-side
comment/assign via MCP.
- *Alternative — uniform `absorb --title --description` verb:* rejected; it degenerates to a thin
  alias over `create` with tracker-dependent flags and hides that the CLI can't do the Linear side-effects.

### D6 — `promoted=<#>` tag value generalizes from int to string ref
So Linear identifiers (`ENG-42`) or URLs fit. GitHub promotions still store the issue number as its
string form. `KeyedTag` values are already strings end-to-end, so this is a semantic widening, not a
storage change.

### D7 — `fleece install --tracker` with interactive fallback
`InstallSettings` gains `--tracker github|linear`. When unset and stdin is a TTY, prompt via Spectre
`SelectionPrompt`; when unset and non-interactive, default to `github`. Install then persists the
choice, writes `BuildClaudeMemoryBlock(tracker)`, and ships the core skill refs + `github.md` XOR
`linear.md` + a tracker-named SKILL.md.

## Risks / Trade-offs

- **Two-step promote is easy to half-complete** (agent emits, creates the Linear issue, forgets to
  record) → the bare emit output prints the exact `--ref` re-run command; the issue simply stays
  active until recorded, so the CI gate still catches it.
- **`promoted` tag widening could confuse consumers expecting an int** → GitHub still writes the number;
  only Linear writes non-numeric refs, and readers already treat the value as a string. No existing
  data changes.
- **Selective skill shipping means only one tracker's reference is present** → intentional (less noise
  for the agent); re-running `install --tracker <other>` swaps it, and both files remain embedded.
- **Non-interactive install with no `--tracker` silently defaults to github** → documented; matches the
  back-compat default and keeps CI/scripted installs deterministic.

## Migration Plan

No migration required. `tracker` defaults to `github`, so existing repositories keep today's exact
behaviour (commands, skill, CLAUDE block) until someone opts in via `fleece install --tracker linear`
or `fleece config` sets `tracker=linear`. Rollback is setting `tracker` back to `github` and
re-running `install`. The change is additive at the data layer (a new optional setting; a widened tag
value that remains backward-compatible).

## Open Questions

- Should `promote --ref` also be accepted in **GitHub** mode (record an externally-created GitHub issue
  without an API call)? Out of scope here; noted as a possible later symmetry.
