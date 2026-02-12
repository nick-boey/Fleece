using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class MergeCommand(IMergeService mergeService) : AsyncCommand<MergeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, MergeSettings settings)
    {
        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]Dry run mode - no changes will be made[/]");
        }

        var mergedCount = await mergeService.FindAndResolveDuplicatesAsync(settings.DryRun);

        if (mergedCount == 0)
        {
            AnsiConsole.MarkupLine("[green]No duplicates found. Issues are already consolidated.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[yellow]Merged {mergedCount} issue(s) with property-level resolution[/]");

        if (!settings.DryRun)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]Merge complete! Issues consolidated into single file.[/]");
        }

        return 0;
    }
}
