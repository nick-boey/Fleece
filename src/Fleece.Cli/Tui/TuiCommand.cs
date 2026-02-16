using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Tui;

/// <summary>
/// Default command that launches the interactive TUI when fleece is run with no arguments.
/// </summary>
public sealed class TuiCommand(
    IFleeceInMemoryService inMemoryService,
    IStorageService storageService) : AsyncCommand<TuiSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, TuiSettings settings)
    {
        // Check for multiple unmerged files (standard guard)
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        // Check if running in an interactive terminal
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            AnsiConsole.MarkupLine("[yellow]Non-interactive terminal detected.[/]");
            AnsiConsole.MarkupLine("The TUI requires an interactive terminal.");
            AnsiConsole.MarkupLine("Use [bold]fleece --help[/] to see available commands.");
            return 0;
        }

        var app = new TuiApp(inMemoryService);
        return await app.RunAsync();
    }
}
