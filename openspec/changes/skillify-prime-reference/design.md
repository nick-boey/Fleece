## Context

`fleece prime` is wired as a `SessionStart` hook by `fleece install` (registered as the literal command `fleece prime`). On every session it runs `PrimeCommand.ExecuteAsync`, which — when a `.fleece/` directory is present and no topic is supplied — prints `BuildOverview(activeCount)`: a ~2k-token block mixing static workflow reference with a single dynamic line (`There are currently N active issue(s)`). With a topic argument it prints one of nine ~500–1500 token static topic consts (`hierarchy`, `commands`, `statuses`, `sync`, `json`, `next`, `tree`, `github`, `v4-migration`). `fleece install` also writes a philosophy block into `CLAUDE.md` (`BuildClaudeMemoryBlock`, delimited by marker comments) that partly duplicates the overview.

Claude skills are pull-based: an agent loads `.claude/skills/<name>/SKILL.md` only when it already senses relevance (from the skill `description` or a standing pointer). They are the correct home for the static reference but structurally cannot deliver the dynamic CI-gate tripwire — an agent unaware that active issues exist has no trigger to pull the skill. This change therefore **splits** the surfaces rather than swapping prime for a skill.

There is no existing `install` spec; the skill-provisioning behavior is net-new. The `prime-command` spec exists and is modified.

## Goals / Non-Goals

**Goals:**
- Cut clean-session token cost from ~2k to ~0 while preserving the CI-gate signal on dirty branches.
- Establish a single source of truth for the static reference (the skill) — no duplication with prime or CLAUDE.md.
- Keep the SessionStart hook registration untouched: the hook still invokes `fleece prime`; only prime's output changes.
- Make the CLAUDE.md philosophy the standing relevance signal that keeps the skill discoverable when the hook is silent.

**Non-Goals:**
- Changing the hook command string or `fleece install`'s hook-registration logic.
- Preserving `fleece prime <topic>` as a CLI path (it is removed; the skill replaces it).
- Reworking issue storage, the CI gate workflow, or the GitHub round-trip.
- Authoring user-level (global) skills — the skill is provisioned per-project under the repo's `.claude/`.

## Decisions

### D1: Three surfaces split by why / what / how
- **CLAUDE.md** (always in context) → *why*: philosophy + the decision rule (*blocks this PR → Fleece; non-blocking follow-up / new feature / durable → GitHub issue*) + a pointer to the skill.
- **`fleece prime`** (SessionStart hook) → *what's live*: the dynamic active-issue line only; silent when zero active.
- **`.claude/skills/fleece/`** (pull-based) → *how*: full static reference.

Rationale: each surface earns its token cost differently. The always-on surface must be smallest; the per-session surface must be near-zero unless actionable; the pull-based surface can be rich because it costs nothing until summoned. *Alternative considered*: a single slimmer prime block — rejected, it still pays per-session and keeps duplicating CLAUDE.md.

### D2: `fleece prime` stays the hook command; only its body changes
`InstallCommand.InstallClaudeHooksAsync` detects/registers `fleece prime` via `hasPrimeHook`. Keeping `prime` as the command means zero changes to hook wiring and no re-install migration for existing users. `PrimeCommand` keeps `CountActiveIssuesAsync` and the `.fleece/`-absent silent-exit guard; it loses `BuildOverview`'s static body and all nine topic consts. *Alternative considered*: a new `fleece hook session-start` command — rejected as it forces a settings.json migration for no benefit.

### D3: Prime output — silent when clean, two lines when dirty
- `.fleece/` absent → no output, exit 0 (unchanged).
- `.fleece/` present, `activeCount == 0` → **no output**, exit 0 (new — was the full overview).
- `.fleece/` present, `activeCount > 0` → a short signal: the count, the resolve/promote/seal rule, the CI-gate consequence, and a pointer to the `fleece` skill for commands.

### D4: Skill content is an embedded markdown resource, written by install
The skill markdown is authored as real `.md` files in the repo and compiled in as embedded resources (no C# string escaping; diff-able; Fleece can dogfood its own skill). `fleece install` gains `InstallClaudeSkillAsync`, which writes `.claude/skills/fleece/SKILL.md` and `.claude/skills/fleece/references/<topic>.md`. *Alternative considered*: a raw-string const mirroring `BuildWorkflowYaml` — rejected for a doc this size; markdown-in-strings is hard to author and review.

### D5: Skill granularity — `SKILL.md` + `references/`
`SKILL.md` holds the overview and an index; the nine topics become `references/<topic>.md`, pulled only when needed. This mirrors the old `prime <topic>` progressive disclosure and keeps the always-pulled skill body small. The `SKILL.md` `description` frontmatter is the matcher that makes the skill discoverable, reinforced by the CLAUDE.md pointer (D1).

### D6: Re-install overwrites the skill wholesale
Unlike `CLAUDE.md` (shared, marker-delimited, merged in place), the skill files are fleece-owned generated artifacts — same spirit as the CI workflow. Install overwrites `SKILL.md` and `references/*.md` wholesale each run so content updates propagate, with a "managed by fleece install — edits will be overwritten" header. *Alternative considered*: write-only-if-absent — rejected, it strands users on stale reference after a Fleece upgrade.

## Risks / Trade-offs

- **Pull-based skill never fires → reference goes unseen.** → The CLAUDE.md pointer (D1) plus a precise skill `description` provide the standing relevance signal; the dirty-branch prime line (D3) also names the skill. The dynamic CI-gate tripwire — the only thing that must not be missed — stays in prime, not the skill.
- **BREAKING: `fleece prime <topic>` removed.** Any user muscle-memory, docs, or scripts invoking it break. → Acceptable and intended (single source of truth); the slim prime output and CLAUDE.md redirect to the skill. Documented as BREAKING in the proposal.
- **Wholesale overwrite (D6) clobbers user edits to the skill.** → The managed-file header warns against editing; customization belongs in a separate, non-fleece skill. Matches existing expectations for generated artifacts.
- **Embedded-resource plumbing is new to the CLI.** → Small, well-trodden .NET pattern; the install tests assert the files land on disk with expected content, covering the wiring.
- **CLAUDE.md memory-block rewrite could drop content agents relied on.** → The block stays marker-delimited and re-runnable; the reference it used to imply now lives in the skill, reachable via the pointer.
