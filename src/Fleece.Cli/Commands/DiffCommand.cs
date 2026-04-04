using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class DiffCommand(IDiffService diffService, IFleeceService fleeceService) : AsyncCommand<DiffSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DiffSettings settings)
    {
        var (hasMultiple, message) = await fleeceService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");
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
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {Markup.Escape(settings.File1!)}");
            return 1;
        }

        if (!File.Exists(settings.File2))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {Markup.Escape(settings.File2!)}");
            return 1;
        }

        var result = await diffService.CompareFilesAsync(settings.File1!, settings.File2!);

        if (!result.HasDifferences)
        {
            AnsiConsole.MarkupLine("[dim]No differences found between the two files.[/]");
            return 0;
        }

        var file1Name = Path.GetFileName(settings.File1);
        var file2Name = Path.GetFileName(settings.File2);

        // Display modified issues
        if (result.Modified.Count > 0)
        {
            var modifiedTable = new Table();
            modifiedTable.Border(TableBorder.Rounded);
            modifiedTable.Title = new TableTitle("[yellow]Modified Issues[/]");
            modifiedTable.AddColumn("ID");
            modifiedTable.AddColumn($"File 1: {Markup.Escape(file1Name!)}");
            modifiedTable.AddColumn($"File 2: {Markup.Escape(file2Name!)}");

            foreach (var (issue1, issue2) in result.Modified)
            {
                modifiedTable.AddRow(
                    issue1.Id,
                    FormatIssue(issue1),
                    FormatIssue(issue2)
                );
            }

            AnsiConsole.Write(modifiedTable);
        }

        // Display issues only in file 1
        if (result.OnlyInFile1.Count > 0)
        {
            var onlyIn1Table = new Table();
            onlyIn1Table.Border(TableBorder.Rounded);
            onlyIn1Table.Title = new TableTitle($"[cyan]Only in {Markup.Escape(file1Name!)}[/]");
            onlyIn1Table.AddColumn("ID");
            onlyIn1Table.AddColumn("Title");
            onlyIn1Table.AddColumn("Status");

            foreach (var issue in result.OnlyInFile1)
            {
                onlyIn1Table.AddRow(
                    issue.Id,
                    Markup.Escape(issue.Title),
                    GetStatusMarkup(issue.Status)
                );
            }

            AnsiConsole.Write(onlyIn1Table);
        }

        // Display issues only in file 2
        if (result.OnlyInFile2.Count > 0)
        {
            var onlyIn2Table = new Table();
            onlyIn2Table.Border(TableBorder.Rounded);
            onlyIn2Table.Title = new TableTitle($"[cyan]Only in {Markup.Escape(file2Name!)}[/]");
            onlyIn2Table.AddColumn("ID");
            onlyIn2Table.AddColumn("Title");
            onlyIn2Table.AddColumn("Status");

            foreach (var issue in result.OnlyInFile2)
            {
                onlyIn2Table.AddRow(
                    issue.Id,
                    Markup.Escape(issue.Title),
                    GetStatusMarkup(issue.Status)
                );
            }

            AnsiConsole.Write(onlyIn2Table);
        }

        AnsiConsole.MarkupLine($"[dim]{result.Modified.Count} modified, {result.OnlyInFile1.Count} only in file 1, {result.OnlyInFile2.Count} only in file 2[/]");

        return 0;
    }

    private static string FormatIssue(Issue issue)
    {
        var statusMarkup = GetStatusMarkup(issue.Status);
        return $"{Markup.Escape(issue.Title)} {statusMarkup}";
    }

    private static string GetStatusMarkup(IssueStatus status)
    {
        var color = status switch
        {
            IssueStatus.Open => "cyan",
            IssueStatus.Progress => "blue",
            IssueStatus.Review => "purple",
            IssueStatus.Complete => "green",
            IssueStatus.Draft => "dim",
            IssueStatus.Archived => "dim",
            IssueStatus.Closed => "dim",
            _ => "white"
        };

        return $"[{color}]{status}[/]";
    }
}
