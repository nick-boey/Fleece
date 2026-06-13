# Implementation Patterns

**Project**: Fleece
**Last Updated**: 2026-06-04

## Naming & Organization

**Files**: PascalCase; interfaces prefixed `I`; commands suffixed `Command`; settings suffixed `Settings`
**Functions**: `Async` suffix on all async methods; verb-noun (`BuildGraph`, `ApplyEvent`, `ValidateTags`); private helpers descriptive (`LoadAndNormalizeAsync`, `ResolveActiveOrRotateAsync`)
**Imports**: Absolute namespace paths; grouped System → Microsoft/Spectre → internal Fleece; no `using` aliases
**Directories**: Feature-layer hybrid — `Services/`, `EventSourcing/Services/`, `FunctionalCore/`, `Models/`, `Commands/`

Evidence: `src/Fleece.Core/Services/FleeceService.cs`, `src/Fleece.Core/FunctionalCore/Issues.cs`

## Type & Data Modeling

**Data Representation**: Sealed records with `required init`-only properties for domain models (`Issue`, `Tombstone`); sealed classes for services; abstract records for event hierarchy (`IssueEvent`)
**Type Strictness**: `required` keyword on all non-optional fields; nullable reference types throughout; `IReadOnlyList<T>` for all collection returns
**Immutability**: Records + `with`-expressions for mutation (`existing with { Status = ..., LastUpdate = now }`); `IReadOnlyList<T>` / `IReadOnlyDictionary<K,V>` for all public collection surfaces
**Nullability**: Nullable reference types enabled; optional params use `T?`; `StringComparison.OrdinalIgnoreCase` consistently

Evidence: `src/Fleece.Core/Models/Issue.cs`, `src/Fleece.Core/FunctionalCore/Cleaning.cs`

## Error Handling

**Strategy**: Exceptions only — no `Result`/`Either`; `ArgumentException` for invalid inputs, `KeyNotFoundException` for missing entities, `InvalidOperationException` for business rule violations
**Propagation**: Validate at service boundary before acquiring locks; bubble up to CLI layer which renders `[red]Error:[/]`
**Recovery**: ID collision → retry loop up to 10 attempts; file flush tolerance via `try/catch` on `FlushAsync`

Evidence: `src/Fleece.Core/Services/FleeceService.cs:80-110`, `src/Fleece.Cli/Commands/ListCommand.cs:21-26`

## Validation & Boundaries

**Location**: Service layer before lock acquisition; CLI validates enum parsing and mutually exclusive flags before calling services
**Method**: `ArgumentException.ThrowIfNullOrWhiteSpace` for required strings; static `ValidateBranchName`/`ValidateTags` at top of create/update; `[GeneratedRegex]` partial methods for branch name patterns
**Normalization**: Tags validated via `Tags.ValidateTags()`; sort orders normalized lazily via `Issues.NormalizeSortOrders()` on every load; LexoRank assigned to missing `SortOrder` on first use

Evidence: `src/Fleece.Core/Services/FleeceService.cs:80-83`, `src/Fleece.Core/FunctionalCore/Tags.cs`

## Observability

**Logging**: None in Core; warnings surfaced via `IWarningSink` interface (`ConsoleWarningSink`, `NullWarningSink`); diagnostic output rendered via Spectre.Console markup
**Metrics**: None detected
**Tracing**: None detected

Evidence: `src/Fleece.Core/EventSourcing/Services/Interfaces/IWarningSink.cs`

## Testing Idioms

**Organization**: Separate projects per layer — `Fleece.Core.Tests` (unit), `Fleece.Cli.Tests` (DI/composition), `Fleece.Cli.E2E.Tests` (in-process `MockFileSystem`), `Fleece.Cli.Integration.Tests` (real disk+git, `[NonParallelizable]`)
**Fixtures**: `IFileSystem` (Testably.Abstractions) injected into all services; `MockFileSystem` for E2E; `TestConsole` for Spectre output capture; `NullEventGitContext`/`NullWarningSink` as null-object sentinels
**Levels**: Unit dominant for Core; golden-file snapshots (`Verify.NUnit`) for stable CLI human output; `--json` output tested structurally, not via snapshots

Evidence: `tests/Fleece.Cli.E2E.Tests/CliScenarioTestBase.cs`, `tests/Fleece.Core.Tests/EventSourcing/EventStoreTests.cs`

## I/O & Integration

**File System**: `IFileSystem` (Testably.Abstractions) injected everywhere; `RealFileSystem` in production; `FileMode.Append + FileShare.Read` for concurrent-safe JSONL appends
**Serialization**: System.Text.Json with AOT source-gen contexts (`FleeceJsonContext`, `EventSourcingJsonContext`); `[JsonPolymorphicAttribute]` + `[JsonDerivedType]` for event discriminated unions; JSONL (one record per line) for all storage
**Storage Stack**: Layered snapshot + change files + replay; `EventSourcedStorageAdapter` bridges legacy `IStorageService`; `SemaphoreSlim` write lock in `EventStore` and `FleeceService`

Evidence: `src/Fleece.Core/EventSourcing/Services/EventStore.cs:270-297`, `src/Fleece.Core/Serialization/FleeceJsonContext.cs`

## Concurrency & Async

**Async Usage**: All service methods are `async Task<T>`; `CancellationToken` threaded through every public API; synchronous `ICommandInterceptor` bridged via `.GetAwaiter().GetResult()`
**Parallelism**: `SemaphoreSlim(1,1)` write lock in `EventStore` and `FleeceService`; reads are lock-free; `FunctionalCore` statics are purely synchronous and side-effect-free
**Safety**: `ConcurrentDictionary` in `FleeceInMemoryService`; `ReaderWriterLockSlim` for cache read/write separation

Evidence: `src/Fleece.Core/EventSourcing/Services/EventStore.cs:25`, `src/Fleece.Core/Services/FleeceService.cs:23`

## Dependency & Configuration

**DI Pattern**: Constructor injection; `internal` constructors on service implementations; static factory methods (`FleeceService.ForFile`) for alternative wiring; `AddFleeceCore`/`AddFleeceInMemoryService` extension methods
**Config Loading**: `ISettingsService` — global `~/.fleece/settings.json` + local `.fleece/settings.json`; local overrides global; all properties nullable for partial overrides
**Initialization**: Lazy via `Func<IServiceProvider>` in `AutoMigrateInterceptor`; `IIssueSerializationQueue.StartProcessing()` called eagerly post-registration
**Architecture Rule**: Business logic in `FunctionalCore` or Core services; never in CLI commands

Evidence: `src/Fleece.Core/Extensions/ServiceCollectionExtensions.cs`, `src/Fleece.Cli/CliComposition.cs`
