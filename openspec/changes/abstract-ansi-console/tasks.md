## 1. DI registration

- [ ] 1.1 Register `IAnsiConsole` in `CliComposition.BuildServices` defaulting to `AnsiConsole.Console`.
- [ ] 1.2 Add `Spectre.Console.Testing` to `tests/Fleece.Cli.Tests/Fleece.Cli.Tests.csproj`.

## 2. Command refactor (one group per commit)

- [ ] 2.1 Migrate read-side commands: `ListCommand`, `ShowCommand`, `SearchCommand`, `NextCommand`, `DiffCommand`.
- [ ] 2.2 Migrate write-side commands: `CreateCommand`, `EditCommand`, `DeleteCommand`, `CleanCommand`, `MergeCommand`, `MigrateCommand`.
- [ ] 2.3 Migrate workflow commands: `CommitCommand`, `DependencyCommand`, `MoveCommand`, `ValidateCommand`, `InstallCommand`, `PrimeCommand`, `ConfigCommand`.
- [ ] 2.4 Migrate status commands: `StatusCommandBase` and subclasses (`OpenCommand`, `ProgressCommand`, `ReviewCommand`, `CompleteCommand`, `ArchivedCommand`, `ClosedCommand`).
- [ ] 2.5 Migrate output helpers in `src/Fleece.Cli/Output/`.
- [ ] 2.6 Migrate `AutoMergeInterceptor` to resolve `IAnsiConsole` through its existing service-provider factory.

## 3. Verification

- [ ] 3.1 `grep -rn "AnsiConsole\." src/Fleece.Cli/` returns only the default registration in `CliComposition`.
- [ ] 3.2 Update affected tests in `tests/Fleece.Cli.Tests/Commands/` to inject a `TestConsole` and assert on `TestConsole.Output` instead of redirecting `Console.Out`.
- [ ] 3.3 Existing `CliCompositionTests` still resolves every command (now with `IAnsiConsole` in the constructor).
- [ ] 3.4 `dotnet test` green; CLI smoke still passes (`dotnet run -- list --json`).
