# Filesystem Access

## Purpose

Defines the architectural invariant that all `Fleece.Core` filesystem I/O flows through an injected `IFileSystem` (from `Testably.Abstractions`). This enables fast in-memory testing via `MockFileSystem`, isolates Core from OS quirks, and prevents future contributors from silently reintroducing direct `System.IO` calls.

## Requirements

### Requirement: Core services SHALL access the filesystem exclusively through IFileSystem

All `Fleece.Core` services that read, write, enumerate, or watch files SHALL do so through an injected `IFileSystem` (from `Testably.Abstractions`). No Core service SHALL call `System.IO.File`, `System.IO.Directory`, `System.IO.Path`, or construct `System.IO.FileSystemWatcher` directly. The only permitted exceptions are code paths that shell out to external processes (e.g. the git CLI) where a real on-disk working directory is inherent to the operation.

#### Scenario: Core service operates under a MockFileSystem
- **WHEN** a test registers `AddFleeceCore(basePath, new MockFileSystem())` and performs a round-trip (create issue, load issues)
- **THEN** all writes and reads resolve against the mock instance
- **AND** no file is created on the real disk
- **AND** the round-trip returns the same records written

#### Scenario: New Core code adding direct System.IO usage is rejected
- **WHEN** a developer introduces a `System.IO.File.*`, `Directory.*`, or `new FileSystemWatcher(...)` call in a non-excluded Core service
- **THEN** the audit check in `tasks.md` item 2.7 (and its CI equivalent) surfaces the call
- **AND** the change is expected to route through `IFileSystem` instead

### Requirement: AddFleeceCore SHALL default to a real filesystem when none is injected

`ServiceCollectionExtensions.AddFleeceCore` SHALL register an `IFileSystem` implementation backed by the real OS filesystem when no instance is supplied by the caller, preserving existing behaviour for production consumers.

#### Scenario: Default registration resolves to real filesystem
- **WHEN** a caller invokes `services.AddFleeceCore(basePath)` without providing `IFileSystem`
- **THEN** `serviceProvider.GetRequiredService<IFileSystem>()` returns an instance backed by real `System.IO`
- **AND** subsequent file operations hit the real disk

#### Scenario: Caller-supplied filesystem is honoured
- **WHEN** a caller invokes `services.AddFleeceCore(basePath, fileSystem: mockFs)`
- **THEN** `serviceProvider.GetRequiredService<IFileSystem>()` returns the same `mockFs` instance
- **AND** no default real-filesystem registration overrides it
