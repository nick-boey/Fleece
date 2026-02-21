using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public abstract class StatusCommandBase(IIssueServiceFactory issueServiceFactory, IStorageServiceProvider storageServiceProvider)
    : AsyncCommand<StatusSettings>
{
    protected abstract IssueStatus TargetStatus { get; }

    public override async Task<int> ExecuteAsync(CommandContext context, StatusSettings settings)
    {
        // Validate at least one ID provided
        if (settings.Ids.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] At least one issue ID is required");
            return 1;
        }

        var storageService = storageServiceProvider.GetStorageService(settings.IssuesFile);
        var issueService = issueServiceFactory.GetIssueService(settings.IssuesFile);

        // Check for unmerged files
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        // Phase 1: Resolve ALL partial IDs first, collecting any errors
        var resolutionErrors = new List<string>();
        var resolvedIds = new List<string>();

        foreach (var id in settings.Ids)
        {
            var matches = await issueService.ResolveByPartialIdAsync(id);

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

        // If any validation errors, report ALL errors and exit
        if (resolutionErrors.Count > 0)
        {
            foreach (var error in resolutionErrors)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {error}");
            }
            return 1;
        }

        // Phase 2: Update all issues, collect results
        var updatedIssues = new List<Issue>();
        foreach (var resolvedId in resolvedIds)
        {
            try
            {
                var issue = await issueService.UpdateAsync(
                    id: resolvedId,
                    status: TargetStatus);
                updatedIssues.Add(issue);
            }
            catch (KeyNotFoundException)
            {
                // Should not happen since we validated above, but handle gracefully
                AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{resolvedId}' not found during update");
                return 1;
            }
        }

        // Output table or JSON depending on flags
        if (settings.Json || settings.JsonVerbose)
        {
            JsonFormatter.RenderIssues(updatedIssues, verbose: settings.JsonVerbose);
        }
        else
        {
            var statusName = TargetStatus.ToString().ToLowerInvariant();
            AnsiConsole.MarkupLine($"[green]Updated {updatedIssues.Count} issue(s) to status '{statusName}'[/]");
            TableFormatter.RenderIssues(updatedIssues);
        }

        return 0;
    }
}
