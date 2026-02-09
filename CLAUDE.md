# CLAUDE.md

This file provides guidance for AI assistants working on the Fleece codebase.

## Project Architecture

Fleece is a local-first issue tracking system with two main components:

### Fleece.Core (Library)
The core library (`src/Fleece.Core/`) contains all business logic and is designed for external consumption:
- **Services**: `IIssueService`, `IStorageService`, `IChangeService`, etc.
- **Models**: `Issue`, `ChangeRecord`, enums like `IssueStatus`, `IssueType`
- **Serialization**: JSON handling for issue storage

### Fleece.Cli (CLI Application)
The command-line interface (`src/Fleece.Cli/`) is a thin wrapper around Core APIs:
- **Commands**: Each command (e.g., `ListCommand`, `CreateCommand`) delegates to Core services
- **Settings**: Command option definitions (e.g., `ListSettings`)
- **Output**: Formatters for table and JSON output

## Key Design Principles

### CLI Commands Should Be Thin Wrappers

CLI commands should:
1. Parse and validate command-line arguments
2. Map arguments to Core API parameters
3. Call Core service methods
4. Format and display results

CLI commands should NOT:
- Contain business logic (put it in Core)
- Implement filtering/searching (use Core's `FilterAsync`/`SearchAsync`)
- Directly manipulate issue data

**Example**: The `list` command's filtering is implemented in `IssueService.FilterAsync`, not in the CLI.

### Core API Should Be Complete

When adding features:
1. First add the capability to Core services
2. Then expose it through CLI commands
3. External consumers (like Homespun) can use Core directly

## Common Tasks

### Adding a New Filter Option

1. Add parameter to `IIssueService.FilterAsync` interface
2. Implement filtering logic in `IssueService.FilterAsync`
3. Add CLI option to relevant Settings classes (e.g., `ListSettings`, `TreeSettings`)
4. Pass the option to `FilterAsync` in the command
5. Add unit tests in `IssueServiceTests`

### Testing

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Fleece.Core.Tests
```

### Clean Command and Tombstones

The `fleece clean` command permanently removes soft-deleted issues (status `Deleted`) from `issues_{hash}.jsonl` and writes tombstone records to `tombstones_{hash}.jsonl`. Tombstone files are merged alongside issue and change files during `fleece merge`.

Key details:
- **Tombstone records** store `IssueId`, `CleanedAt`, and `CleanedBy`. Only the ID is needed for collision detection since IDs are immutable after creation.
- **ID collision prevention**: When `CreateAsync` generates an ID that matches a tombstoned issue, it retries with an incrementing salt (appended to the normalized title before hashing), up to 10 attempts.
- **Reference stripping**: By default, `clean` removes dangling `LinkedIssues` and `ParentIssues` references from remaining issues. Use `--no-strip-refs` to skip this.
- **Change record cleanup**: All `ChangeRecord` entries for cleaned issue IDs are removed.
- **Optional flags**: `--include-complete`, `--include-closed`, `--include-archived` extend cleaning beyond just `Deleted` issues.
- **Core service**: `ICleanService` / `CleanService` contains all business logic. The CLI `CleanCommand` is a thin wrapper.

## File Locations

| Purpose | Location |
|---------|----------|
| Core service interfaces | `src/Fleece.Core/Services/Interfaces/` |
| Core service implementations | `src/Fleece.Core/Services/` |
| CLI commands | `src/Fleece.Cli/Commands/` |
| CLI settings | `src/Fleece.Cli/Settings/` |
| Core unit tests | `tests/Fleece.Core.Tests/` |
