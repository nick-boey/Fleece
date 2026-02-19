using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class SearchCommand(IIssueServiceFactory issueServiceFactory, IStorageServiceProvider storageServiceProvider) : AsyncCommand<SearchSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SearchSettings settings)
    {
        var storageService = storageServiceProvider.GetStorageService(settings.IssuesFile);
        var issueService = issueServiceFactory.GetIssueService(settings.IssuesFile);
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        var issues = await issueService.SearchAsync(settings.Query);

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
