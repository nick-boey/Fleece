using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class ListCommand(IIssueService issueService, IStorageService storageService) : AsyncCommand<ListSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

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
        else if (!settings.All)
        {
            // Default to showing only open issues unless --all is specified
            status = IssueStatus.Open;
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

        var issues = await issueService.FilterAsync(status, type, settings.Priority, settings.Group, settings.AssignedTo);

        if (settings.Json || settings.JsonVerbose)
        {
            JsonFormatter.RenderIssues(issues, verbose: settings.JsonVerbose);
        }
        else
        {
            TableFormatter.RenderIssues(issues);
        }

        return 0;
    }
}
