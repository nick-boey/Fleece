using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class CreateCommand(IIssueService issueService, IStorageService storageService) : AsyncCommand<CreateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CreateSettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(settings.Title))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --title is required");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(settings.Type))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --type is required");
            return 1;
        }

        if (!Enum.TryParse<IssueType>(settings.Type, ignoreCase: true, out var issueType))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid type '{settings.Type}'. Use: task, bug, chore, idea, feature");
            return 1;
        }

        var status = IssueStatus.Open;
        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            if (!Enum.TryParse<IssueStatus>(settings.Status, ignoreCase: true, out status))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{settings.Status}'. Use: open, complete, closed, archived");
                return 1;
            }
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

        await storageService.EnsureDirectoryExistsAsync();

        var issue = await issueService.CreateAsync(
            title: settings.Title,
            type: issueType,
            description: settings.Description,
            status: status,
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
            AnsiConsole.MarkupLine($"[green]Created issue[/] [bold]{issue.Id}[/]");
            TableFormatter.RenderIssue(issue);
        }

        return 0;
    }
}
