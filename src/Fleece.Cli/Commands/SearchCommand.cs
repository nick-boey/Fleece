using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class SearchCommand(IIssueService issueService) : AsyncCommand<SearchSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SearchSettings settings)
    {
        var issues = await issueService.SearchAsync(settings.Query);

        if (settings.Json)
        {
            JsonFormatter.RenderIssues(issues);
        }
        else
        {
            TableFormatter.RenderIssues(issues);
        }

        return 0;
    }
}
