using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;

namespace Fleece.Cli.Tui.Screens;

/// <summary>
/// Sub-menu for editing all fields of an issue.
/// </summary>
public sealed class EditFieldsScreen(IIssueService issueService, IAnsiConsole console)
{
    private enum EditField
    {
        Title,
        Description,
        Type,
        Priority,
        AssignedTo,
        LinkedPR,
        LinkedIssues,
        ParentIssues,
        Tags,
        WorkingBranchId,
        ExecutionMode,
        Back
    }

    public async Task<Issue> ShowAsync(Issue issue, CancellationToken cancellationToken)
    {
        var currentIssue = issue;

        while (true)
        {
            console.Clear();
            RenderFieldSummary(currentIssue);
            console.WriteLine();

            var field = console.Prompt(
                new SelectionPrompt<EditField>()
                    .Title("[bold]Select a field to edit:[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .UseConverter(f => f switch
                    {
                        EditField.Title => $"Title: {Markup.Escape(currentIssue.Title)}",
                        EditField.Description => $"Description: {Markup.Escape(Truncate(currentIssue.Description, 50))}",
                        EditField.Type => $"Type: {currentIssue.Type}",
                        EditField.Priority => $"Priority: {currentIssue.Priority?.ToString() ?? "(none)"}",
                        EditField.AssignedTo => $"Assigned To: {Markup.Escape(currentIssue.AssignedTo ?? "(none)")}",
                        EditField.LinkedPR => $"Linked PR: {(currentIssue.LinkedPR.HasValue ? $"#{currentIssue.LinkedPR}" : "(none)")}",
                        EditField.LinkedIssues => $"Linked Issues: {(currentIssue.LinkedIssues.Count > 0 ? string.Join(", ", currentIssue.LinkedIssues) : "(none)")}",
                        EditField.ParentIssues => $"Parent Issues: {(currentIssue.ParentIssues.Count > 0 ? string.Join(", ", currentIssue.ParentIssues.Select(p => $"{p.ParentIssue}:{p.SortOrder}")) : "(none)")}",
                        EditField.Tags => $"Tags: {(currentIssue.Tags.Count > 0 ? string.Join(", ", currentIssue.Tags) : "(none)")}",
                        EditField.WorkingBranchId => $"Working Branch: {Markup.Escape(currentIssue.WorkingBranchId ?? "(none)")}",
                        EditField.ExecutionMode => $"Execution Mode: {currentIssue.ExecutionMode}",
                        EditField.Back => "[dim]<< Back[/]",
                        _ => f.ToString()
                    })
                    .AddChoices(Enum.GetValues<EditField>()));

            if (field == EditField.Back)
            {
                return currentIssue;
            }

            currentIssue = await EditFieldAsync(currentIssue, field, cancellationToken);
        }
    }

    private async Task<Issue> EditFieldAsync(Issue issue, EditField field, CancellationToken cancellationToken)
    {
        try
        {
            return field switch
            {
                EditField.Title => await EditTitleAsync(issue, cancellationToken),
                EditField.Description => await EditDescriptionAsync(issue, cancellationToken),
                EditField.Type => await EditTypeAsync(issue, cancellationToken),
                EditField.Priority => await EditPriorityAsync(issue, cancellationToken),
                EditField.AssignedTo => await EditAssignedToAsync(issue, cancellationToken),
                EditField.LinkedPR => await EditLinkedPRAsync(issue, cancellationToken),
                EditField.LinkedIssues => await EditLinkedIssuesAsync(issue, cancellationToken),
                EditField.ParentIssues => await EditParentIssuesAsync(issue, cancellationToken),
                EditField.Tags => await EditTagsAsync(issue, cancellationToken),
                EditField.WorkingBranchId => await EditWorkingBranchIdAsync(issue, cancellationToken),
                EditField.ExecutionMode => await EditExecutionModeAsync(issue, cancellationToken),
                _ => issue
            };
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            console.MarkupLine("[dim]Press any key to continue...[/]");
            console.Input.ReadKey(intercept: true);
            return issue;
        }
    }

