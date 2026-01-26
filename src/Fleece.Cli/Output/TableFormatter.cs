using Fleece.Core.Models;
using Spectre.Console;

namespace Fleece.Cli.Output;

public static class TableFormatter
{
    /// <summary>
    /// Escapes text for safe console rendering.
    /// Handles Spectre.Console markup escaping and replaces backticks with single quotes
    /// to avoid rendering issues in Windows console.
    /// </summary>
    private static string EscapeForConsole(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        // Replace backticks with single quotes for console compatibility
        var sanitized = text.Replace('`', '\'');

        // Apply Spectre.Console markup escaping
        return Markup.Escape(sanitized);
    }

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
                IssueStatus.Idea => "magenta",
                IssueStatus.Spec => "cyan",
                IssueStatus.Next => "yellow",
                IssueStatus.Progress => "blue",
                IssueStatus.Review => "purple",
                IssueStatus.Complete => "green",
                IssueStatus.Archived => "dim",
                IssueStatus.Closed => "dim",
                _ => "white"
            };

            var typeColor = issue.Type switch
            {
                IssueType.Bug => "red",
                IssueType.Feature => "cyan",
                IssueType.Task => "white",
                IssueType.Chore => "dim",
                _ => "white"
            };

            var priDisplay = issue.Priority?.ToString() ?? "-";
            var groupDisplay = issue.Group ?? "-";
            var assignedDisplay = issue.AssignedTo ?? "-";

            table.AddRow(
                $"[bold]{issue.Id}[/]",
                EscapeForConsole(issue.Title),
                $"[{typeColor}]{issue.Type}[/]",
                $"[{statusColor}]{issue.Status}[/]",
                priDisplay,
                EscapeForConsole(groupDisplay),
                EscapeForConsole(assignedDisplay),
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
            Header = new PanelHeader($"[bold]{issue.Id}[/] - {EscapeForConsole(issue.Title)}"),
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
            lines.Add($"[bold]Description:[/] {EscapeForConsole(issue.Description)}");
        }

        if (issue.CreatedAt != default)
        {
            lines.Add($"[bold]Created At:[/] {issue.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }

        if (!string.IsNullOrEmpty(issue.WorkingBranchId))
        {
            lines.Add($"[bold]Working Branch:[/] {EscapeForConsole(issue.WorkingBranchId)}");
        }

        if (issue.LinkedPR.HasValue)
        {
            lines.Add($"[bold]Linked PR:[/] #{issue.LinkedPR}");
        }

        if (issue.LinkedIssues?.Count > 0)
        {
            lines.Add($"[bold]Linked Issues:[/] {string.Join(", ", issue.LinkedIssues)}");
        }

        if (issue.ParentIssues?.Count > 0)
        {
            lines.Add($"[bold]Parent Issues:[/] {string.Join(", ", issue.ParentIssues)}");
        }

        if (issue.PreviousIssues?.Count > 0)
        {
            lines.Add($"[bold]Previous Issues:[/] {string.Join(", ", issue.PreviousIssues)}");
        }

        if (!string.IsNullOrEmpty(issue.Group))
        {
            lines.Add($"[bold]Group:[/] {EscapeForConsole(issue.Group)}");
        }

        if (!string.IsNullOrEmpty(issue.AssignedTo))
        {
            lines.Add($"[bold]Assigned To:[/] {EscapeForConsole(issue.AssignedTo)}");
        }

        if (issue.Tags?.Count > 0)
        {
            lines.Add($"[bold]Tags:[/] {string.Join(", ", issue.Tags.Select(EscapeForConsole))}");
        }

        if (!string.IsNullOrEmpty(issue.CreatedBy))
        {
            lines.Add($"[bold]Created By:[/] {EscapeForConsole(issue.CreatedBy)}");
        }

        lines.Add($"[bold]Last Update:[/] {issue.LastUpdate:yyyy-MM-dd HH:mm:ss}");

        // Questions section
        if (issue.Questions?.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Questions:[/]");
            foreach (var question in issue.Questions)
            {
                lines.Add($"  [cyan]{question.Id}:[/] {EscapeForConsole(question.Text)}");
                var askedBy = question.AskedBy ?? "unknown";
                lines.Add($"    [dim]Asked by:[/] {EscapeForConsole(askedBy)} [dim]on[/] {question.AskedAt:yyyy-MM-dd}");

                if (!string.IsNullOrEmpty(question.Answer))
                {
                    lines.Add($"    [green]Answer:[/] {EscapeForConsole(question.Answer)}");
                    var answeredBy = question.AnsweredBy ?? "unknown";
                    var answeredAt = question.AnsweredAt?.ToString("yyyy-MM-dd") ?? "unknown";
                    lines.Add($"    [dim]Answered by:[/] {EscapeForConsole(answeredBy)} [dim]on[/] {answeredAt}");
                }
                else
                {
                    lines.Add("    [yellow](Unanswered)[/]");
                }
            }
        }

        return string.Join("\n", lines);
    }

    public static void RenderChanges(IReadOnlyList<ChangeRecord> changes)
    {
        if (changes.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No changes found.[/]");
            return;
        }

        foreach (var change in changes.OrderByDescending(c => c.ChangedAt))
        {
            var typeColor = change.Type switch
            {
                ChangeType.Created => "green",
                ChangeType.Updated => "yellow",
                ChangeType.Deleted => "red",
                ChangeType.Merged => "cyan",
                _ => "white"
            };

            var header = $"[{typeColor}]{change.Type}[/] {change.IssueId} by {EscapeForConsole(change.ChangedBy)}";
            var content = BuildChangeContent(change);

            var panel = new Panel(content)
            {
                Header = new PanelHeader(header),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);

            // Show property changes if available
            if (change.PropertyChanges.Count > 0)
            {
                RenderPropertyChanges(change.PropertyChanges);
            }

            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine($"[dim]{changes.Count} change(s)[/]");
    }

    private static string BuildChangeContent(ChangeRecord change)
    {
        var lines = new List<string>
        {
            $"[bold]Change ID:[/] {change.ChangeId}",
            $"[bold]Issue ID:[/] {change.IssueId}",
            $"[bold]Type:[/] {change.Type}",
            $"[bold]Changed By:[/] {EscapeForConsole(change.ChangedBy)}",
            $"[bold]Changed At:[/] {change.ChangedAt:yyyy-MM-dd HH:mm:ss}"
        };

        return string.Join("\n", lines);
    }

    public static void RenderPropertyChanges(IReadOnlyList<PropertyChange> propertyChanges)
    {
        if (propertyChanges.Count == 0)
        {
            return;
        }

        var table = new Table();
        table.Border(TableBorder.Simple);
        table.AddColumn(new TableColumn("Property").Centered());
        table.AddColumn(new TableColumn("Old Value"));
        table.AddColumn(new TableColumn("New Value"));
        table.AddColumn(new TableColumn("Resolution").Centered());

        foreach (var pc in propertyChanges)
        {
            var resolutionColor = pc.MergeResolution switch
            {
                "A" => "cyan",
                "B" => "magenta",
                "Union" => "green",
                _ => "dim"
            };

            var resolutionDisplay = pc.MergeResolution ?? "-";

            table.AddRow(
                $"[bold]{pc.PropertyName}[/]",
                EscapeForConsole(pc.OldValue ?? "(null)"),
                EscapeForConsole(pc.NewValue ?? "(null)"),
                $"[{resolutionColor}]{resolutionDisplay}[/]"
            );
        }

        AnsiConsole.Write(table);
    }
}
