using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class DiffCommand(IChangeService changeService, IMergeService mergeService, IStorageService storageService) : AsyncCommand<DiffSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DiffSettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        // Check if we're comparing two files
        if (!string.IsNullOrWhiteSpace(settings.File1) && !string.IsNullOrWhiteSpace(settings.File2))
        {
            return await CompareFilesAsync(settings);
        }

        // Otherwise, show change history with optional filtering
        return await ShowHistoryAsync(settings);
    }

    private async Task<int> ShowHistoryAsync(DiffSettings settings)
    {
        var changes = await GetChangesAsync(settings);

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

    private async Task<IReadOnlyList<ChangeRecord>> GetChangesAsync(DiffSettings settings)
    {
        // If File1 is provided but File2 is not, treat File1 as an issue ID filter
        if (!string.IsNullOrWhiteSpace(settings.File1))
        {
            return await changeService.GetByIssueIdAsync(settings.File1);
        }

        if (!string.IsNullOrWhiteSpace(settings.User))
        {
            return await changeService.GetByUserAsync(settings.User);
        }

        return await changeService.GetAllAsync();
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
