## Context

All CLI commands currently call `AnsiConsole.MarkupLine`, `AnsiConsole.Write`, and friends directly. `AnsiConsole.Console` is a static singleton resolved from the process-global console. Existing tests either tolerate real console output or redirect `Console.SetOut` — both break under parallel execution.

Spectre.Console already exposes `IAnsiConsole` as an injectable interface. `Spectre.Console.Testing.TestConsole` implements it and captures output as structured segments that tests can assert on without fighting ANSI escape codes.

## Goals / Non-Goals

**Goals:**
- Every command resolves its `IAnsiConsole` from DI.
- CLI runs identically to today when the default registration (`AnsiConsole.Console`) is used.
- Parallel tests can each give their command a dedicated `TestConsole` with no shared state.
- Test assertions on stdout use the `TestConsole.Output` string, not `Console.Out` gymnastics.

**Non-Goals:**
- Changing output format, colours, or markup.
- Replacing `Spectre.Console.Cli` — `CommandApp` stays.
- Moving the static `AnsiConsole` calls inside `Spectre.Console.Cli` itself (those are internal to the library).

## Decisions

**Default registration in `CliComposition`.**

```csharp
services.AddSingleton<IAnsiConsole>(_ => AnsiConsole.Console);
```

Tests override with `services.AddSingleton<IAnsiConsole>(_ => new TestConsole())` before building the provider.

**Constructor injection per command.**

```csharp
public sealed class ListCommand(
    IFleeceService fleeceService,
    ISettingsService settingsService,
    IAnsiConsole console) : AsyncCommand<ListSettings>
{
    public override async Task<int> ExecuteAsync(...)
    {
        console.MarkupLine($"[red]Error:[/] ...");
        ...
    }
}
```

Commands with inherited bases (`StatusCommandBase`) take `IAnsiConsole` once at the base and pass through.

**Output helpers.** Anything under `src/Fleece.Cli/Output/` that writes directly (table formatters, JSON printers) takes `IAnsiConsole` instead of referencing the static.

**Interceptor.** `AutoMergeInterceptor` writes via the static singleton today. It moves to resolving `IAnsiConsole` through the same lazy service-provider factory already in place.

**Spectre integration.** `CommandApp` resolves commands via `ITypeResolver`, so no special wiring is needed — the DI container already provides the right instance to each command constructor.

## Risks / Trade-offs

- **Widespread diff.** Every command touches — but each change is local and mechanical. Commit one command at a time for clean review.
- **Static leakage.** Any missed call to `AnsiConsole.*` still writes to the global sink, leaking cross-test output. Mitigation: after the refactor, a ripgrep check for `AnsiConsole\.` in `src/Fleece.Cli/` should return zero hits outside the `CliComposition` default registration.
- **Test migration effort.** Existing command tests swap from `Console.SetOut(...)` to `new TestConsole()` injection. Net reduction in boilerplate.
- **Interceptor wiring.** `AutoMergeInterceptor` currently uses a lazy `Func<IServiceProvider>` — continue using the same pattern to resolve `IAnsiConsole`.
