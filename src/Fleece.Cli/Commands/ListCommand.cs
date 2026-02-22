using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class ListCommand(
    IIssueServiceFactory issueServiceFactory,
    IStorageServiceProvider storageServiceProvider,
    ISyncStatusService syncStatusService) : AsyncCommand<ListSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings)
    {
        var storageService = storageServiceProvider.GetStorageService(settings.IssuesFile);
        var issueService = issueServiceFactory.GetIssueService(settings.IssuesFile);
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        // --tree-root implies --tree
        var isTree = settings.Tree || !string.IsNullOrWhiteSpace(settings.TreeRoot);

        // Validate mutually exclusive options
        if (isTree && settings.Next)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --tree and --next cannot be used together");
            return 1;
        }

        if ((isTree || settings.Next) && settings.OneLine)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --tree/--next cannot be used with --one-line");
            return 1;
        }

        if (settings.Next && settings.Json)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --next and --json cannot be used together");
            return 1;
        }

        // --- Next mode ---
        if (settings.Next)
        {
            var graph = await issueService.BuildTaskGraphLayoutAsync();
            TaskGraphRenderer.Render(graph);
            return 0;
        }

        // --- Tree mode and default list mode share filtering/diagnostics ---

        // Load issues with diagnostics (only for non-next modes)
        var loadResult = await storageService.LoadIssuesWithDiagnosticsAsync();
        var hasWarnings = DiagnosticFormatter.RenderDiagnostics(loadResult.Diagnostics);

        // Fail early in strict mode if there are warnings
        if (settings.Strict && hasWarnings)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Schema warnings detected in strict mode.");
            return 1;
        }

        IssueStatus? status = null;
        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            if (!Enum.TryParse<IssueStatus>(settings.Status, ignoreCase: true, out var parsedStatus))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{settings.Status}'. Use: open, progress, review, complete, archived, closed");
                return 1;
            }
            status = parsedStatus;
        }

        IssueType? type = null;
        if (!string.IsNullOrWhiteSpace(settings.Type))
        {
            if (!Enum.TryParse<IssueType>(settings.Type, ignoreCase: true, out var parsedType))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid type '{settings.Type}'. Use: task, bug, chore, feature, idea");
                return 1;
            }
            type = parsedType;
        }

        // Validate mutually exclusive output options
        if (settings.OneLine && (settings.Json || settings.JsonVerbose))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --one-line cannot be used with --json or --json-verbose");
            return 1;
        }

        // Apply filtering via the issue service
        var issues = await issueService.FilterAsync(
            status,
            type,
            settings.Priority,
            settings.AssignedTo,
            settings.Tags,
            settings.LinkedPr,
            settings.All);

        // --- Tree mode ---
        if (isTree)
        {
            return await ExecuteTreeMode(issueService, issues.ToList(), settings);
        }

        // --- Default list mode ---
        // Get sync statuses if requested
        IReadOnlyDictionary<string, SyncStatus>? syncStatuses = null;
        if (settings.SyncStatus)
        {
            syncStatuses = await syncStatusService.GetSyncStatusesAsync();
        }

        if (settings.Json || settings.JsonVerbose)
        {
            JsonFormatter.RenderIssues(issues, verbose: settings.JsonVerbose, syncStatuses: syncStatuses);
        }
        else if (settings.OneLine)
        {
            RenderOneLine(issues, syncStatuses);
        }
        else
        {
            TableFormatter.RenderIssues(issues, syncStatuses);
        }

        return 0;
    }

    private static async Task<int> ExecuteTreeMode(
        IIssueService issueService,
        List<Issue> issueList,
        ListSettings settings)
    {
        // Resolve optional root issue ID
        Issue? rootIssue = null;
        if (!string.IsNullOrWhiteSpace(settings.TreeRoot))
        {
            var matches = await issueService.ResolveByPartialIdAsync(settings.TreeRoot);

            if (matches.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{settings.TreeRoot}' not found");
                return 1;
            }

            if (matches.Count > 1)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.TreeRoot}':");
                foreach (var match in matches)
                {
                    AnsiConsole.MarkupLine($"  {match.Id} {Markup.Escape(match.Title)}");
                }
                return 1;
            }

            rootIssue = matches[0];
        }

        // When a root issue is specified, constrain to the root + its transitive descendants
        if (rootIssue is not null)
        {
            issueList = TreeRenderer.ScopeToDescendants(rootIssue, issueList);
        }

        if (settings.Json)
        {
            TreeRenderer.RenderJsonTree(issueList);
        }
        else
        {
            TreeRenderer.RenderTree(issueList);
        }

        return 0;
    }

    private static void RenderOneLine(IReadOnlyList<Issue> issues, IReadOnlyDictionary<string, SyncStatus>? syncStatuses)
    {
        if (issues.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No issues found[/]");
            return;
        }

        foreach (var issue in issues)
        {
            var syncStatus = syncStatuses?.GetValueOrDefault(issue.Id);
            Console.WriteLine(IssueLineFormatter.FormatPlainText(issue, syncStatus));
        }
    }
}
