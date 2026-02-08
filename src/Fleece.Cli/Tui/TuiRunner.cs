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
    IChangeService changeService,
    ITaskGraphService taskGraphService,
    IGitConfigService gitConfigService,
    IAnsiConsole console)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        // Initialize all screens
        var mainMenuScreen = new MainMenuScreen(issueService, console);
        var issueListScreen = new IssueListScreen(console);
        var issueDetailScreen = new IssueDetailScreen(console);
        var statusUpdateScreen = new StatusUpdateScreen(issueService, console);
        var filterScreen = new FilterScreen(console);
        var editFieldsScreen = new EditFieldsScreen(issueService, console);
        var questionScreen = new QuestionScreen(issueService, gitConfigService, console);
        var changeHistoryScreen = new ChangeHistoryScreen(changeService, console);
        var taskGraphScreen = new TaskGraphScreen(taskGraphService, console);
        var deleteConfirmScreen = new DeleteConfirmScreen(issueService, console);

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
                            editFieldsScreen, questionScreen, changeHistoryScreen,
                            taskGraphScreen, deleteConfirmScreen,
                            filter: null, cancellationToken);
                        break;

                    case MenuChoice.BrowseFiltered:
                        console.Clear();
                        var filter = filterScreen.Show();
                        await BrowseIssuesAsync(
                            issueListScreen, issueDetailScreen, statusUpdateScreen,
                            editFieldsScreen, questionScreen, changeHistoryScreen,
                            taskGraphScreen, deleteConfirmScreen,
                            filter, cancellationToken);
                        break;

                    case MenuChoice.Search:
                        await SearchIssuesAsync(
                            issueListScreen, issueDetailScreen, statusUpdateScreen,
                            editFieldsScreen, questionScreen, changeHistoryScreen,
                            taskGraphScreen, deleteConfirmScreen,
                            cancellationToken);
                        break;

                    case MenuChoice.NextActionable:
                        await ShowNextIssuesAsync(
                            issueListScreen, issueDetailScreen, statusUpdateScreen,
                            editFieldsScreen, questionScreen, changeHistoryScreen,
                            taskGraphScreen, deleteConfirmScreen,
                            cancellationToken);
                        break;

                    case MenuChoice.CreateIssue:
                        await CreateIssueAsync(cancellationToken);
                        break;

                    case MenuChoice.ViewTaskGraph:
                        await taskGraphScreen.ShowAsync(cancellationToken);
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
        EditFieldsScreen editFieldsScreen,
        QuestionScreen questionScreen,
        ChangeHistoryScreen changeHistoryScreen,
        TaskGraphScreen taskGraphScreen,
        DeleteConfirmScreen deleteConfirmScreen,
        FilterCriteria? filter,
        CancellationToken cancellationToken)
    {
        var browsingList = true;
        while (browsingList)
        {
            console.Clear();

            // Reload fresh data each time
            var issues = await issueService.FilterAsync(
                status: filter?.Status,
                type: filter?.Type,
                priority: filter?.Priority,
                assignedTo: filter?.AssignedTo,
                tags: filter?.Tags,
                linkedPr: filter?.LinkedPr,
                includeTerminal: filter?.Status?.IsTerminal() ?? false,
                cancellationToken: cancellationToken);

            var title = BuildFilterTitle(filter);
            var selected = issueListScreen.Show(issues, title);

            if (selected is null)
            {
                browsingList = false;
            }
            else
            {
                var backToMenu = await ShowIssueDetailAsync(
                    selected, issueDetailScreen, statusUpdateScreen,
                    editFieldsScreen, questionScreen, changeHistoryScreen,
                    taskGraphScreen, deleteConfirmScreen,
                    cancellationToken);
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
        EditFieldsScreen editFieldsScreen,
        QuestionScreen questionScreen,
        ChangeHistoryScreen changeHistoryScreen,
        TaskGraphScreen taskGraphScreen,
        DeleteConfirmScreen deleteConfirmScreen,
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
                    selected, issueDetailScreen, statusUpdateScreen,
                    editFieldsScreen, questionScreen, changeHistoryScreen,
                    taskGraphScreen, deleteConfirmScreen,
                    cancellationToken);
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
        EditFieldsScreen editFieldsScreen,
        QuestionScreen questionScreen,
        ChangeHistoryScreen changeHistoryScreen,
        TaskGraphScreen taskGraphScreen,
        DeleteConfirmScreen deleteConfirmScreen,
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
                    selected, issueDetailScreen, statusUpdateScreen,
                    editFieldsScreen, questionScreen, changeHistoryScreen,
                    taskGraphScreen, deleteConfirmScreen,
                    cancellationToken);
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
        EditFieldsScreen editFieldsScreen,
        QuestionScreen questionScreen,
        ChangeHistoryScreen changeHistoryScreen,
        TaskGraphScreen taskGraphScreen,
        DeleteConfirmScreen deleteConfirmScreen,
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
                case DetailAction.EditFields:
                    currentIssue = await editFieldsScreen.ShowAsync(currentIssue, cancellationToken);
                    break;

                case DetailAction.ChangeStatus:
                    console.WriteLine();
                    currentIssue = await statusUpdateScreen.ShowAsync(currentIssue, cancellationToken);
                    // Stay in detail view to see updated status
                    break;

                case DetailAction.Questions:
                    currentIssue = await questionScreen.ShowAsync(currentIssue, cancellationToken);
                    break;

                case DetailAction.ViewHistory:
                    await changeHistoryScreen.ShowAsync(currentIssue, cancellationToken);
                    break;

                case DetailAction.ViewTaskGraph:
                    await taskGraphScreen.ShowAsync(cancellationToken);
                    break;

                case DetailAction.DeleteIssue:
                    console.WriteLine();
                    var deleted = await deleteConfirmScreen.ShowAsync(currentIssue, cancellationToken);
                    if (deleted)
                    {
                        // Issue was deleted, go back to list
                        viewingDetail = false;
                        return false;
                    }
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

    private async Task CreateIssueAsync(CancellationToken cancellationToken)
    {
        console.Clear();

        // Title (required)
        var title = console.Prompt(
            new TextPrompt<string>("[bold]Issue title:[/]")
                .PromptStyle(new Style(Color.Cyan1)));

        if (string.IsNullOrWhiteSpace(title))
        {
            console.MarkupLine("[dim]Cancelled.[/]");
            return;
        }

        // Type (required)
        var type = console.Prompt(
            new SelectionPrompt<IssueType>()
                .Title("[bold]Issue type:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(Enum.GetValues<IssueType>()));

        // Description (optional)
        var description = console.Prompt(
            new TextPrompt<string>("[bold]Description[/] (optional):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        // Priority (optional)
        var priInput = console.Prompt(
            new TextPrompt<string>("[bold]Priority 1-5[/] (optional):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        int? priority = null;
        if (!string.IsNullOrWhiteSpace(priInput) && int.TryParse(priInput, out var priValue) && priValue is >= 1 and <= 5)
        {
            priority = priValue;
        }

        // Status (optional, default Open)
        var statusValues = Enum.GetValues<IssueStatus>().Where(s => s != IssueStatus.Deleted).ToList();
        var status = console.Prompt(
            new SelectionPrompt<IssueStatus>()
                .Title("[bold]Initial status[/] (default: Open):")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(statusValues));

        // Assigned To (optional)
        var assignedTo = console.Prompt(
            new TextPrompt<string>("[bold]Assigned to[/] (optional):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        // Linked PR (optional)
        var linkedPrInput = console.Prompt(
            new TextPrompt<string>("[bold]Linked PR number[/] (optional):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        int? linkedPr = null;
        if (!string.IsNullOrWhiteSpace(linkedPrInput) && int.TryParse(linkedPrInput, out var prValue) && prValue > 0)
        {
            linkedPr = prValue;
        }

        // Linked Issues (optional)
        var linkedIssuesInput = console.Prompt(
            new TextPrompt<string>("[bold]Linked issues[/] (comma-separated IDs, optional):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        IReadOnlyList<string>? linkedIssues = null;
        if (!string.IsNullOrWhiteSpace(linkedIssuesInput))
        {
            linkedIssues = linkedIssuesInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        // Parent Issues (optional)
        var parentIssuesInput = console.Prompt(
            new TextPrompt<string>("[bold]Parent issues[/] (format: id1:sortOrder,id2:sortOrder, optional):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        IReadOnlyList<ParentIssueRef>? parentIssues = null;
        if (!string.IsNullOrWhiteSpace(parentIssuesInput))
        {
            parentIssues = ParentIssueRef.ParseFromStrings(parentIssuesInput);
        }

        // Tags (optional)
        var tagsInput = console.Prompt(
            new TextPrompt<string>("[bold]Tags[/] (comma-separated, optional):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        IReadOnlyList<string>? tags = null;
        if (!string.IsNullOrWhiteSpace(tagsInput))
        {
            tags = tagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        // Working Branch ID (optional)
        var workingBranchId = console.Prompt(
            new TextPrompt<string>("[bold]Working branch ID[/] (optional):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        // Execution Mode (optional)
        var executionMode = console.Prompt(
            new SelectionPrompt<ExecutionMode>()
                .Title("[bold]Execution mode:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(Enum.GetValues<ExecutionMode>()));

        await storageService.EnsureDirectoryExistsAsync(cancellationToken);

        var issue = await issueService.CreateAsync(
            title: title,
            type: type,
            description: string.IsNullOrWhiteSpace(description) ? null : description,
            status: status,
            priority: priority,
            linkedPr: linkedPr,
            linkedIssues: linkedIssues,
            parentIssues: parentIssues,
            assignedTo: string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo,
            tags: tags,
            workingBranchId: string.IsNullOrWhiteSpace(workingBranchId) ? null : workingBranchId,
            executionMode: executionMode,
            cancellationToken: cancellationToken);

        console.MarkupLine($"[green]Created issue[/] [bold]{issue.Id}[/]");
        console.MarkupLine("[dim]Press any key to continue...[/]");
        console.Input.ReadKey(intercept: true);
    }

    private static string BuildFilterTitle(FilterCriteria? filter)
    {
        if (filter is null)
        {
            return "Issues";
        }

        var parts = new List<string> { "Issues" };

        if (filter.Status.HasValue)
        {
            parts.Add($"status={filter.Status.Value}");
        }
        if (filter.Type.HasValue)
        {
            parts.Add($"type={filter.Type.Value}");
        }
        if (filter.Priority.HasValue)
        {
            parts.Add($"priority={filter.Priority.Value}");
        }
        if (!string.IsNullOrEmpty(filter.AssignedTo))
        {
            parts.Add($"assigned={filter.AssignedTo}");
        }
        if (filter.Tags is { Count: > 0 })
        {
            parts.Add($"tags={string.Join("+", filter.Tags)}");
        }
        if (filter.LinkedPr.HasValue)
        {
            parts.Add($"pr=#{filter.LinkedPr.Value}");
        }

        return string.Join(" ", parts);
    }
}
