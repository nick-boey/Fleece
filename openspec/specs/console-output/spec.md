# Console Output

## Purpose

Defines the architectural invariant that `Fleece.Cli` commands and helpers write output exclusively through an injected `IAnsiConsole` (from `Spectre.Console`) rather than the static `AnsiConsole.*` singleton. This enables parallel test isolation via `Spectre.Console.Testing.TestConsole` and structured output capture.

## Requirements

### Requirement: CLI commands SHALL write output exclusively through an injected IAnsiConsole

Every command class in `Fleece.Cli` and every helper it invokes SHALL obtain its output sink via constructor-injected `Spectre.Console.IAnsiConsole`. No command or helper SHALL call the static `Spectre.Console.AnsiConsole.*` APIs directly. The sole permitted direct reference to `AnsiConsole.Console` is the default registration inside `CliComposition.BuildServices`.

#### Scenario: Command writes through injected console
- **WHEN** a command handler emits output (e.g. `console.MarkupLine("[red]Error:[/] ...")`)
- **THEN** the output is routed to the `IAnsiConsole` instance supplied by the DI container
- **AND** replacing that registration with a `TestConsole` captures the output for assertions without touching `System.Console.Out`

#### Scenario: New direct AnsiConsole call is rejected
- **WHEN** a developer introduces a `Spectre.Console.AnsiConsole.*` call outside the permitted default registration
- **THEN** the ripgrep invariant in `tasks.md` item 3.1 (and any CI equivalent) surfaces the call

### Requirement: CliComposition SHALL register IAnsiConsole defaulting to the real console

`CliComposition.BuildServices` SHALL register `IAnsiConsole` as a singleton resolving to `AnsiConsole.Console` by default, preserving production CLI behaviour.

#### Scenario: Default registration returns real console
- **WHEN** `CliComposition.BuildServices()` is invoked without overrides
- **THEN** `serviceProvider.GetRequiredService<IAnsiConsole>()` returns `AnsiConsole.Console`
- **AND** commands emit to the terminal as before

#### Scenario: Test override substitutes TestConsole
- **WHEN** a test calls `services.AddSingleton<IAnsiConsole>(testConsole)` after `BuildServices` and before resolving commands
- **THEN** commands resolved from the container write exclusively to `testConsole`
- **AND** `testConsole.Output` contains the emitted markup
