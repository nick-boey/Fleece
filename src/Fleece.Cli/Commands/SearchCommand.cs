using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class SearchCommand(IFleeceService fleeceService, IAnsiConsole console) : AsyncCommand<SearchSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SearchSettings settings)
    {
        var (hasMultiple, message) = await fleeceService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            console.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        var issues = await fleeceService.SearchAsync(settings.Query);

        if (settings.Json || settings.JsonVerbose)
        {
            JsonFormatter.RenderIssues(issues, verbose: settings.JsonVerbose);
        }
        else
        {
            TableFormatter.RenderIssues(console, issues);
        }

        return 0;
    }
}
