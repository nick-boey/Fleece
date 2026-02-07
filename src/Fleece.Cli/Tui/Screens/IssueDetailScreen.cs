using Fleece.Core.Models;
using Spectre.Console;

namespace Fleece.Cli.Tui.Screens;

/// <summary>
/// Actions available from the issue detail screen.
/// </summary>
public enum DetailAction
{
    ChangeStatus,
    EditPriority,
    BackToList,
    BackToMenu
}

/// <summary>
/// Displays full issue detail and an action selection prompt.
/// </summary>
public sealed class IssueDetailScreen(IAnsiConsole console)
{
    public DetailAction Show(Issue issue)
    {
        RenderDetail(issue);
        console.WriteLine();

        var choice = console.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Choose an action:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(
                    "Change Status",
                    "Edit Priority",
                    "Back to List",
                    "Back to Main Menu"));

        return choice switch
        {
            "Change Status" => DetailAction.ChangeStatus,
            "Edit Priority" => DetailAction.EditPriority,
            "Back to List" => DetailAction.BackToList,
            "Back to Main Menu" => DetailAction.BackToMenu,
            _ => DetailAction.BackToMenu
        };
    }

    private void RenderDetail(Issue issue)
    {
        var content = BuildIssueContent(issue);

        var panel = new Panel(content)
        {
            Header = new PanelHeader($"[bold]{issue.Id}[/] - {Markup.Escape(issue.Title)}"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };

        console.Write(panel);
    }

    private static string BuildIssueContent(Issue issue)
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

        var lines = new List<string>
        {
            $"[bold]Status:[/]   [{statusColor}]{issue.Status}[/]",
            $"[bold]Type:[/]     [{typeColor}]{issue.Type}[/]"
        };

        if (issue.Priority.HasValue)
        {
            lines.Add($"[bold]Priority:[/] {issue.Priority}");
        }

        if (!string.IsNullOrEmpty(issue.Description))
        {
            lines.Add("");
            lines.Add($"[bold]Description:[/]");
            lines.Add(Markup.Escape(issue.Description));
        }

        if (!string.IsNullOrEmpty(issue.AssignedTo))
        {
            lines.Add($"[bold]Assigned:[/] {Markup.Escape(issue.AssignedTo)}");
        }

        if (issue.CreatedAt != default)
        {
            lines.Add($"[bold]Created:[/]  {issue.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }

        if (!string.IsNullOrEmpty(issue.WorkingBranchId))
        {
            lines.Add($"[bold]Branch:[/]   {Markup.Escape(issue.WorkingBranchId)}");
        }

        if (issue.LinkedPR.HasValue)
        {
            lines.Add($"[bold]PR:[/]       #{issue.LinkedPR}");
        }

        if (issue.LinkedIssues?.Count > 0)
        {
            lines.Add($"[bold]Linked:[/]   {string.Join(", ", issue.LinkedIssues)}");
        }

        if (issue.ParentIssues?.Count > 0)
        {
            lines.Add($"[bold]Parents:[/]  {string.Join(", ", issue.ParentIssues.Select(p => p.ParentIssue))}");
        }

        if (issue.Tags?.Count > 0)
        {
            lines.Add($"[bold]Tags:[/]     {string.Join(", ", issue.Tags.Select(Markup.Escape))}");
        }

        lines.Add($"[bold]Updated:[/]  {issue.LastUpdate:yyyy-MM-dd HH:mm:ss}");

        // Questions section
        if (issue.Questions?.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Questions:[/]");
            foreach (var question in issue.Questions)
            {
                lines.Add($"  [cyan]{question.Id}:[/] {Markup.Escape(question.Text)}");
                if (!string.IsNullOrEmpty(question.Answer))
                {
                    lines.Add($"    [green]Answer:[/] {Markup.Escape(question.Answer)}");
                }
                else
                {
                    lines.Add("    [yellow](Unanswered)[/]");
                }
            }
        }

        return string.Join("\n", lines);
    }
}
