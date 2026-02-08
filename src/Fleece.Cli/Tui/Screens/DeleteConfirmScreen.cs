using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;

namespace Fleece.Cli.Tui.Screens;

/// <summary>
/// Confirmation dialog for deleting an issue.
/// </summary>
public sealed class DeleteConfirmScreen(IIssueService issueService, IAnsiConsole console)
{
    /// <summary>
    /// Shows a confirmation dialog for deleting an issue.
    /// Returns true if the issue was deleted.
    /// </summary>
    public async Task<bool> ShowAsync(Issue issue, CancellationToken cancellationToken)
    {
        console.MarkupLine($"[bold red]Delete issue {issue.Id}?[/]");
        console.MarkupLine($"[dim]Title: {Markup.Escape(issue.Title)}[/]");
        console.WriteLine();

        var choice = console.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Are you sure?[/]")
                .HighlightStyle(new Style(Color.Red))
                .AddChoices("No, cancel", "[red]Yes, delete[/]"));

        if (choice == "No, cancel")
        {
            console.MarkupLine("[dim]Cancelled.[/]");
            return false;
        }

        try
        {
            var deleted = await issueService.DeleteAsync(issue.Id, cancellationToken);
            if (deleted)
            {
                console.MarkupLine($"[green]Issue {issue.Id} deleted.[/]");
            }
            else
            {
                console.MarkupLine($"[red]Failed to delete issue {issue.Id}.[/]");
            }

            console.MarkupLine("[dim]Press any key to continue...[/]");
            console.Input.ReadKey(intercept: true);
            return deleted;
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            console.MarkupLine("[dim]Press any key to continue...[/]");
            console.Input.ReadKey(intercept: true);
            return false;
        }
    }
}
