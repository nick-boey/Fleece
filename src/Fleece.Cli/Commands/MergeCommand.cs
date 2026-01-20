using Fleece.Cli.Output;
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

        var conflicts = await mergeService.FindAndResolveDuplicatesAsync();

        if (conflicts.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No duplicates found. Issues are already consolidated.[/]");
            return 0;
        }

        if (settings.Json)
        {
            JsonFormatter.RenderConflicts(conflicts);
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Merged {conflicts.Count} issue(s) with property-level resolution[/]");
            AnsiConsole.WriteLine();
            TableFormatter.RenderConflicts(conflicts);

            if (!settings.DryRun)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[green]Merge complete! Issues consolidated into single file.[/]");
                AnsiConsole.MarkupLine("[dim]Conflict details logged to conflicts.jsonl[/]");
            }
        }

        return 0;
    }
}
