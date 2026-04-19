using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class CleanCommand(IFleeceService fleeceService, IAnsiConsole console) : AsyncCommand<CleanSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CleanSettings settings)
    {
        var (hasMultiple, message) = await fleeceService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            console.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        if (settings.DryRun)
        {
            console.MarkupLine("[yellow]Dry run mode - no changes will be made[/]");
        }

        var result = await fleeceService.CleanAsync(
            includeComplete: settings.IncludeComplete,
            includeClosed: settings.IncludeClosed,
            includeArchived: settings.IncludeArchived,
            stripReferences: !settings.NoStripRefs,
            dryRun: settings.DryRun);

        if (result.CleanedTombstones.Count == 0)
        {
            if (settings.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(result, FleeceJsonContext.Default.CleanResult));
            }
            else
            {
                console.MarkupLine("[green]No issues to clean.[/]");
            }
            return 0;
        }

        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, FleeceJsonContext.Default.CleanResult));
        }
        else
        {
            console.MarkupLine($"[yellow]Cleaned {result.CleanedTombstones.Count} issue(s)[/]");
            console.WriteLine();

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn(new TableColumn("Issue ID").Centered());
            table.AddColumn(new TableColumn("Title"));
            table.AddColumn(new TableColumn("Cleaned By"));

            foreach (var tombstone in result.CleanedTombstones)
            {
                table.AddRow(
                    $"[bold]{tombstone.IssueId}[/]",
                    Markup.Escape(tombstone.OriginalTitle),
                    Markup.Escape(tombstone.CleanedBy));
            }

            console.Write(table);

            if (result.StrippedReferences.Count > 0)
            {
                console.WriteLine();
                console.MarkupLine($"[yellow]Stripped {result.StrippedReferences.Count} dangling reference(s)[/]");

                var refTable = new Table();
                refTable.Border(TableBorder.Simple);
                refTable.AddColumn(new TableColumn("Cleaned Issue").Centered());
                refTable.AddColumn(new TableColumn("Referenced By").Centered());
                refTable.AddColumn(new TableColumn("Reference Type").Centered());

                foreach (var reference in result.StrippedReferences)
                {
                    refTable.AddRow(reference.IssueId, reference.ReferencingIssueId, reference.ReferenceType);
                }

                console.Write(refTable);
            }

            if (!settings.DryRun)
            {
                console.WriteLine();
                console.MarkupLine("[green]Clean complete! Tombstone records created.[/]");
            }
        }

        return 0;
    }
}
