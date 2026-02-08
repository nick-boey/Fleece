using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;

namespace Fleece.Cli.Tui.Screens;

/// <summary>
/// Displays the change history for an issue.
/// </summary>
public sealed class ChangeHistoryScreen(IChangeService changeService, IAnsiConsole console)
{
    public async Task ShowAsync(Issue issue, CancellationToken cancellationToken)
    {
        console.Clear();

        var changes = await changeService.GetByIssueIdAsync(issue.Id, cancellationToken);

        if (changes.Count == 0)
        {
            console.MarkupLine($"[dim]No change history found for {issue.Id}.[/]");
            console.MarkupLine("[dim]Press any key to go back...[/]");
            console.Input.ReadKey(intercept: true);
            return;
        }

        var header = new Rule($"[bold]Change History — {issue.Id}[/]");
        header.Style = Style.Parse("cyan");
        console.Write(header);
        console.WriteLine();

        foreach (var change in changes.OrderByDescending(c => c.ChangedAt))
        {
            var typeColor = change.Type switch
            {
                ChangeType.Created => "green",
                ChangeType.Updated => "yellow",
                ChangeType.Deleted => "red",
                ChangeType.Merged => "cyan",
                _ => "white"
            };

            var contentLines = new List<string>
            {
                $"[bold]Change ID:[/] {change.ChangeId}",
                $"[bold]Type:[/]      [{typeColor}]{change.Type}[/]",
                $"[bold]Changed By:[/] {Markup.Escape(change.ChangedBy)}",
                $"[bold]Changed At:[/] {change.ChangedAt:yyyy-MM-dd HH:mm:ss}"
            };

            var panel = new Panel(string.Join("\n", contentLines))
            {
                Header = new PanelHeader($"[{typeColor}]{change.Type}[/] by {Markup.Escape(change.ChangedBy)}"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(2, 1)
            };

            console.Write(panel);

            // Show property changes if available
            if (change.PropertyChanges.Count > 0)
            {
                RenderPropertyChanges(change.PropertyChanges);
            }

            console.WriteLine();
        }

        console.MarkupLine($"[dim]{changes.Count} change(s). Press any key to go back...[/]");
        console.Input.ReadKey(intercept: true);
    }

    private void RenderPropertyChanges(IReadOnlyList<PropertyChange> propertyChanges)
    {
        if (propertyChanges.Count == 0)
        {
            return;
        }

        var table = new Table();
        table.Border(TableBorder.Simple);
        table.AddColumn(new TableColumn("Property").Centered());
        table.AddColumn(new TableColumn("Old Value"));
        table.AddColumn(new TableColumn("New Value"));
        table.AddColumn(new TableColumn("Resolution").Centered());

        foreach (var pc in propertyChanges)
        {
            var resolutionColor = pc.MergeResolution switch
            {
                "A" => "cyan",
                "B" => "magenta",
                "Union" => "green",
                _ => "dim"
            };

            var resolutionDisplay = pc.MergeResolution ?? "-";

            table.AddRow(
                $"[bold]{pc.PropertyName}[/]",
                Markup.Escape(pc.OldValue ?? "(null)"),
                Markup.Escape(pc.NewValue ?? "(null)"),
                $"[{resolutionColor}]{resolutionDisplay}[/]"
            );
        }

        console.Write(table);
    }
}
