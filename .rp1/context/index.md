# Fleece - Knowledge Base

**Type**: Single Project
**Languages**: C#, YAML, Markdown
**Version**: 3.1.3
**Updated**: 2026-06-04

## Project Summary

Fleece is a local-first, git-native issue tracking CLI tool that stores issues as JSONL files under `.fleece/` alongside your code. It uses an event-sourced storage model — append-only change files are replayed over a snapshot — so issue history survives squash-merges and multi-branch workflows. The CLI is a thin wrapper over `Fleece.Core`, which is also distributed as a library for programmatic use.

## Quick Reference

| Aspect | Value |
|--------|-------|
| Entry Point | `fleece` CLI (`src/Fleece.Cli/Program.cs`) |
| Key Pattern | Event Sourcing + Functional Core / Imperative Shell |
| Tech Stack | .NET 10, C#, Spectre.Console.Cli, System.Text.Json (AOT), NUnit |

## KB File Manifest

**Progressive Loading**: Load files on-demand based on your task.

| File | Lines | Load For |
|------|-------|----------|
| architecture.md | ~147 | System design, event-sourcing storage model, data flows, CI/CD |
| interaction-model.md | ~114 | CLI surfaces, UX principles, output modes, feedback loops |
| modules.md | ~124 | Component breakdown, service responsibilities, dependency graph |
| patterns.md | ~79 | Code conventions, DI, async, testing idioms |
| concept_map.md | ~114 | Domain terminology, event sourcing concepts, cross-references |

## Task-Based Loading

| Task | Files to Load |
|------|---------------|
| Code review | `patterns.md` |
| Bug investigation | `architecture.md`, `modules.md` |
| Feature implementation | `modules.md`, `patterns.md` |
| CLI / UX / output work | `interaction-model.md`, `modules.md`, `patterns.md` |
| Event-sourcing work | `architecture.md`, `modules.md`, `concept_map.md` |
| Strategic analysis | ALL files |

## How to Load

```
Read: .rp1/context/{filename}
```

## Project Structure

```
Fleece/
├── src/
│   ├── Fleece.Core/          # Library: all business logic, models, event-sourcing
│   │   ├── Services/         # FleeceService facade, adapters, git, settings, layout
│   │   ├── EventSourcing/    # EventStore, ReplayEngine, ProjectionService, LinkService
│   │   ├── FunctionalCore/   # Pure static functions: Issues, Dependencies, Cleaning, Tags
│   │   ├── Models/           # Issue, Tombstone, IssueGraph, IssueEvent, DTOs
│   │   ├── Search/           # SearchQueryParser
│   │   ├── Serialization/    # AOT source-gen JSON contexts
│   │   └── Utilities/        # LexoRank
│   └── Fleece.Cli/           # CLI tool: thin wrapper over Core
│       ├── Commands/         # One command class per CLI command
│       ├── Settings/         # Spectre.Console settings (flags) per command
│       ├── Interceptors/     # AutoMigrateInterceptor, AutoMergeInterceptor
│       └── Output/           # TableFormatter, TreeRenderer, TaskGraphRenderer
├── tests/
│   ├── Fleece.Core.Tests/          # Unit tests for Core services + FunctionalCore
│   ├── Fleece.Cli.Tests/           # DI composition + command-resolution checks
│   ├── Fleece.Cli.E2E.Tests/       # In-process scenarios with MockFileSystem
│   └── Fleece.Cli.Integration.Tests/ # Real disk + real git [NonParallelizable]
├── .fleece/                  # Issue storage (committed alongside code)
└── openspec/                 # OpenSpec change artifacts
```

## Navigation

- **[architecture.md](architecture.md)**: System design, event-sourcing model, data flows, CI/CD integrations
- **[interaction-model.md](interaction-model.md)**: CLI surfaces, output modes, UX feedback loops, AI integration
- **[modules.md](modules.md)**: Component breakdown, service responsibilities, dependency graph
- **[patterns.md](patterns.md)**: Code conventions, DI, async patterns, testing idioms
- **[concept_map.md](concept_map.md)**: Domain terminology, event sourcing concepts, cross-references

---
*Note: This is a passive KB build — no Arcade run registered. Agents load KB automatically; no manual `knowledge-load` needed.*
