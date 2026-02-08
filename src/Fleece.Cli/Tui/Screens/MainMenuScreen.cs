using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;

namespace Fleece.Cli.Tui.Screens;

/// <summary>
/// Displays a summary panel and main menu selection prompt.
/// </summary>
public sealed class MainMenuScreen(IIssueService issueService, IAnsiConsole console)
{
    public async Task<MenuChoice> ShowAsync(CancellationToken cancellationToken = default)
    {
        var issues = await issueService.GetAllAsync(cancellationToken);

        // Filter out deleted issues for the summary
        var activeIssues = issues.Where(i => i.Status != IssueStatus.Deleted).ToList();

        RenderSummary(activeIssues);

        var choice = console.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]What would you like to do?[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(
                    "Browse All Issues",
                    "Browse Issues (filtered)",
                    "Search Issues",
                    "Next Actionable Issues",
                    "Create Issue",
                    "View Task Graph",
                    "Exit"));

        return choice switch
        {
            "Browse All Issues" => MenuChoice.BrowseAll,
            "Browse Issues (filtered)" => MenuChoice.BrowseFiltered,
            "Search Issues" => MenuChoice.Search,
            "Next Actionable Issues" => MenuChoice.NextActionable,
            "Create Issue" => MenuChoice.CreateIssue,
            "View Task Graph" => MenuChoice.ViewTaskGraph,
            "Exit" => MenuChoice.Exit,
            _ => MenuChoice.Exit
        };
    }

    private void RenderSummary(IReadOnlyList<Issue> issues)
    {
        var statusCounts = issues
            .GroupBy(i => i.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var parts = new List<string> { $"[bold]{issues.Count}[/] issues total" };

        AddStatusCount(parts, statusCounts, IssueStatus.Open, "cyan");
        AddStatusCount(parts, statusCounts, IssueStatus.Progress, "blue");
        AddStatusCount(parts, statusCounts, IssueStatus.Review, "purple");
        AddStatusCount(parts, statusCounts, IssueStatus.Complete, "green");
        AddStatusCount(parts, statusCounts, IssueStatus.Archived, "dim");
        AddStatusCount(parts, statusCounts, IssueStatus.Closed, "dim");

        var summaryText = string.Join(", ", parts);

        var panel = new Panel(summaryText)
        {
            Header = new PanelHeader("[bold cyan]Fleece Issue Tracker[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };

        console.Write(panel);
        console.WriteLine();
    }

    private static void AddStatusCount(
        List<string> parts,
        Dictionary<IssueStatus, int> statusCounts,
        IssueStatus status,
        string color)
    {
        if (statusCounts.TryGetValue(status, out var count) && count > 0)
        {
            parts.Add($"[{color}]{count} {status.ToString().ToLowerInvariant()}[/]");
        }
    }
}
