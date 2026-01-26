using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class ShowCommand(IIssueService issueService, IStorageService storageService) : AsyncCommand<ShowSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ShowSettings settings)
    {
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

        var issue = matches[0];

        if (settings.JsonVerbose)
        {
            JsonFormatter.RenderIssue(issue, verbose: true);
        }
        else if (settings.Json)
        {
            JsonFormatter.RenderIssue(issue, verbose: false);
        }
        else
        {
            TableFormatter.RenderIssue(issue);
        }

        return 0;
    }
}
