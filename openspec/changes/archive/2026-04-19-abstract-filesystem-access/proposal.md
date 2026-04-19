## Why

Every `Fleece.Core` service that touches disk calls `System.IO.File`, `Directory`, `Path`, or `FileSystemWatcher` directly. That couples the entire library to real I/O, blocks fast in-memory E2E testing, and lets OS quirks (case sensitivity on macOS vs Linux, path-separator handling, permissions) bleed into test runs. It also prevents the upcoming CLI scenario matrix from running in parallel without per-test temp directories.

Introducing an `IFileSystem` abstraction now is a mechanical refactor that unlocks the console abstraction and the E2E scenario suite without rework later.

## What Changes

- Add `Testably.Abstractions` NuGet package (`Testably.Abstractions` + `Testably.Abstractions.Testing`) to `Fleece.Core` and the test projects.
- Register `IFileSystem` in `AddFleeceCore` (defaulting to `RealFileSystem`), with an overload accepting a user-supplied instance so tests can inject `MockFileSystem`.
- Replace every static `System.IO.File.*`, `Directory.*`, and `Path.*` call in Core services with equivalent calls through an injected `IFileSystem`. Path manipulation (`Path.Combine`, `Path.GetFileName`) routes through `IFileSystem.Path`.
- Replace `FileSystemWatcher` usage in `FleeceInMemoryService` with `IFileSystemWatcherFactory` from the abstraction.
- Keep `GitService` out of scope — it shells out to git and continues to assume a real filesystem. Git-dependent tests will use a real temp directory (covered by the `add-cli-e2e-scenario-tests` change).

No user-visible CLI behaviour change. No public Core API breakage other than the optional constructor parameter and DI registration gaining an overload.

## Capabilities

### New Capabilities
- `filesystem-access`: defines the architectural invariant that all Core filesystem I/O flows through `IFileSystem`, so future contributors can't silently reintroduce direct `System.IO` calls.

### Modified Capabilities
<!-- none -->

## Impact

- **Code**: every file in `src/Fleece.Core/Services/*.cs` that reads or writes the filesystem — `JsonlStorageService`, `SettingsService`, `GitConfigService`, `SyncStatusService`, `CleanService`, `FleeceInMemoryService`, and their supporting helpers in `Serialization/`.
- **DI**: `ServiceCollectionExtensions.AddFleeceCore` gains an optional `IFileSystem` parameter; `AddFleeceInMemoryService` forwards it.
- **Tests**: `Fleece.Core.Tests` can migrate to `MockFileSystem` incrementally. Any test that currently writes to a temp directory can keep doing so — the real `IFileSystem` stays the default.
- **Dependencies**: adds `Testably.Abstractions` (permissive license, actively maintained, targets .NET 6+). No runtime behaviour change; only adds an abstraction layer.
- **External consumers** (e.g. Homespun) who call `AddFleeceCore()` without arguments are unaffected.
