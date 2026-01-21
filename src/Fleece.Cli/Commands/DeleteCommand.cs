using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class DeleteCommand(IIssueService issueService, IStorageService storageService) : AsyncCommand<DeleteSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeleteSettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        var deleted = await issueService.DeleteAsync(settings.Id);

        if (deleted)
        {
            AnsiConsole.MarkupLine($"[green]Deleted issue[/] [bold]{settings.Id}[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
            return 1;
        }
    }
}
