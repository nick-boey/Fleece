# Architecture

Fleece is a local-first issue tracking system with a clean separation between its core library and CLI application.

## Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        Fleece.Cli                           │
│   ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐          │
│   │ Create  │ │  List   │ │  Edit   │ │ Search  │  ...     │
│   │ Command │ │ Command │ │ Command │ │ Command │          │
│   └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘          │
│        │           │           │           │                │
│        └───────────┴───────────┴───────────┘                │
│                         │                                   │
└─────────────────────────┼───────────────────────────────────┘
                          │
┌─────────────────────────┼───────────────────────────────────┐
│                   Fleece.Core                               │
│                         ▼                                   │
│   ┌─────────────────────────────────────────────────────┐  │
│   │                    Services                          │  │
│   │  ┌──────────────┐ ┌──────────────┐ ┌─────────────┐  │  │
│   │  │ IssueService │ │StorageService│ │ CleanService│  │  │
│   │  └──────────────┘ └──────────────┘ └─────────────┘  │  │
│   └─────────────────────────────────────────────────────┘  │
│   ┌─────────────────────────────────────────────────────┐  │
│   │                     Models                           │  │
│   │  ┌───────┐ ┌───────────┐ ┌───────────┐ ┌─────────┐  │  │
│   │  │ Issue │ │IssueStatus│ │ IssueType │ │Tombstone│  │  │
│   │  └───────┘ └───────────┘ └───────────┘ └─────────┘  │  │
│   └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## Components

### Fleece.Core (Library)

The core library (`src/Fleece.Core/`) contains all business logic and is designed for external consumption. If you want to integrate Fleece into your own application, this is the package to reference.

#### Services

| Interface | Implementation | Purpose |
|-----------|----------------|---------|
| `IIssueService` | `IssueService` | CRUD operations, filtering, searching |
| `IStorageService` | `StorageService` | JSONL file persistence |
| `ICleanService` | `CleanService` | Tombstone management, cleanup |

#### Models

- **`Issue`**: The primary entity representing a tracked issue
- **`Tombstone`**: Records of permanently deleted issues
- **`IssueStatus`**: Enum of valid statuses (Open, Progress, Review, Complete, etc.)
- **`IssueType`**: Enum of issue types (Task, Bug, Chore, Feature, Verify)

### Fleece.Cli (CLI Application)

The command-line interface (`src/Fleece.Cli/`) is a thin wrapper around Core APIs. It uses [Spectre.Console.Cli](https://spectreconsole.net/) for command parsing and output formatting.

#### Structure

| Directory | Purpose |
|-----------|---------|
| `Commands/` | Command implementations (CreateCommand, ListCommand, etc.) |
| `Settings/` | Command option definitions (CreateSettings, ListSettings, etc.) |
| `Output/` | Formatters for table and JSON output |

## Design Principles

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

### Core API Should Be Complete

When adding features:
1. First add the capability to Core services
2. Then expose it through CLI commands
3. External consumers can use Core directly

### Example: Filtering

The `list` command's filtering is implemented in `IIssueService.FilterAsync`, not in the CLI. This means any consumer of Fleece.Core gets the same filtering capabilities.

```csharp
// In CLI command
var issues = await issueService.FilterAsync(
    status: settings.Status,
    type: settings.Type,
    priority: settings.Priority
);

// The CLI just passes options through - no filtering logic here
```

## File Locations

| Purpose | Location |
|---------|----------|
| Core service interfaces | `src/Fleece.Core/Services/Interfaces/` |
| Core service implementations | `src/Fleece.Core/Services/` |
| CLI commands | `src/Fleece.Cli/Commands/` |
| CLI settings | `src/Fleece.Cli/Settings/` |
| Core unit tests | `tests/Fleece.Core.Tests/` |

## Using Fleece.Core Programmatically

To use Fleece in your own .NET application:

```csharp
using Fleece.Core.Services;

// Create services
var storageService = new StorageService("/path/to/repo/.fleece");
var issueService = new IssueService(storageService);

// Create an issue
var issue = await issueService.CreateAsync(
    title: "My new issue",
    type: IssueType.Task,
    description: "Description here"
);

// List issues
var issues = await issueService.FilterAsync(status: IssueStatus.Open);

// Update an issue
await issueService.UpdateAsync(issue.Id, status: IssueStatus.Progress);
```

## Tombstones and Cleanup

The `fleece clean` command permanently removes soft-deleted issues and creates tombstone records.

Key details:
- **Tombstone records** store `IssueId`, `OriginalTitle`, `CleanedAt`, and `CleanedBy`
- **ID collision prevention**: When creating an issue would collide with a tombstoned ID, Fleece automatically salts the title hash
- **Reference stripping**: By default, `clean` removes dangling references from remaining issues

## Testing

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Fleece.Core.Tests
```
