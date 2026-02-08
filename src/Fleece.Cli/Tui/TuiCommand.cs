using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Tui;

/// <summary>
/// Default command that launches the interactive TUI when fleece is run with no arguments.
/// </summary>
public sealed class TuiCommand(
    IIssueService issueService,
    IStorageService storageService,
    INextService nextService,
    IChangeService changeService,
    ITaskGraphService taskGraphService,
    IGitConfigService gitConfigService) : AsyncCommand<TuiSettings>
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

        var runner = new TuiRunner(
            issueService, storageService, nextService,
            changeService, taskGraphService, gitConfigService,
            AnsiConsole.Console);
        return await runner.RunAsync();
    }
}
