## Why

Fleece CLI commands write to stdout via `Spectre.Console.AnsiConsole.MarkupLine(...)` and related static APIs. That singleton is per-process, so parallel tests cannot give each command its own `TestConsole`: they race on the global sink, interleave output, and fight over `Console.Out`. Any E2E scenario matrix that wants both parallel execution and stdout assertions has to either swap the singleton sequentially (`[NonParallelizable]`) or refactor to an injected console.

Injection is the right call. Spectre ships `Spectre.Console.Testing.TestConsole` for exactly this, and `IAnsiConsole` is already on the public API. The change is mechanical but enabling — without it, the E2E suite trades either speed or output assertions.

## What Changes

- Add `Spectre.Console.Testing` NuGet package to the CLI test projects.
- Register `IAnsiConsole` in the CLI composition (`CliComposition.BuildServices`) bound to `AnsiConsole.Console` by default.
- Give every command and helper that currently calls `AnsiConsole.*` a constructor-injected `IAnsiConsole` dependency and route output through it.
- Configure `CommandApp` so `context.AddCommand<T>()` resolves commands with the DI-provided console.
- Provide a test helper for building a DI graph where `IAnsiConsole` is a per-scope `TestConsole`.

No user-visible CLI change. Commands still write the same markup; only the sink is injectable.

## Capabilities

### New Capabilities
- `console-output`: defines the architectural invariant that CLI commands write output through an injected `IAnsiConsole` rather than the static singleton, enabling parallel test isolation and structured output capture.

### Modified Capabilities
<!-- none -->

## Impact

- **Code**: every file in `src/Fleece.Cli/Commands/` that references `AnsiConsole.*` (approximately every command), plus any helper in `src/Fleece.Cli/Output/` and `src/Fleece.Cli/Interceptors/`.
- **DI**: `CliComposition.BuildServices` gains an `IAnsiConsole` registration (defaults to the real singleton).
- **Tests**: existing command unit tests already mock the console by redirecting `Console.SetOut` — they migrate to `TestConsole` injection which is cleaner.
- **Dependencies**: `Spectre.Console.Testing` (test projects only). No production dependency additions.
- **Behaviour**: zero change. Output format and colour handling remain identical.
