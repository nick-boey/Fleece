## 1. Dependency + DI plumbing

- [x] 1.1 Add `Testably.Abstractions` to `src/Fleece.Core/Fleece.Core.csproj` and `Testably.Abstractions.Testing` to both test projects.
- [x] 1.2 Extend `AddFleeceCore` with optional `IFileSystem fileSystem = null` parameter and register it as a singleton, defaulting to `new RealFileSystem()`.
- [x] 1.3 Forward the parameter through `AddFleeceInMemoryService`.

## 2. Core service migrations (one commit per bullet for reviewability)

- [x] 2.1 Migrate `SettingsService` to take `IFileSystem` via ctor and route all `File/Directory/Path` calls through it.
- [x] 2.2 Migrate `JsonlStorageService` and its helpers in `src/Fleece.Core/Serialization/` (including `JsonlSerializer` stream acquisition). `JsonlSerializer` operates on strings only — no stream acquisition changes needed. `SingleFileStorageService` and `DiffService` migrated as well.
- [x] 2.3 Migrate `GitConfigService` (reads `.git/config`). Current implementation shells out via `git config` — no direct `.git/config` file access. Per design doc escape hatch, no migration required.
- [x] 2.4 Migrate `SyncStatusService`.
- [x] 2.5 Migrate `CleanService`. `CleanService` no longer exists as a standalone class; clean logic lives in `FleeceService.CleanAsync`, which already accesses the filesystem only via the injected `IStorageService`. No direct filesystem calls to migrate.
- [x] 2.6 Migrate `FleeceInMemoryService`, including `FileSystemWatcher` → `IFileSystemWatcher` via `fileSystem.FileSystemWatcher.New(...)`.
- [x] 2.7 Audit: `grep -rn "System\.IO\.File\|Directory\.\|Path\." src/Fleece.Core/` returns only expected exclusions (git shell-out paths, logging, tests).

## 3. Verification

- [x] 3.1 Add a Core unit test that builds `AddFleeceCore(tmp, new MockFileSystem())`, creates an issue via `IFleeceService`, and asserts the JSONL record lands in the mock filesystem (not on disk).
- [x] 3.2 Re-run full test suite — existing tests continue to pass against the default real filesystem. (One pre-existing SettingsServiceTests failure unrelated to this change — test reads the real `~/.fleece/settings.json` on the dev machine.)
- [x] 3.3 Smoke-run the built CLI (`fleece list --json`) against the repo to confirm no regression.
