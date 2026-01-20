using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class EditCommand(IIssueService issueService) : AsyncCommand<EditSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, EditSettings settings)
    {
        IssueStatus? status = null;
        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            if (!Enum.TryParse<IssueStatus>(settings.Status, ignoreCase: true, out var parsedStatus))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{settings.Status}'. Use: open, complete, closed, archived");
                return 1;
            }
            status = parsedStatus;
        }

        IssueType? type = null;
        if (!string.IsNullOrWhiteSpace(settings.Type))
        {
            if (!Enum.TryParse<IssueType>(settings.Type, ignoreCase: true, out var parsedType))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid type '{settings.Type}'. Use: task, bug, chore, idea, feature");
                return 1;
            }
            type = parsedType;
        }

        IReadOnlyList<string>? linkedIssues = null;
        if (!string.IsNullOrWhiteSpace(settings.LinkedIssues))
        {
            linkedIssues = settings.LinkedIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        IReadOnlyList<string>? parentIssues = null;
        if (!string.IsNullOrWhiteSpace(settings.ParentIssues))
        {
            parentIssues = settings.ParentIssues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        try
        {
            var issue = await issueService.UpdateAsync(
                id: settings.Id,
                title: settings.Title,
                description: settings.Description,
                status: status,
                type: type,
                priority: settings.Priority,
                linkedPr: settings.LinkedPr,
                linkedIssues: linkedIssues,
                parentIssues: parentIssues,
                group: settings.Group,
                assignedTo: settings.AssignedTo);

            if (settings.Json)
            {
                JsonFormatter.RenderIssue(issue);
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Updated issue[/] [bold]{issue.Id}[/]");
                TableFormatter.RenderIssue(issue);
            }

            return 0;
        }
        catch (KeyNotFoundException)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
            return 1;
        }
    }
}
