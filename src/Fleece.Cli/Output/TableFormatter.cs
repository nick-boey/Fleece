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
        table.AddColumn(new TableColumn("Group").Centered());
        table.AddColumn(new TableColumn("Assigned").Centered());
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
            var groupDisplay = issue.Group ?? "-";
            var assignedDisplay = issue.AssignedTo ?? "-";

            table.AddRow(
                $"[bold]{issue.Id}[/]",
                Markup.Escape(issue.Title),
                $"[{typeColor}]{issue.Type}[/]",
                $"[{statusColor}]{issue.Status}[/]",
                priDisplay,
                Markup.Escape(groupDisplay),
                Markup.Escape(assignedDisplay),
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

        if (!string.IsNullOrEmpty(issue.Group))
        {
            lines.Add($"[bold]Group:[/] {Markup.Escape(issue.Group)}");
        }

        if (!string.IsNullOrEmpty(issue.AssignedTo))
        {
            lines.Add($"[bold]Assigned To:[/] {Markup.Escape(issue.AssignedTo)}");
        }

        if (!string.IsNullOrEmpty(issue.CreatedBy))
        {
            lines.Add($"[bold]Created By:[/] {Markup.Escape(issue.CreatedBy)}");
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
            var content = BuildConflictContent(conflict);
            var panel = new Panel(content)
            {
                Header = new PanelHeader($"[yellow]Merged[/] {conflict.IssueId}"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);

            // Show property-level resolution table if available
            if (conflict.PropertyConflicts is { Count: > 0 })
            {
                RenderPropertyConflicts(conflict.PropertyConflicts);
            }
        }

        AnsiConsole.MarkupLine($"[dim]{conflicts.Count} merge(s) performed[/]");
    }

    private static string BuildConflictContent(ConflictRecord conflict)
    {
        var lines = new List<string>
        {
            $"[bold]Issue ID:[/] {conflict.IssueId}",
            $"[bold]Version A:[/] {Markup.Escape(conflict.OlderVersion.Title)} (Updated: {conflict.OlderVersion.LastUpdate:yyyy-MM-dd HH:mm})",
            $"[bold]Version B:[/] {Markup.Escape(conflict.NewerVersion.Title)} (Updated: {conflict.NewerVersion.LastUpdate:yyyy-MM-dd HH:mm})",
            $"[bold]Detected:[/] {conflict.DetectedAt:yyyy-MM-dd HH:mm:ss}"
        };

        if (conflict.MergedResult is not null)
        {
            lines.Add($"[bold]Merged Title:[/] {Markup.Escape(conflict.MergedResult.Title)}");
        }

        return string.Join("\n", lines);
    }

    public static void RenderPropertyConflicts(IReadOnlyList<PropertyConflict> propertyConflicts)
    {
        if (propertyConflicts.Count == 0)
        {
            return;
        }

        var table = new Table();
        table.Border(TableBorder.Simple);
        table.AddColumn(new TableColumn("Property").Centered());
        table.AddColumn(new TableColumn("Value A"));
        table.AddColumn(new TableColumn("Value B"));
        table.AddColumn(new TableColumn("Winner").Centered());
        table.AddColumn(new TableColumn("Resolved Value"));

        foreach (var pc in propertyConflicts)
        {
            var winnerColor = pc.Resolution switch
            {
                "A" => "cyan",
                "B" => "magenta",
                "Union" => "green",
                _ => "white"
            };

            table.AddRow(
                $"[bold]{pc.PropertyName}[/]",
                Markup.Escape(pc.ValueA ?? "(null)"),
                Markup.Escape(pc.ValueB ?? "(null)"),
                $"[{winnerColor}]{pc.Resolution}[/]",
                Markup.Escape(pc.ResolvedValue ?? "(null)")
            );
        }

        AnsiConsole.Write(table);
    }
}
