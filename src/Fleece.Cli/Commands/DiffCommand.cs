using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class DiffCommand(IChangeService changeService, IMergeService mergeService) : AsyncCommand<DiffSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DiffSettings settings)
    {
        // If no files specified, show change history
        if (string.IsNullOrWhiteSpace(settings.File1))
        {
            var changes = await changeService.GetAllAsync();

            if (settings.Json)
            {
                JsonFormatter.RenderChanges(changes);
            }
            else
            {
                TableFormatter.RenderChanges(changes);
            }

            return 0;
        }

        // Compare two files
        if (string.IsNullOrWhiteSpace(settings.File2))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Both FILE1 and FILE2 must be specified for comparison");
            return 1;
        }

        if (!File.Exists(settings.File1))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {settings.File1}");
            return 1;
        }

        if (!File.Exists(settings.File2))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {settings.File2}");
            return 1;
        }

        var differences = await mergeService.CompareFilesAsync(settings.File1, settings.File2);

        if (differences.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No differences found in issues present in both files.[/]");
            return 0;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("ID");
        table.AddColumn($"File 1: {Path.GetFileName(settings.File1)}");
        table.AddColumn($"File 2: {Path.GetFileName(settings.File2)}");

        foreach (var (issue1, issue2) in differences)
        {
            table.AddRow(
                issue1.Id,
                $"{Markup.Escape(issue1.Title)} [{issue1.Status}]",
                $"{Markup.Escape(issue2.Title)} [{issue2.Status}]"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]{differences.Count} difference(s)[/]");

        return 0;
    }
}
