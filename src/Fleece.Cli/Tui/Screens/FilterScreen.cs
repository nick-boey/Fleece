using Fleece.Core.Models;
using Spectre.Console;

namespace Fleece.Cli.Tui.Screens;

/// <summary>
/// Displays filter selection prompts for status and type.
/// </summary>
public sealed class FilterScreen(IAnsiConsole console)
{
    public (IssueStatus? Status, IssueType? Type) Show()
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

        return (status, type);
    }
}
