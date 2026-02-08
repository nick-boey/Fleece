using Fleece.Core.Models;
using Spectre.Console;

namespace Fleece.Cli.Tui.Screens;

/// <summary>
/// Criteria for filtering issues in the TUI.
/// </summary>
public sealed record FilterCriteria(
    IssueStatus? Status,
    IssueType? Type,
    int? Priority,
    string? AssignedTo,
    IReadOnlyList<string>? Tags,
    int? LinkedPr);

/// <summary>
/// Displays filter selection prompts for status, type, priority, assigned to, tags, and linked PR.
/// </summary>
public sealed class FilterScreen(IAnsiConsole console)
{
    public FilterCriteria Show()
    {
        // Status filter
        var statusChoices = new List<string> { "All" };
        statusChoices.AddRange(
            Enum.GetValues<IssueStatus>()
                .Where(s => s != IssueStatus.Deleted)
                .Select(s => s.ToString()));

        var statusChoice = console.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Filter by status:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(statusChoices));

        IssueStatus? status = statusChoice == "All"
            ? null
            : Enum.Parse<IssueStatus>(statusChoice);

        // Type filter
        var typeChoices = new List<string> { "All" };
        typeChoices.AddRange(Enum.GetValues<IssueType>().Select(t => t.ToString()));

        var typeChoice = console.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Filter by type:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(typeChoices));

        IssueType? type = typeChoice == "All"
            ? null
            : Enum.Parse<IssueType>(typeChoice);

        // Priority filter
        var priorityChoices = new List<string> { "All", "1", "2", "3", "4", "5" };
        var priorityChoice = console.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Filter by priority:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(priorityChoices));

        int? priority = priorityChoice == "All"
            ? null
            : int.Parse(priorityChoice);

        // Assigned To filter
        var assignedToInput = console.Prompt(
            new TextPrompt<string>("[bold]Filter by assigned to[/] (leave empty for all):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        string? assignedTo = string.IsNullOrWhiteSpace(assignedToInput) ? null : assignedToInput;

        // Tags filter
        var tagsInput = console.Prompt(
            new TextPrompt<string>("[bold]Filter by tags[/] (comma-separated, leave empty for all):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        IReadOnlyList<string>? tags = string.IsNullOrWhiteSpace(tagsInput)
            ? null
            : tagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        // Linked PR filter
        var linkedPrInput = console.Prompt(
            new TextPrompt<string>("[bold]Filter by linked PR[/] (number, leave empty for all):")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        int? linkedPr = null;
        if (!string.IsNullOrWhiteSpace(linkedPrInput) && int.TryParse(linkedPrInput, out var prValue))
        {
            linkedPr = prValue;
        }

        return new FilterCriteria(status, type, priority, assignedTo, tags, linkedPr);
    }
}
