## Context

`fleece prime` is a single CLI command whose sole responsibility is to emit documentation for AI agents at session start. Its implementation lives in one file (`src/Fleece.Cli/Commands/PrimeCommand.cs`) and consists of an `Execute` method plus a set of `private const string` content blocks, one per topic, dispatched through a case-insensitive `Topics` dictionary.

The command is wired into Claude Code via the `SessionStart` hook installed by `fleece install`. At session start the hook runs `fleece prime` with no topic, so the default (no-topic) overview is what the agent actually reads every session in every Fleece-enabled repo.

OpenSpec is a separate, parallel system that lives in `openspec/` at the repo root. It has its own CLI (`openspec`) and its own on-disk layout (`openspec/changes/`, `openspec/specs/`). There is currently no awareness between the two systems — each is blind to the other. This change gives Fleece one-directional awareness of OpenSpec's presence: when `openspec/` exists alongside `.fleece/`, the prime output is extended. OpenSpec itself is untouched.

## Goals / Non-Goals

**Goals:**

- Emit OpenSpec linking guidance automatically at session start in repos that use both systems, so agents never have to be hand-instructed.
- Keep the guidance content accessible on demand via `fleece prime openspec`, regardless of whether `openspec/` is present, so the topic is discoverable and unit-testable without filesystem fixtures for every test.
- Leave the existing overview output byte-identical in repos that do not have `openspec/`, so users on Fleece-only projects see no behaviour change.
- Use Fleece primitives that already exist (keyed tags, parent-child hierarchy, execution-order) for the linking convention — no new fields or APIs on `Issue`.

**Non-Goals:**

- This change does NOT modify OpenSpec itself, its CLI, its schemas, or its artifacts.
- This change does NOT automate the linking itself (e.g., it does not have `fleece` watch for new OpenSpec changes and auto-tag issues). The agent performs the linking, guided by the prime content.
- This change does NOT introduce a new data model for tracking the Fleece ↔ OpenSpec relationship. The relationship is carried solely by the `openspec={change-name}` keyed tag on the Fleece issue.
- This change does NOT validate that a linked change name actually exists in `openspec/changes/`. The tag is documentation, not a foreign key.
- This change does NOT touch the `fleece install` hook, branch parsing logic, or any other command.

## Decisions

### Decision 1: Conditional inline injection into the overview, not a separate hook invocation

The prime overview is a single block of content assembled once per invocation. When `openspec/` is detected, the OpenSpec Integration section is appended to the emitted string (either by concatenation in `Execute` or by an `OverviewContent` that is built at runtime from two halves).

**Alternative considered:** Have `fleece install` write two separate hook entries — `fleece prime` and `fleece prime openspec` — so the OpenSpec content comes through as a second session-start message. Rejected because (a) it would require users with existing installs to re-run `fleece install` to get the behaviour, (b) it splits one logical "session primer" into two, and (c) conditional detection inside the hook is simpler than conditional hook installation.

### Decision 2: Also register `openspec` as a first-class topic

Even though detection auto-injects the content, we add an `["openspec"] = OpenSpecContent` entry to the `Topics` dictionary and include `openspec` in the topics list near the bottom of the overview.

**Why:** (a) it makes the topic discoverable via `fleece prime -h`-style workflows, (b) it lets the unit tests verify the content in isolation from filesystem state, (c) it gives the agent a way to re-read the guidance on demand mid-session.

### Decision 3: Detection is `Directory.Exists("openspec")` relative to CWD only

No walking up the directory tree, no checking for `openspec/config.yaml` as a signal, no resolving symlinks specially beyond what `Directory.Exists` already does.

**Why:** the existing `.fleece/` check already uses this same pattern (`Path.Combine(Directory.GetCurrentDirectory(), ".fleece")`). Symmetry matters. `prime` is always invoked from the repo root via the session-start hook, so CWD-relative is sufficient.

**Alternative considered:** Read `openspec/config.yaml` and only trigger if the schema field is set. Rejected — the presence of the directory is the useful signal; parsing is overkill.

### Decision 4: Linking conventions are encoded in prose, not tooling

The OpenSpec Integration content describes what tag to apply, how to pick an issue, and how to arrange hierarchies — but Fleece itself does not enforce any of this. An issue with `openspec=does-not-exist` is valid; an issue without a tag for a change-in-progress is valid. This matches how Fleece treats other keyed tags (`project=`, `team=`).

**Why:** enforcement would require either parsing `openspec/` on every `fleece` invocation or running the `openspec` CLI as a subprocess. Both couple Fleece to OpenSpec's on-disk layout and CLI stability. Prose guidance keeps the coupling zero.

### Decision 5: Decision-tree content, not a flowchart image

The linking logic is described as numbered prose rules in the content, augmented with a small ASCII decision tree. No external assets, no rendered images.

**Why:** the content is consumed by AI agents reading text. ASCII is fine. Markdown renders it readably for humans too.

### Decision 6: Implementation stays in `PrimeCommand.cs`

The `OpenSpecContent` constant, the `Topics` entry, and the conditional-append logic all live in `PrimeCommand.cs`. No new service, no new interface in `Fleece.Core`.

**Why:** per `CLAUDE.md`, CLI commands are thin wrappers around Core, and Core should contain business logic. But this content is not business logic — it is CLI-output copy, specific to the CLI's SessionStart role. Putting a `PrimeContentService` in Core would be abstraction for its own sake. The one place Core might eventually want to get involved is if the OpenSpec content ever needed to query issues (it does not today).

## Risks / Trade-offs

- **[Content drift with OpenSpec upstream]** → If OpenSpec renames concepts or commands, the embedded guidance can go stale. Mitigation: the content references only stable concepts (change name, tasks, archive) and does not reproduce OpenSpec command surfaces. If upstream drifts, the fix is a content edit in `OpenSpecContent`.
- **[Hidden content-size growth]** → Every session in an OpenSpec+Fleece repo pays the token cost of the new section. Mitigation: keep the section tight (target ~600 tokens; the decision tree is the bulk of it).
- **[Detection false positives]** → A stray `openspec/` directory (e.g. a cloned submodule unrelated to the project's spec workflow) would trigger the guidance. Mitigation: low harm — the worst case is guidance the agent doesn't need, which it can ignore. A tighter signal (e.g. `openspec/config.yaml`) can be added later if this actually bites.
- **[Users who never re-run `fleece install`]** → The existing hook still invokes `fleece prime` with no topic, so the new conditional injection runs without a hook re-install. No migration required.
- **[Tag collisions]** → Other tools could also use `openspec=` keyed tags. Mitigation: the tag key is descriptive enough that collisions are unlikely. If it becomes an issue, the convention can evolve (e.g. `openspec-change=`).
