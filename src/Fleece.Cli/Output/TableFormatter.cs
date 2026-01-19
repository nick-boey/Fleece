using Fleece.Core.Models;
using Spectre.Console;

namespace Fleece.Cli.Output;

public static class TableFormatter
{
    public static void RenderIssues(IReadOnlyList<Issue> issues)
    {
        if (issues.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No issues found.[/]");
            return;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);

        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn(new TableColumn("Title"));
        table.AddColumn(new TableColumn("Type").Centered());
        table.AddColumn(new TableColumn("Status").Centered());
        table.AddColumn(new TableColumn("Pri").Centered());
        table.AddColumn(new TableColumn("Updated").Centered());

        foreach (var issue in issues)
        {
            var statusColor = issue.Status switch
            {
                IssueStatus.Open => "yellow",
                IssueStatus.Complete => "green",
                IssueStatus.Closed => "blue",
                IssueStatus.Archived => "dim",
                _ => "white"
            };

            var typeColor = issue.Type switch
            {
                IssueType.Bug => "red",
                IssueType.Feature => "cyan",
                IssueType.Task => "white",
                IssueType.Chore => "dim",
                IssueType.Idea => "magenta",
                _ => "white"
            };

            var priDisplay = issue.Priority?.ToString() ?? "-";

            table.AddRow(
                $"[bold]{issue.Id}[/]",
                Markup.Escape(issue.Title),
                $"[{typeColor}]{issue.Type}[/]",
                $"[{statusColor}]{issue.Status}[/]",
                priDisplay,
                issue.LastUpdate.ToString("yyyy-MM-dd")
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]{issues.Count} issue(s)[/]");
    }

    public static void RenderIssue(Issue issue)
    {
        var panel = new Panel(BuildIssueContent(issue))
        {
            Header = new PanelHeader($"[bold]{issue.Id}[/] - {Markup.Escape(issue.Title)}"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
    }

    private static string BuildIssueContent(Issue issue)
    {
        var lines = new List<string>
        {
            $"[bold]Status:[/] {issue.Status}",
            $"[bold]Type:[/] {issue.Type}"
        };

        if (issue.Priority.HasValue)
        {
            lines.Add($"[bold]Priority:[/] {issue.Priority}");
        }

        if (!string.IsNullOrEmpty(issue.Description))
        {
            lines.Add($"[bold]Description:[/] {Markup.Escape(issue.Description)}");
        }

        if (issue.LinkedPR.HasValue)
        {
            lines.Add($"[bold]Linked PR:[/] #{issue.LinkedPR}");
        }

        if (issue.LinkedIssues.Count > 0)
        {
            lines.Add($"[bold]Linked Issues:[/] {string.Join(", ", issue.LinkedIssues)}");
        }

        if (issue.ParentIssues.Count > 0)
        {
            lines.Add($"[bold]Parent Issues:[/] {string.Join(", ", issue.ParentIssues)}");
        }

        lines.Add($"[bold]Last Update:[/] {issue.LastUpdate:yyyy-MM-dd HH:mm:ss}");

        return string.Join("\n", lines);
    }

    public static void RenderConflicts(IReadOnlyList<ConflictRecord> conflicts)
    {
        if (conflicts.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No conflicts found.[/]");
            return;
        }

        foreach (var conflict in conflicts)
        {
            var panel = new Panel(
                $"[bold]Issue ID:[/] {conflict.IssueId}\n" +
                $"[bold]Older:[/] {Markup.Escape(conflict.OlderVersion.Title)} (Updated: {conflict.OlderVersion.LastUpdate:yyyy-MM-dd HH:mm})\n" +
                $"[bold]Newer:[/] {Markup.Escape(conflict.NewerVersion.Title)} (Updated: {conflict.NewerVersion.LastUpdate:yyyy-MM-dd HH:mm})\n" +
                $"[bold]Detected:[/] {conflict.DetectedAt:yyyy-MM-dd HH:mm:ss}")
            {
                Header = new PanelHeader($"[red]Conflict[/] {conflict.ConflictId:N}".Substring(0, 40)),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);
        }

        AnsiConsole.MarkupLine($"[dim]{conflicts.Count} conflict(s)[/]");
    }
}
