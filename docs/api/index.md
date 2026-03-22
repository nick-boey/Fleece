# API Reference

This section contains the programmatic API documentation for Fleece.

## Namespaces

### Fleece.Core

The core library containing all business logic. Use this package to integrate Fleece into your own applications.

| Namespace | Description |
|-----------|-------------|
| `Fleece.Core.Models` | Data models including `Issue`, `Tombstone`, and enums |
| `Fleece.Core.Services` | Service implementations for issue management |
| `Fleece.Core.Services.Interfaces` | Service interfaces for dependency injection |

### Fleece.Cli

The command-line interface application. This package is typically used as a dotnet tool rather than referenced directly.

| Namespace | Description |
|-----------|-------------|
| `Fleece.Cli.Commands` | CLI command implementations |
| `Fleece.Cli.Settings` | Command option definitions |
| `Fleece.Cli.Output` | Output formatters |

## Quick Example

```csharp
using Fleece.Core.Services;
using Fleece.Core.Models;

// Create services
var storageService = new StorageService("/path/to/repo/.fleece");
var issueService = new IssueService(storageService);

// Create an issue
var issue = await issueService.CreateAsync(
    title: "Implement feature X",
    type: IssueType.Feature,
    description: "Add support for feature X"
);

// Filter issues
var openBugs = await issueService.FilterAsync(
    status: IssueStatus.Open,
    type: IssueType.Bug
);

// Update an issue
await issueService.UpdateAsync(
    issue.Id,
    status: IssueStatus.Progress
);
```

## Package Installation

To use Fleece.Core in your project:

```bash
dotnet add package Fleece.Core
```

Browse the API documentation below for detailed information on all types and members.
