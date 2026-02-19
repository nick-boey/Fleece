using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class DeleteCommand(IIssueServiceFactory issueServiceFactory, IStorageServiceProvider storageServiceProvider) : AsyncCommand<DeleteSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeleteSettings settings)
    {
        var storageService = storageServiceProvider.GetStorageService(settings.IssuesFile);
        var issueService = issueServiceFactory.GetIssueService(settings.IssuesFile);
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        var matches = await issueService.ResolveByPartialIdAsync(settings.Id);

        if (matches.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
            return 1;
        }

        if (matches.Count > 1)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.Id}':");
            TableFormatter.RenderIssues(matches);
            return 1;
        }

        var resolvedId = matches[0].Id;
        var deleted = await issueService.DeleteAsync(resolvedId);

        if (deleted)
        {
            AnsiConsole.MarkupLine($"[green]Deleted issue[/] [bold]{resolvedId}[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{resolvedId}' not found");
            return 1;
        }
    }
}
