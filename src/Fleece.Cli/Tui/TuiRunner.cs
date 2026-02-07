using Fleece.Cli.Tui.Screens;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;

namespace Fleece.Cli.Tui;

/// <summary>
/// Main loop orchestrator for the TUI.
/// Manages screen transitions and navigation.
/// </summary>
public sealed class TuiRunner(
    IIssueService issueService,
    IStorageService storageService,
    INextService nextService,
    IAnsiConsole console)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var mainMenuScreen = new MainMenuScreen(issueService, console);
        var issueListScreen = new IssueListScreen(console);
        var issueDetailScreen = new IssueDetailScreen(console);
        var statusUpdateScreen = new StatusUpdateScreen(issueService, console);
        var filterScreen = new FilterScreen(console);

        var running = true;

        while (running)
        {
            try
            {
                console.Clear();

                var choice = await mainMenuScreen.ShowAsync(cancellationToken);

                switch (choice)
                {
                    case MenuChoice.BrowseAll:
                        await BrowseIssuesAsync(
                            issueListScreen, issueDetailScreen, statusUpdateScreen,
                            status: null, type: null, cancellationToken);
                        break;

                    case MenuChoice.BrowseFiltered:
                        console.Clear();
                        var (status, type) = filterScreen.Show();
                        await BrowseIssuesAsync(
                            issueListScreen, issueDetailScreen, statusUpdateScreen,
                            status, type, cancellationToken);
                        break;

                    case MenuChoice.Search:
                        await SearchIssuesAsync(
                            issueListScreen, issueDetailScreen, statusUpdateScreen,
                            cancellationToken);
                        break;

                    case MenuChoice.NextActionable:
                        await ShowNextIssuesAsync(
                            issueListScreen, issueDetailScreen, statusUpdateScreen,
                            cancellationToken);
                        break;

                    case MenuChoice.CreateIssue:
                        await CreateIssueAsync(cancellationToken);
                        break;

                    case MenuChoice.Exit:
                        running = false;
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C — exit cleanly
                running = false;
            }
        }

        console.Clear();
        console.MarkupLine("[dim]Goodbye![/]");
        return 0;
    }

    private async Task BrowseIssuesAsync(
        IssueListScreen issueListScreen,
        IssueDetailScreen issueDetailScreen,
        StatusUpdateScreen statusUpdateScreen,
        IssueStatus? status,
        IssueType? type,
        CancellationToken cancellationToken)
    {
        var browsingList = true;
        while (browsingList)
        {
            console.Clear();

            // Reload fresh data each time
            var issues = await issueService.FilterAsync(
                status: status,
                type: type,
                includeTerminal: status?.IsTerminal() ?? false,
                cancellationToken: cancellationToken);

            var title = BuildFilterTitle(status, type);
            var selected = issueListScreen.Show(issues, title);

            if (selected is null)
            {
                browsingList = false;
            }
            else
            {
                var backToMenu = await ShowIssueDetailAsync(
                    selected, issueDetailScreen, statusUpdateScreen, cancellationToken);
                if (backToMenu)
                {
                    browsingList = false;
                }
            }
        }
    }

    private async Task SearchIssuesAsync(
        IssueListScreen issueListScreen,
        IssueDetailScreen issueDetailScreen,
        StatusUpdateScreen statusUpdateScreen,
        CancellationToken cancellationToken)
    {
        console.Clear();

        var query = console.Prompt(
            new TextPrompt<string>("[bold]Search query:[/]")
                .PromptStyle(new Style(Color.Cyan1)));

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var browsingList = true;
        while (browsingList)
        {
            console.Clear();

            var results = await issueService.SearchAsync(query, cancellationToken);
            var selected = issueListScreen.Show(results, $"Search: {query}");

            if (selected is null)
            {
                browsingList = false;
            }
            else
            {
                var backToMenu = await ShowIssueDetailAsync(
                    selected, issueDetailScreen, statusUpdateScreen, cancellationToken);
                if (backToMenu)
                {
                    browsingList = false;
                }
            }
        }
    }

    private async Task ShowNextIssuesAsync(
        IssueListScreen issueListScreen,
        IssueDetailScreen issueDetailScreen,
        StatusUpdateScreen statusUpdateScreen,
        CancellationToken cancellationToken)
    {
        var browsingList = true;
        while (browsingList)
        {
            console.Clear();

            var issues = await nextService.GetNextIssuesAsync(cancellationToken: cancellationToken);
            var selected = issueListScreen.Show(issues, "Next Actionable Issues");

            if (selected is null)
            {
                browsingList = false;
            }
            else
            {
                var backToMenu = await ShowIssueDetailAsync(
                    selected, issueDetailScreen, statusUpdateScreen, cancellationToken);
                if (backToMenu)
                {
                    browsingList = false;
                }
            }
        }
    }

    /// <summary>
    /// Shows the issue detail screen in a loop. Returns true if the user chose "Back to Main Menu".
    /// </summary>
    private async Task<bool> ShowIssueDetailAsync(
        Issue issue,
        IssueDetailScreen issueDetailScreen,
        StatusUpdateScreen statusUpdateScreen,
        CancellationToken cancellationToken)
    {
        var currentIssue = issue;
        var viewingDetail = true;

        while (viewingDetail)
        {
            console.Clear();

            // Reload issue to get latest state
            var refreshed = await issueService.GetByIdAsync(currentIssue.Id, cancellationToken);
            if (refreshed is not null)
            {
                currentIssue = refreshed;
            }

            var action = issueDetailScreen.Show(currentIssue);

            switch (action)
            {
                case DetailAction.ChangeStatus:
                    console.WriteLine();
                    currentIssue = await statusUpdateScreen.ShowAsync(currentIssue, cancellationToken);
                    // Stay in detail view to see updated status
                    break;

                case DetailAction.EditPriority:
                    console.WriteLine();
                    currentIssue = await EditPriorityAsync(currentIssue, cancellationToken);
                    break;

                case DetailAction.BackToList:
                    viewingDetail = false;
                    return false;

                case DetailAction.BackToMenu:
                    viewingDetail = false;
                    return true;
            }
        }

        return false;
    }

    private async Task<Issue> EditPriorityAsync(Issue issue, CancellationToken cancellationToken)
    {
        var currentPri = issue.Priority?.ToString() ?? "none";

        var input = console.Prompt(
            new TextPrompt<string>($"[bold]Priority[/] (current: {currentPri}, enter 1-5 or 'none'):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(input))
        {
            console.MarkupLine("[dim]Priority unchanged.[/]");
            return issue;
        }

        int? newPriority;
        if (input.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            // Clear priority by setting to 0 (or keep as-is — service decides)
            console.MarkupLine("[dim]Priority clearing is not supported via update. Priority unchanged.[/]");
            return issue;
        }
        else if (int.TryParse(input, out var parsed) && parsed is >= 1 and <= 5)
        {
            newPriority = parsed;
        }
        else
        {
            console.MarkupLine("[red]Invalid priority. Must be 1-5.[/]");
            return issue;
        }

        var updated = await issueService.UpdateAsync(issue.Id, priority: newPriority, cancellationToken: cancellationToken);
        console.MarkupLine($"[green]Priority updated to {newPriority}[/]");
        return updated;
    }

    private async Task CreateIssueAsync(CancellationToken cancellationToken)
    {
        console.Clear();

        var title = console.Prompt(
            new TextPrompt<string>("[bold]Issue title:[/]")
                .PromptStyle(new Style(Color.Cyan1)));

        if (string.IsNullOrWhiteSpace(title))
        {
            console.MarkupLine("[dim]Cancelled.[/]");
            return;
        }

        var type = console.Prompt(
            new SelectionPrompt<IssueType>()
                .Title("[bold]Issue type:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(Enum.GetValues<IssueType>()));

        var description = console.Prompt(
            new TextPrompt<string>("[bold]Description[/] (optional):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        var priInput = console.Prompt(
            new TextPrompt<string>("[bold]Priority 1-5[/] (optional):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        int? priority = null;
        if (!string.IsNullOrWhiteSpace(priInput) && int.TryParse(priInput, out var priValue) && priValue is >= 1 and <= 5)
        {
            priority = priValue;
        }

        await storageService.EnsureDirectoryExistsAsync(cancellationToken);

        var issue = await issueService.CreateAsync(
            title: title,
            type: type,
            description: string.IsNullOrWhiteSpace(description) ? null : description,
            priority: priority,
            cancellationToken: cancellationToken);

        console.MarkupLine($"[green]Created issue[/] [bold]{issue.Id}[/]");
        console.MarkupLine("[dim]Press any key to continue...[/]");
        console.Input.ReadKey(intercept: true);
    }

    private static string BuildFilterTitle(IssueStatus? status, IssueType? type)
    {
        var parts = new List<string> { "Issues" };
        if (status.HasValue)
        {
            parts.Add($"status={status.Value}");
        }
        if (type.HasValue)
        {
            parts.Add($"type={type.Value}");
        }
        return string.Join(" ", parts);
    }
}
