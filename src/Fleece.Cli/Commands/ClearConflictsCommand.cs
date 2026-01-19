using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class ClearConflictsCommand(IConflictService conflictService) : AsyncCommand<ClearConflictsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ClearConflictsSettings settings)
    {
        var cleared = await conflictService.ClearByIssueIdAsync(settings.Id);

        if (cleared)
        {
            AnsiConsole.MarkupLine($"[green]Cleared conflicts for issue[/] [bold]{settings.Id}[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[dim]No conflicts found for issue '{settings.Id}'[/]");
            return 0;
        }
    }
}