    private async Task<Issue> EditTitleAsync(Issue issue, CancellationToken cancellationToken)
    {
        var input = console.Prompt(
            new TextPrompt<string>($"[bold]Title[/] (current: {Markup.Escape(issue.Title)}):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(input))
        {
            console.MarkupLine("[dim]Title unchanged.[/]");
            return issue;
        }

        var updated = await issueService.UpdateAsync(issue.Id, title: input, cancellationToken: cancellationToken);
        console.MarkupLine($"[green]Title updated.[/]");
        return updated;
    }

    private async Task<Issue> EditDescriptionAsync(Issue issue, CancellationToken cancellationToken)
    {
        var currentDesc = issue.Description ?? "(none)";
        console.MarkupLine($"[bold]Current description:[/] {Markup.Escape(currentDesc)}");
        var input = console.Prompt(
            new TextPrompt<string>("[bold]New description[/] (enter 'clear' to remove, empty to keep):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(input))
        {
            console.MarkupLine("[dim]Description unchanged.[/]");
            return issue;
        }

        var newDescription = input.Equals("clear", StringComparison.OrdinalIgnoreCase) ? "" : input;
        var updated = await issueService.UpdateAsync(issue.Id, description: newDescription, cancellationToken: cancellationToken);
        console.MarkupLine($"[green]Description updated.[/]");
        return updated;
    }

    private async Task<Issue> EditTypeAsync(Issue issue, CancellationToken cancellationToken)
    {
        var newType = console.Prompt(
            new SelectionPrompt<IssueType>()
                .Title($"[bold]Type[/] (current: {issue.Type}):")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(Enum.GetValues<IssueType>()));

        if (newType == issue.Type)
        {
            console.MarkupLine("[dim]Type unchanged.[/]");
            return issue;
        }

        var updated = await issueService.UpdateAsync(issue.Id, type: newType, cancellationToken: cancellationToken);
        console.MarkupLine($"[green]Type updated to {newType}.[/]");
        return updated;
    }

    private async Task<Issue> EditPriorityAsync(Issue issue, CancellationToken cancellationToken)
    {
        var currentPri = issue.Priority?.ToString() ?? "none";
        var input = console.Prompt(
            new TextPrompt<string>($"[bold]Priority[/] (current: {currentPri}, enter 1-5 or 'none' to clear):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(input))
        {
            console.MarkupLine("[dim]Priority unchanged.[/]");
            return issue;
        }

        if (input.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            var updated = await issueService.UpdateAsync(issue.Id, priority: 0, cancellationToken: cancellationToken);
            console.MarkupLine("[green]Priority cleared (set to 0).[/]");
            return updated;
        }

        if (int.TryParse(input, out var parsed) && parsed is >= 1 and <= 5)
        {
            var updated = await issueService.UpdateAsync(issue.Id, priority: parsed, cancellationToken: cancellationToken);
            console.MarkupLine($"[green]Priority updated to {parsed}.[/]");
            return updated;
        }

        console.MarkupLine("[red]Invalid priority. Must be 1-5 or 'none'.[/]");
        return issue;
    }

    private async Task<Issue> EditAssignedToAsync(Issue issue, CancellationToken cancellationToken)
    {
        var current = issue.AssignedTo ?? "(none)";
        var input = console.Prompt(
            new TextPrompt<string>($"[bold]Assigned To[/] (current: {Markup.Escape(current)}, enter 'clear' to remove):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(input))
        {
            console.MarkupLine("[dim]Assigned To unchanged.[/]");
            return issue;
        }

        var newValue = input.Equals("clear", StringComparison.OrdinalIgnoreCase) ? "" : input;
        var updated = await issueService.UpdateAsync(issue.Id, assignedTo: newValue, cancellationToken: cancellationToken);
        console.MarkupLine($"[green]Assigned To updated.[/]");
        return updated;
    }

    private async Task<Issue> EditLinkedPRAsync(Issue issue, CancellationToken cancellationToken)
    {
        var current = issue.LinkedPR?.ToString() ?? "none";
        var input = console.Prompt(
            new TextPrompt<string>($"[bold]Linked PR[/] (current: {current}, enter number or 'none' to clear):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(input))
        {
            console.MarkupLine("[dim]Linked PR unchanged.[/]");
            return issue;
        }

        if (input.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            var updated = await issueService.UpdateAsync(issue.Id, linkedPr: 0, cancellationToken: cancellationToken);
            console.MarkupLine("[green]Linked PR cleared.[/]");
            return updated;
        }

        if (int.TryParse(input, out var prNumber) && prNumber > 0)
        {
            var updated = await issueService.UpdateAsync(issue.Id, linkedPr: prNumber, cancellationToken: cancellationToken);
            console.MarkupLine($"[green]Linked PR updated to #{prNumber}.[/]");
            return updated;
        }

        console.MarkupLine("[red]Invalid PR number.[/]");
        return issue;
    }

    private async Task<Issue> EditLinkedIssuesAsync(Issue issue, CancellationToken cancellationToken)
    {
        var current = issue.LinkedIssues.Count > 0 ? string.Join(", ", issue.LinkedIssues) : "(none)";
        var input = console.Prompt(
            new TextPrompt<string>($"[bold]Linked Issues[/] (current: {current})\nEnter comma-separated IDs or 'clear' to remove all:")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(input))
        {
            console.MarkupLine("[dim]Linked Issues unchanged.[/]");
            return issue;
        }

        IReadOnlyList<string> newLinkedIssues;
        if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            newLinkedIssues = [];
        }
        else
        {
            newLinkedIssues = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        var updated = await issueService.UpdateAsync(issue.Id, linkedIssues: newLinkedIssues, cancellationToken: cancellationToken);
        console.MarkupLine($"[green]Linked Issues updated.[/]");
        return updated;
    }

    private async Task<Issue> EditParentIssuesAsync(Issue issue, CancellationToken cancellationToken)
    {
        var current = issue.ParentIssues.Count > 0
            ? string.Join(", ", issue.ParentIssues.Select(p => $"{p.ParentIssue}:{p.SortOrder}"))
            : "(none)";
        var input = console.Prompt(
            new TextPrompt<string>($"[bold]Parent Issues[/] (current: {current})\nFormat: id1:sortOrder,id2:sortOrder or 'clear':")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(input))
        {
            console.MarkupLine("[dim]Parent Issues unchanged.[/]");
            return issue;
        }

        IReadOnlyList<ParentIssueRef> newParents;
        if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            newParents = [];
        }
        else
        {
            newParents = ParentIssueRef.ParseFromStrings(input);
        }

        var updated = await issueService.UpdateAsync(issue.Id, parentIssues: newParents, cancellationToken: cancellationToken);
        console.MarkupLine($"[green]Parent Issues updated.[/]");
        return updated;
    }

    private async Task<Issue> EditTagsAsync(Issue issue, CancellationToken cancellationToken)
    {
        var current = issue.Tags.Count > 0 ? string.Join(", ", issue.Tags) : "(none)";
        var input = console.Prompt(
            new TextPrompt<string>($"[bold]Tags[/] (current: {current})\nEnter comma-separated tags or 'clear' to remove all:")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(input))
        {
            console.MarkupLine("[dim]Tags unchanged.[/]");
            return issue;
        }

        IReadOnlyList<string> newTags;
        if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            newTags = [];
        }
        else
        {
            newTags = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        var updated = await issueService.UpdateAsync(issue.Id, tags: newTags, cancellationToken: cancellationToken);
        console.MarkupLine($"[green]Tags updated.[/]");
        return updated;
    }

    private async Task<Issue> EditWorkingBranchIdAsync(Issue issue, CancellationToken cancellationToken)
    {
        var current = issue.WorkingBranchId ?? "(none)";
        var input = console.Prompt(
            new TextPrompt<string>($"[bold]Working Branch[/] (current: {Markup.Escape(current)}, enter 'clear' to remove):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(input))
        {
            console.MarkupLine("[dim]Working Branch unchanged.[/]");
            return issue;
        }

        var newValue = input.Equals("clear", StringComparison.OrdinalIgnoreCase) ? "" : input;
        var updated = await issueService.UpdateAsync(issue.Id, workingBranchId: newValue, cancellationToken: cancellationToken);
        console.MarkupLine($"[green]Working Branch updated.[/]");
        return updated;
    }

    private async Task<Issue> EditExecutionModeAsync(Issue issue, CancellationToken cancellationToken)
    {
        var newMode = console.Prompt(
            new SelectionPrompt<ExecutionMode>()
                .Title($"[bold]Execution Mode[/] (current: {issue.ExecutionMode}):")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(Enum.GetValues<ExecutionMode>()));

        if (newMode == issue.ExecutionMode)
        {
            console.MarkupLine("[dim]Execution Mode unchanged.[/]");
            return issue;
        }

        var updated = await issueService.UpdateAsync(issue.Id, executionMode: newMode, cancellationToken: cancellationToken);
        console.MarkupLine($"[green]Execution Mode updated to {newMode}.[/]");
        return updated;
    }

    private void RenderFieldSummary(Issue issue)
    {
        var lines = new List<string>
        {
            $"[bold]Title:[/]          {Markup.Escape(issue.Title)}",
            $"[bold]Description:[/]    {Markup.Escape(Truncate(issue.Description, 60))}",
            $"[bold]Type:[/]           {issue.Type}",
            $"[bold]Priority:[/]       {issue.Priority?.ToString() ?? "(none)"}",
            $"[bold]Assigned To:[/]    {Markup.Escape(issue.AssignedTo ?? "(none)")}",
            $"[bold]Linked PR:[/]      {(issue.LinkedPR.HasValue ? $"#{issue.LinkedPR}" : "(none)")}",
            $"[bold]Linked Issues:[/]  {(issue.LinkedIssues.Count > 0 ? string.Join(", ", issue.LinkedIssues) : "(none)")}",
            $"[bold]Parent Issues:[/]  {(issue.ParentIssues.Count > 0 ? string.Join(", ", issue.ParentIssues.Select(p => $"{p.ParentIssue}:{p.SortOrder}")) : "(none)")}",
            $"[bold]Tags:[/]           {(issue.Tags.Count > 0 ? string.Join(", ", issue.Tags.Select(Markup.Escape)) : "(none)")}",
            $"[bold]Working Branch:[/] {Markup.Escape(issue.WorkingBranchId ?? "(none)")}",
            $"[bold]Execution Mode:[/] {issue.ExecutionMode}"
        };

        var panel = new Panel(string.Join("\n", lines))
        {
            Header = new PanelHeader($"[bold]Edit Fields — {issue.Id}[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };

        console.Write(panel);
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(none)";
        }

        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
