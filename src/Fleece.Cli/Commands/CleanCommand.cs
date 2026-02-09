using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class CleanCommand(ICleanService cleanService, IStorageService storageService) : AsyncCommand<CleanSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CleanSettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]Dry run mode - no changes will be made[/]");
        }

        var result = await cleanService.CleanAsync(
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
                AnsiConsole.MarkupLine("[green]No issues to clean.[/]");
            }
            return 0;
        }

        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, FleeceJsonContext.Default.CleanResult));
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Cleaned {result.CleanedTombstones.Count} issue(s)[/]");
            AnsiConsole.WriteLine();

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn(new TableColumn("Issue ID").Centered());
            table.AddColumn(new TableColumn("Cleaned By"));

            foreach (var tombstone in result.CleanedTombstones)
            {
                table.AddRow(
                    $"[bold]{tombstone.IssueId}[/]",
                    Markup.Escape(tombstone.CleanedBy));
            }

            AnsiConsole.Write(table);

            if (result.StrippedReferences.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[yellow]Stripped {result.StrippedReferences.Count} dangling reference(s)[/]");

                var refTable = new Table();
                refTable.Border(TableBorder.Simple);
                refTable.AddColumn(new TableColumn("Cleaned Issue").Centered());
                refTable.AddColumn(new TableColumn("Referenced By").Centered());
                refTable.AddColumn(new TableColumn("Reference Type").Centered());

                foreach (var reference in result.StrippedReferences)
                {
                    refTable.AddRow(reference.IssueId, reference.ReferencingIssueId, reference.ReferenceType);
                }

                AnsiConsole.Write(refTable);
            }

            if (result.RemovedChangeRecords > 0)
            {
                AnsiConsole.MarkupLine($"[dim]Removed {result.RemovedChangeRecords} change record(s)[/]");
            }

            if (!settings.DryRun)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[green]Clean complete! Tombstone records created.[/]");
            }
        }

        return 0;
    }
}
