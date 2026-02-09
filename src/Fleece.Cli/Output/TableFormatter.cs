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
        table.AddColumn(new TableColumn("Assigned").Centered());
        table.AddColumn(new TableColumn("Updated").Centered());

        foreach (var issue in issues)
        {
            var statusColor = issue.Status switch
            {
                IssueStatus.Open => "cyan",
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
            var assignedDisplay = issue.AssignedTo ?? "-";

            table.AddRow(
                $"[bold]{issue.Id}[/]",
                Markup.Escape(issue.Title),
                $"[{typeColor}]{issue.Type}[/]",
                $"[{statusColor}]{issue.Status}[/]",
                priDisplay,
                Markup.Escape(assignedDisplay),
                issue.LastUpdate.ToString("yyyy-MM-dd")
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]{issues.Count} issue(s)[/]");
    }

    public static void RenderIssue(Issue issue, IssueShowDto? context = null)
    {
        var panel = new Panel(BuildIssueContent(issue, context))
        {
            Header = new PanelHeader($"[bold]{issue.Id}[/] - {Markup.Escape(issue.Title)}"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
    }

    private static string BuildIssueContent(Issue issue, IssueShowDto? context = null)
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

        if (issue.CreatedAt != default)
        {
            lines.Add($"[bold]Created At:[/] {issue.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }

        if (!string.IsNullOrEmpty(issue.WorkingBranchId))
        {
            lines.Add($"[bold]Working Branch:[/] {Markup.Escape(issue.WorkingBranchId)}");
        }

        if (issue.LinkedPR.HasValue)
        {
            lines.Add($"[bold]Linked PR:[/] #{issue.LinkedPR}");
        }

        if (issue.LinkedIssues?.Count > 0)
        {
            lines.Add($"[bold]Linked Issues:[/] {string.Join(", ", issue.LinkedIssues)}");
        }

        if (context?.Parents.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Parent Issues:[/]");
            foreach (var pc in context.Parents)
            {
                lines.Add($"  {IssueLineFormatter.FormatMarkup(pc.Parent)}");
                lines.Add($"    [dim]Execution Mode:[/] {pc.ExecutionMode.ToString().ToLowerInvariant()}");
                if (pc.ExecutionMode == ExecutionMode.Series && pc.Position.HasValue)
                {
                    lines.Add($"    [dim]Position:[/] {pc.Position} of {pc.TotalSiblings}");
                    if (pc.PreviousSibling is not null)
                    {
                        lines.Add($"    [dim]Previous:[/] {IssueLineFormatter.FormatMarkup(pc.PreviousSibling)}");
                    }
                    if (pc.NextSibling is not null)
                    {
                        lines.Add($"    [dim]Next:[/]     {IssueLineFormatter.FormatMarkup(pc.NextSibling)}");
                    }
                }
            }
        }
        else if (issue.ParentIssues?.Count > 0)
        {
            lines.Add($"[bold]Parent Issues:[/] {string.Join(", ", issue.ParentIssues.Select(p => p.ParentIssue))}");
        }

        if (!string.IsNullOrEmpty(issue.AssignedTo))
        {
            lines.Add($"[bold]Assigned To:[/] {Markup.Escape(issue.AssignedTo)}");
        }

        if (issue.Tags?.Count > 0)
        {
            lines.Add($"[bold]Tags:[/] {string.Join(", ", issue.Tags.Select(Markup.Escape))}");
        }

        if (context?.Children.Count > 0)
        {
            var modeLabel = context.ExecutionMode.ToString().ToLowerInvariant();
            lines.Add("");
            lines.Add($"[bold]Children:[/] ({modeLabel})");
            for (var i = 0; i < context.Children.Count; i++)
            {
                var child = context.Children[i];
                var prefix = context.ExecutionMode == ExecutionMode.Series
                    ? $"  {i + 1}. "
                    : "  - ";
                lines.Add($"{prefix}{IssueLineFormatter.FormatMarkup(child)}");
            }
        }

        if (!string.IsNullOrEmpty(issue.CreatedBy))
        {
            lines.Add($"[bold]Created By:[/] {Markup.Escape(issue.CreatedBy)}");
        }

        lines.Add($"[bold]Last Update:[/] {issue.LastUpdate:yyyy-MM-dd HH:mm:ss}");

        // Questions section
        if (issue.Questions?.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Questions:[/]");
            foreach (var question in issue.Questions)
            {
                lines.Add($"  [cyan]{question.Id}:[/] {Markup.Escape(question.Text)}");
                var askedBy = question.AskedBy ?? "unknown";
                lines.Add($"    [dim]Asked by:[/] {Markup.Escape(askedBy)} [dim]on[/] {question.AskedAt:yyyy-MM-dd}");

                if (!string.IsNullOrEmpty(question.Answer))
                {
                    lines.Add($"    [green]Answer:[/] {Markup.Escape(question.Answer)}");
                    var answeredBy = question.AnsweredBy ?? "unknown";
                    var answeredAt = question.AnsweredAt?.ToString("yyyy-MM-dd") ?? "unknown";
                    lines.Add($"    [dim]Answered by:[/] {Markup.Escape(answeredBy)} [dim]on[/] {answeredAt}");
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

            var header = $"[{typeColor}]{change.Type}[/] {change.IssueId} by {Markup.Escape(change.ChangedBy)}";
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
            $"[bold]Changed By:[/] {Markup.Escape(change.ChangedBy)}",
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
                Markup.Escape(pc.OldValue ?? "(null)"),
                Markup.Escape(pc.NewValue ?? "(null)"),
                $"[{resolutionColor}]{resolutionDisplay}[/]"
            );
        }

        AnsiConsole.Write(table);
    }
}
