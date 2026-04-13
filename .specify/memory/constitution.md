<!--
SYNC IMPACT REPORT
==================
Version change: [none] → 1.0.0 (initial ratification)
Source: Extracted from CLAUDE.md (project instructions)

Modified principles: N/A (initial adoption)
Added sections:
  - Core Principles (I. Core-First Architecture, II. Thin CLI Wrappers,
    III. Complete & Reusable Core API, IV. Test Coverage for Core Logic,
    V. Local-First Storage Integrity)
  - Data & Storage Standards
  - Development Workflow
  - Governance

Removed sections: N/A (initial adoption)

Templates requiring updates:
  - ✅ .specify/templates/plan-template.md — "Constitution Check" placeholder
    remains generic ("[Gates determined based on constitution file]") and is
    compatible with the principles defined here; concrete gates (Core-first,
    thin CLI, test coverage, storage invariants) will be injected by the
    /speckit.plan command when it reads this file. No template edit required.
  - ✅ .specify/templates/spec-template.md — no constitution-specific sections
    needed; generic requirements template is compatible.
  - ✅ .specify/templates/tasks-template.md — generic task phasing is
    compatible; principles impose no mandatory task categories beyond what
    plans will surface from this file.
  - ✅ .specify/templates/commands/* — directory not present; no action.
  - ✅ CLAUDE.md — remains the operational guide and the source this
    constitution was derived from; no divergence introduced.

Follow-up TODOs: None. Ratification date is set to today because CLAUDE.md
  is the first codified set of principles for this project and no earlier
  adoption date is recorded.
-->

# Fleece Constitution

## Core Principles

### I. Core-First Architecture

All business logic MUST live in `Fleece.Core` (`src/Fleece.Core/`). `Fleece.Core`
is the authoritative library for issue, tombstone, storage, clean, and related
domain behavior, and it is designed for external consumption (e.g., by Homespun)
independent of the CLI. Any new capability MUST be introduced in Core first and
then exposed through consumers.

**Rationale:** Fleece is a local-first issue tracking system with multiple
current and future consumers. Centralizing logic in Core keeps behavior
consistent across consumers, prevents CLI-only features that break external
integrations, and keeps the CLI replaceable.

### II. Thin CLI Wrappers (NON-NEGOTIABLE)

`Fleece.Cli` commands (`src/Fleece.Cli/Commands/`) MUST remain thin adapters.
They MUST:

1. Parse and validate command-line arguments (via `Settings/*`).
2. Map arguments to Core API parameters.
3. Call Core service methods (`IIssueService`, `IStorageService`,
   `ICleanService`, etc.).
4. Format and display results (table or JSON via `Output/` formatters).

They MUST NOT contain business logic, implement filtering or searching
(use `IIssueService.FilterAsync` / `SearchAsync`), or directly manipulate issue
data. Any logic that would otherwise live in a command MUST be added to a Core
service first and invoked from the command.

**Rationale:** Divergent logic between the CLI and other consumers is the
primary risk when Core is bypassed. A strict thin-wrapper rule makes that
divergence impossible by construction.

### III. Complete & Reusable Core API

When adding features, the order is fixed:

1. Add the capability to a Core service interface
   (`src/Fleece.Core/Services/Interfaces/`).
2. Implement it in the corresponding Core service
   (`src/Fleece.Core/Services/`).
3. Only then expose it through CLI commands and settings.

The Core API MUST be complete enough for external consumers to use directly
without shelling out to the CLI. Filtering, searching, listing, creation,
editing, cleaning, merging, and similar behaviors MUST be reachable purely
through Core service calls.

**Rationale:** External consumers (such as Homespun) depend on Core; a CLI-only
capability silently breaks them. Making the Core API the first stop also keeps
the CLI surface honest — it only exposes what Core already supports.

### IV. Test Coverage for Core Logic

Every new Core capability MUST be accompanied by unit tests in
`tests/Fleece.Core.Tests/`. This includes new filter parameters, new service
methods, and new branches in existing methods. Tests MUST be runnable via
`dotnet test` and MUST pass before a change is considered complete. Adding a
new filter option, for example, is not complete until tests in
`IssueServiceTests` exercise it.

**Rationale:** Core is shared infrastructure; regressions there propagate to
every consumer. Locking behavior down with tests at the Core layer is the
cheapest and most durable protection.

### V. Local-First Storage Integrity

Fleece stores issues in JSONL files (`issues_{hash}.jsonl`) and tombstones
in `tombstones_{hash}.jsonl` under a repository's `.fleece/` folder. The
following invariants MUST hold:

- **Tombstones preserve history.** `fleece clean` MUST write a tombstone
  record containing `IssueId`, `OriginalTitle`, `CleanedAt`, and `CleanedBy`
  for every removed issue. Tombstone files MUST be merged alongside issue
  files during `fleece merge`.
- **ID uniqueness across live and tombstoned issues.** IDs are random GUIDs
  (first 5 bytes, Base62-encoded to 6 characters). `CreateAsync` MUST retry
  with a new random ID on collision with any existing issue *or* tombstone,
  up to 10 attempts, before failing.
- **Reference hygiene.** By default, `clean` MUST strip dangling
  `LinkedIssues` and `ParentIssues` references from remaining issues;
  `--no-strip-refs` is the only supported escape hatch.
- **Scope of cleaning.** `clean` removes `Deleted` issues by default;
  `--include-complete`, `--include-closed`, and `--include-archived` are
  the supported extensions. New statuses MUST NOT be swept implicitly.
- **Clean logic lives in Core.** `ICleanService` / `CleanService` owns
  all clean behavior; `CleanCommand` stays a thin wrapper.

**Rationale:** Local-first storage with offline merges is Fleece's
differentiator. These invariants prevent data loss, ID reuse across
history, and broken references after cleanup — all of which would
silently corrupt users' issue tracking.

## Data & Storage Standards

- **File layout.** Issue and tombstone data MUST live under `.fleece/` at
  the repository root, in the `{kind}_{hash}.jsonl` format. Consumers MUST
  go through `IStorageService` (or higher-level services) rather than
  reading or writing files directly.
- **Serialization.** JSON serialization MUST use the serializer and
  conventions defined in `src/Fleece.Core/` so that CLI, library consumers,
  and `fleece merge` all read and write identical shapes.
- **Merge safety.** Merges MUST be resolvable via `fleece merge`; manual
  deletion of conflicting `.fleece/` files is prohibited as a resolution
  strategy.
- **Status workflow.** Issue status transitions follow
  `open → progress → review → complete`, with `archived` and `closed`
  as terminal off-ramps, and `Deleted` reserved for soft deletion prior
  to `fleece clean`. New statuses MUST be added to Core models and
  exercised by tests before any CLI flag surfaces them.

## Development Workflow

- **Adding a new filter option** (canonical example):
  1. Add the parameter to `IIssueService.FilterAsync`.
  2. Implement the filter in `IssueService.FilterAsync`.
  3. Add the CLI option to the relevant `Settings` classes
     (e.g., `ListSettings`, `TreeSettings`).
  4. Pass the option through from the command to `FilterAsync`.
  5. Add unit tests to `IssueServiceTests`.
- **Build and test commands.** `dotnet build` builds the solution.
  `dotnet test` runs all tests. `dotnet test tests/Fleece.Core.Tests`
  runs Core tests only. A change is not complete until the relevant
  build and tests pass locally.
- **File locations (authoritative).**

  | Purpose | Location |
  |---------|----------|
  | Core service interfaces | `src/Fleece.Core/Services/Interfaces/` |
  | Core service implementations | `src/Fleece.Core/Services/` |
  | CLI commands | `src/Fleece.Cli/Commands/` |
  | CLI settings | `src/Fleece.Cli/Settings/` |
  | Core unit tests | `tests/Fleece.Core.Tests/` |

- **Complexity gate.** Additional projects, layers, or abstractions
  beyond Core + CLI + Tests MUST be justified in a plan's
  Complexity Tracking table against a simpler alternative.

## Governance

1. **Supremacy.** This constitution supersedes ad hoc practices. Where
   CLAUDE.md (or any other guidance document) and this constitution
   disagree, this constitution wins; the other document MUST be updated
   to match in the same change.
2. **Amendments.** Amendments MUST be made via a PR that (a) edits this
   file, (b) updates the version per the policy below, (c) updates
   `Last Amended`, and (d) propagates changes to
   `.specify/templates/plan-template.md`,
   `.specify/templates/spec-template.md`,
   `.specify/templates/tasks-template.md`, and any runtime guidance
   (e.g., `CLAUDE.md`, `README.md`) affected by the change.
3. **Versioning policy.** Semantic versioning applies:
   - **MAJOR** — backward-incompatible removal or redefinition of a
     principle or governance rule.
   - **MINOR** — a new principle or section, or materially expanded
     guidance within an existing one.
   - **PATCH** — clarifications, wording, typos, non-semantic
     refinements.
4. **Compliance reviews.** Every PR and code review MUST verify that
   changes do not violate the Core Principles. The
   "Constitution Check" gate in `.specify/templates/plan-template.md`
   MUST be satisfied before Phase 0 of any plan proceeds, and
   re-checked after Phase 1 design.
5. **Runtime guidance.** Day-to-day development guidance (how to add a
   filter, where files live, how to run tests) lives in `CLAUDE.md`
   and is kept in sync with this constitution.

**Version**: 1.0.0 | **Ratified**: 2026-04-14 | **Last Amended**: 2026-04-14
