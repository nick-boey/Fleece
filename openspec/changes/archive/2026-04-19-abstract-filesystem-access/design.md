## Context

`Fleece.Core` currently uses `System.IO` static types throughout. `SettingsService` reads `fleece.settings.json` via `File.ReadAllTextAsync`; `JsonlStorageService` enumerates `.fleece/*.jsonl` via `Directory.EnumerateFiles`; `FleeceInMemoryService` watches the directory via `FileSystemWatcher`. There is no seam for in-memory testing.

Two mature .NET libraries expose the same surface as `System.IO`:

- **`System.IO.Abstractions`** — older, widely used, targets .NET Framework. Limited `FileSystemWatcher` support.
- **`Testably.Abstractions`** — newer, .NET 6+, first-class `IFileSystemWatcher` abstraction, mock has behavioural parity with real filesystem including case sensitivity toggles.

## Goals / Non-Goals

**Goals:**
- Every Core service reads/writes disk exclusively through an injected `IFileSystem`.
- DI container returns a real filesystem by default so no production consumer changes.
- Tests can build a service provider with `MockFileSystem` and exercise real service code with zero disk I/O.
- `FileSystemWatcher` usage swaps cleanly to `IFileSystemWatcher`.

**Non-Goals:**
- Abstracting `Process.Start` / git shell-outs — `GitService` continues to assume a real filesystem.
- Abstracting HTTP, env vars, or clocks.
- Migrating every existing Core unit test to `MockFileSystem` in this change (opt-in as tests are touched).

## Decisions

**Library: `Testably.Abstractions`.** Modern API, first-class watcher support, mock handles async correctly. Dependency is permissive-licensed and has an active maintenance cadence.

**Registration shape.** Extend `AddFleeceCore` with an optional parameter:

```csharp
public static IServiceCollection AddFleeceCore(
    this IServiceCollection services,
    string? basePath = null,
    IFileSystem? fileSystem = null)
{
    ...
    services.AddSingleton<IFileSystem>(fileSystem ?? new RealFileSystem());
    ...
}
```

`AddFleeceInMemoryService` forwards the argument. Tests call `AddFleeceCore(tmpPath, new MockFileSystem())`.

**Path manipulation.** Use `IFileSystem.Path.Combine/GetFileName/GetDirectoryName` instead of static `Path.*`. Slight verbosity cost; ensures MockFileSystem's chosen OS semantics apply consistently.

**Watcher migration.** `FleeceInMemoryService` takes `IFileSystem` in its ctor and resolves `fileSystem.FileSystemWatcher.New(...)` where it currently does `new FileSystemWatcher(...)`.

**Real-FS escape hatch.** `GitService` and anything touching `Process.Start` keep using `System.IO` directly. Their constructors remain unchanged.

## Risks / Trade-offs

- **Diff size.** Dozens of call sites. Mitigated by breaking the refactor into per-service commits (one service per commit, easy to review and bisect).
- **Missed call sites.** A stray `File.Exists(...)` compiles fine. Mitigated by a post-refactor check: `grep -rn "System.IO\.File\|Directory\.\|new FileSystemWatcher"` in `Fleece.Core/` should return only expected holdouts (git shell-out paths). A unit test asserting all public services can be resolved with a `MockFileSystem` and perform a no-op round trip catches most remaining static calls at runtime.
- **Perf.** `IFileSystem` calls are one virtual dispatch each. Negligible for CLI workloads.
- **Serialization.** `JsonlSerializer` uses `JsonSerializer` with streams — stays unchanged; the stream is obtained from `IFileSystem.File.Open*` instead of `File.Open*`.
