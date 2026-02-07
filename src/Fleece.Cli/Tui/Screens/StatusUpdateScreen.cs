using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;

namespace Fleece.Cli.Tui.Screens;

/// <summary>
/// Displays a status selection prompt and updates the issue.
/// </summary>
public sealed class StatusUpdateScreen(IIssueService issueService, IAnsiConsole console)
{
    public async Task<Issue> ShowAsync(Issue issue, CancellationToken cancellationToken = default)
    {
        var statuses = Enum.GetValues<IssueStatus>()
            .Where(s => s != IssueStatus.Deleted)
            .ToList();

        var newStatus = console.Prompt(
            new SelectionPrompt<IssueStatus>()
                .Title($"[bold]Change status[/] (current: [{GetStatusColor(issue.Status)}]{issue.Status}[/]):")
                .HighlightStyle(new Style(Color.Cyan1))
                .UseConverter(s =>
                {
                    var color = GetStatusColor(s);
                    var marker = s == issue.Status ? " (current)" : "";
                    return $"[{color}]{s}[/]{marker}";
                })
                .AddChoices(statuses));

        if (newStatus == issue.Status)
        {
            console.MarkupLine("[dim]Status unchanged.[/]");
            return issue;
        }

        var updated = await issueService.UpdateAsync(issue.Id, status: newStatus, cancellationToken: cancellationToken);
        console.MarkupLine($"[green]Status updated to[/] [{GetStatusColor(newStatus)}]{newStatus}[/]");
        return updated;
    }

    private static string GetStatusColor(IssueStatus status) => status switch
    {
        IssueStatus.Open => "cyan",
        IssueStatus.Progress => "blue",
        IssueStatus.Review => "purple",
        IssueStatus.Complete => "green",
        IssueStatus.Archived => "dim",
        IssueStatus.Closed => "dim",
        _ => "white"
    };
}
