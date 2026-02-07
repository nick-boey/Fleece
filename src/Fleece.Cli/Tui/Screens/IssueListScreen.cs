using Fleece.Core.Models;
using Spectre.Console;

namespace Fleece.Cli.Tui.Screens;

/// <summary>
/// Displays a list of issues as a table and allows selection.
/// Returns the selected issue or null if the user chose "Back".
/// </summary>
public sealed class IssueListScreen(IAnsiConsole console)
{
    public Issue? Show(IReadOnlyList<Issue> issues, string title = "Issues")
    {
        if (issues.Count == 0)
        {
            console.MarkupLine("[dim]No issues found.[/]");
            console.MarkupLine("[dim]Press any key to go back...[/]");
            console.Input.ReadKey(intercept: true);
            return null;
        }

        // Render a summary table first
        RenderTable(issues);
        console.WriteLine();

        // Build selection choices: issues + Back option
        // Use a wrapper to allow null sentinel for "Back"
        var choices = new List<IssueChoice>(issues.Count + 1);
        foreach (var issue in issues)
        {
            choices.Add(new IssueChoice(issue));
        }
        choices.Add(IssueChoice.Back);

        var selected = console.Prompt(
            new SelectionPrompt<IssueChoice>()
                .Title($"[bold]{Markup.Escape(title)}[/] — Select an issue or go back:")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Cyan1))
                .UseConverter(c => c.DisplayText)
                .AddChoices(choices));

        return selected.Issue;
    }

    private void RenderTable(IReadOnlyList<Issue> issues)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);

        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn(new TableColumn("Title"));
        table.AddColumn(new TableColumn("Type").Centered());
        table.AddColumn(new TableColumn("Status").Centered());
        table.AddColumn(new TableColumn("Pri").Centered());

        foreach (var issue in issues)
        {
            var statusColor = GetStatusColor(issue.Status);
            var typeColor = GetTypeColor(issue.Type);
            var priDisplay = issue.Priority?.ToString() ?? "-";

            table.AddRow(
                $"[bold]{issue.Id}[/]",
                Markup.Escape(issue.Title),
                $"[{typeColor}]{issue.Type}[/]",
                $"[{statusColor}]{issue.Status}[/]",
                priDisplay);
        }

        console.Write(table);
        console.MarkupLine($"[dim]{issues.Count} issue(s)[/]");
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

    private static string GetTypeColor(IssueType type) => type switch
    {
        IssueType.Bug => "red",
        IssueType.Feature => "cyan",
        IssueType.Task => "white",
        IssueType.Chore => "dim",
        _ => "white"
    };

    /// <summary>
    /// Wrapper around Issue? that supports a "Back" sentinel and provides display text.
    /// </summary>
    private sealed class IssueChoice
    {
        public static readonly IssueChoice Back = new();

        public Issue? Issue { get; }
        public string DisplayText { get; }

        public IssueChoice(Issue issue)
        {
            Issue = issue;
            var statusColor = GetStatusColor(issue.Status);
            DisplayText = $"{issue.Id} [{statusColor}]{issue.Status}[/] {Markup.Escape(issue.Title)}";
        }

        private IssueChoice()
        {
            Issue = null;
            DisplayText = "[dim]<< Back[/]";
        }
    }
}
