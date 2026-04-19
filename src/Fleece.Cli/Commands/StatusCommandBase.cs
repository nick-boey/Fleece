using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public abstract class StatusCommandBase(IFleeceService fleeceService, IAnsiConsole console)
    : AsyncCommand<StatusSettings>
{
    protected abstract IssueStatus TargetStatus { get; }

    public override async Task<int> ExecuteAsync(CommandContext context, StatusSettings settings)
    {
        if (settings.Ids.Length == 0)
        {
            console.MarkupLine("[red]Error:[/] At least one issue ID is required");
            return 1;
        }

        var (hasMultiple, message) = await fleeceService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            console.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        var resolutionErrors = new List<string>();
        var resolvedIds = new List<string>();

        foreach (var id in settings.Ids)
        {
            var matches = await fleeceService.ResolveByPartialIdAsync(id);

            if (matches.Count == 0)
            {
                resolutionErrors.Add($"Issue '{id}' not found");
            }
            else if (matches.Count > 1)
            {
                var matchingIds = string.Join(", ", matches.Select(m => m.Id));
                resolutionErrors.Add($"Multiple issues match '{id}': {matchingIds}");
            }
            else
            {
                resolvedIds.Add(matches[0].Id);
            }
        }

        if (resolutionErrors.Count > 0)
        {
            foreach (var error in resolutionErrors)
            {
                console.MarkupLine($"[red]Error:[/] {error}");
            }
            return 1;
        }

        var updatedIssues = new List<Issue>();
        foreach (var resolvedId in resolvedIds)
        {
            try
            {
                var issue = await fleeceService.UpdateAsync(
                    id: resolvedId,
                    status: TargetStatus);
                updatedIssues.Add(issue);
            }
            catch (KeyNotFoundException)
            {
                console.MarkupLine($"[red]Error:[/] Issue '{resolvedId}' not found during update");
                return 1;
            }
        }

        if (settings.Json || settings.JsonVerbose)
        {
            JsonFormatter.RenderIssues(updatedIssues, verbose: settings.JsonVerbose);
        }
        else
        {
            var statusName = TargetStatus.ToString().ToLowerInvariant();
            console.MarkupLine($"[green]Updated {updatedIssues.Count} issue(s) to status '{statusName}'[/]");
            TableFormatter.RenderIssues(console, updatedIssues);
        }

        return 0;
    }
}
