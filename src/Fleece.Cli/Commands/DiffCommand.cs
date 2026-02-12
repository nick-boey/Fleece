using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class DiffCommand(IMergeService mergeService, IStorageService storageService) : AsyncCommand<DiffSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DiffSettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(settings.File1) || string.IsNullOrWhiteSpace(settings.File2))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Two JSONL file paths are required for comparison.");
            return 1;
        }

        return await CompareFilesAsync(settings);
    }

    private async Task<int> CompareFilesAsync(DiffSettings settings)
    {
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

        var differences = await mergeService.CompareFilesAsync(settings.File1!, settings.File2!);

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
