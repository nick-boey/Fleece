using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class MergeCommand(IFleeceService fleeceService, IAnsiConsole console) : AsyncCommand<MergeSettings>
{
    internal const string DeprecationNotice =
        "warning: `fleece merge` is deprecated and will be removed in a future release. Use `fleece project` instead.";

    public override async Task<int> ExecuteAsync(CommandContext context, MergeSettings settings)
    {
        Console.Error.WriteLine(DeprecationNotice);

        if (settings.DryRun)
        {
            console.MarkupLine("[yellow]Dry run mode - no changes will be made[/]");
        }

        var mergedCount = await fleeceService.MergeAsync(settings.DryRun);

        if (mergedCount == 0)
        {
            console.MarkupLine("[green]No duplicates found. Issues are already consolidated.[/]");
            return 0;
        }

        console.MarkupLine($"[yellow]Merged {mergedCount} issue(s) with property-level resolution[/]");

        if (!settings.DryRun)
        {
            console.WriteLine();
            console.MarkupLine("[green]Merge complete! Issues consolidated into single file.[/]");
        }

        return 0;
    }
}
