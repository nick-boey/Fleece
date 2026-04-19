using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class ShowCommand(
    IFleeceService fleeceService,
    ISettingsService settingsService,
    IGitConfigService gitConfigService,
    IAnsiConsole console) : AsyncCommand<ShowSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ShowSettings settings)
    {
        IFleeceService fleece = fleeceService;
        if (!string.IsNullOrWhiteSpace(settings.IssuesFile))
        {
            fleece = FleeceService.ForFile(settings.IssuesFile, settingsService, gitConfigService);
        }

        var (hasMultiple, message) = await fleece.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            console.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        var matches = await fleece.ResolveByPartialIdAsync(settings.Id);

        if (matches.Count == 0)
        {
            console.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
            return 1;
        }

        if (matches.Count > 1)
        {
            console.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.Id}':");
            TableFormatter.RenderIssues(console, matches);
            return 1;
        }

        var issue = matches[0];

        if (settings.JsonVerbose)
        {
            JsonFormatter.RenderIssue(issue, verbose: true);
        }
        else
        {
            var allIssues = await fleece.GetAllAsync();
            var showContext = IssueHierarchyHelper.BuildShowContext(issue, allIssues);

            if (settings.Json)
            {
                JsonFormatter.RenderIssueShow(showContext);
            }
            else
            {
                TableFormatter.RenderIssue(console, issue, showContext);
            }
        }

        return 0;
    }
}
