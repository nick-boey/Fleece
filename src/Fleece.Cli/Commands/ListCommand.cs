using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Search;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class ListCommand(
    IIssueServiceFactory issueServiceFactory,
    IStorageServiceProvider storageServiceProvider,
    ISyncStatusService syncStatusService,
    ISearchService searchService,
    ISettingsService settingsService) : AsyncCommand<ListSettings>
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

        // Validate --parents and --children flags
        if (settings.ParentsOnly && settings.ChildrenOnly)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --parents and --children cannot be used together");
            return 1;
        }

        if ((settings.ParentsOnly || settings.ChildrenOnly) && string.IsNullOrWhiteSpace(settings.IssueId))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --parents and --children require an issue ID to be specified");
            return 1;
        }

        // Warn about deprecated --tree-root when <id> is also specified
        if (!string.IsNullOrWhiteSpace(settings.IssueId) && !string.IsNullOrWhiteSpace(settings.TreeRoot))
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] --tree-root is deprecated and ignored when an issue ID is specified. Use '<id> --children' instead.");
        }
        else if (!string.IsNullOrWhiteSpace(settings.TreeRoot))
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] --tree-root is deprecated. Use '<id> --children' instead.");
        }

        // Parse status and type early as they're needed for both --next and list modes
        IssueStatus? status = null;
        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            if (!Enum.TryParse<IssueStatus>(settings.Status, ignoreCase: true, out var parsedStatus))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{settings.Status}'. Use: draft, open, progress, review, complete, archived, closed");
                return 1;
            }
            status = parsedStatus;
        }

        IssueType? type = null;
        if (!string.IsNullOrWhiteSpace(settings.Type))
        {
            if (!Enum.TryParse<IssueType>(settings.Type, ignoreCase: true, out var parsedType))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid type '{settings.Type}'. Use: task, bug, chore, feature, idea, verify");
                return 1;
            }
            type = parsedType;
        }

        // --- Next mode ---
        if (settings.Next)
        {
            // Validate --me and --assigned are mutually exclusive
            if (settings.Me && !string.IsNullOrWhiteSpace(settings.AssignedTo))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] --me and --assigned cannot be used together");
                return 1;
            }

            // Resolve assignee for --me filter
            string? assignedTo = settings.AssignedTo;
            if (settings.Me)
            {
                var effectiveSettings = await settingsService.GetEffectiveSettingsAsync();
                if (string.IsNullOrWhiteSpace(effectiveSettings.Identity))
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] No identity configured. Run 'fleece config --set identity=<name>' or set git user.name");
                    return 1;
                }
                assignedTo = effectiveSettings.Identity;
            }

            // Resolve issue ID for hierarchy filtering in --next mode
            HashSet<string>? nextHierarchyIds = null;
            if (!string.IsNullOrWhiteSpace(settings.IssueId))
            {
                var matches = await issueService.ResolveByPartialIdAsync(settings.IssueId);

                if (matches.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{settings.IssueId}' not found");
                    return 1;
                }

                if (matches.Count > 1)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.IssueId}':");
                    foreach (var match in matches)
                    {
                        AnsiConsole.MarkupLine($"  {match.Id} {Markup.Escape(match.Title)}");
                    }
                    return 1;
                }

                var targetIssue = matches[0];

                // Get hierarchy-filtered issues
                var hierarchyIssues = await issueService.GetIssueHierarchyAsync(
                    targetIssue.Id,
                    includeParents: !settings.ChildrenOnly,
                    includeChildren: !settings.ParentsOnly);

                nextHierarchyIds = new HashSet<string>(
                    hierarchyIssues.Select(i => i.Id),
                    StringComparer.OrdinalIgnoreCase);
            }

            TaskGraph graph;
            if (!string.IsNullOrWhiteSpace(settings.Search))
            {
                // Build filtered graph with search
                var query = searchService.ParseQuery(settings.Search);
                var searchResult = await searchService.SearchWithContextAsync(
                    query,
                    status,
                    type,
                    settings.Priority,
                    assignedTo,
                    settings.Tags,
                    settings.LinkedPr,
                    settings.All);

                if (searchResult.MatchedIssues.Count == 0)
                {
                    AnsiConsole.MarkupLine("[dim]No issues found matching search[/]");
                    return 0;
                }

                // Combine with hierarchy filter if specified
                var matchedIds = nextHierarchyIds is not null
                    ? new HashSet<string>(searchResult.MatchedIds.Where(id => nextHierarchyIds.Contains(id)), StringComparer.OrdinalIgnoreCase)
                    : searchResult.MatchedIds;

                if (matchedIds.Count == 0)
                {
                    AnsiConsole.MarkupLine("[dim]No issues found matching search within hierarchy[/]");
                    return 0;
                }

                graph = await issueService.BuildFilteredTaskGraphLayoutAsync(matchedIds);
            }
            else if (nextHierarchyIds is not null)
            {
                // Use hierarchy filter for --next mode
                graph = await issueService.BuildFilteredTaskGraphLayoutAsync(nextHierarchyIds);
            }
            else
            {
                graph = await issueService.BuildTaskGraphLayoutAsync(
                    includeTerminal: settings.All,
                    assignedTo: assignedTo);
            }

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

        // Validate mutually exclusive output options
        if (settings.OneLine && (settings.Json || settings.JsonVerbose))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --one-line cannot be used with --json or --json-verbose");
            return 1;
        }

        // Parse keyed tags
        List<(string Key, string Value)>? keyedTags = null;
        if (settings.KeyedTags is { Length: > 0 })
        {
            keyedTags = [];
            foreach (var keyedTag in settings.KeyedTags)
            {
                var equalsIndex = keyedTag.IndexOf('=');
                if (equalsIndex <= 0 || equalsIndex >= keyedTag.Length - 1)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Invalid keyed tag format '{keyedTag}'. Expected format: key=value");
                    return 1;
                }
                keyedTags.Add((keyedTag[..equalsIndex], keyedTag[(equalsIndex + 1)..]));
            }
        }

        // Resolve optional issue ID for hierarchy filtering
        HashSet<string>? hierarchyIds = null;
        if (!string.IsNullOrWhiteSpace(settings.IssueId))
        {
            var matches = await issueService.ResolveByPartialIdAsync(settings.IssueId);

            if (matches.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{settings.IssueId}' not found");
                return 1;
            }

            if (matches.Count > 1)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.IssueId}':");
                foreach (var match in matches)
                {
                    AnsiConsole.MarkupLine($"  {match.Id} {Markup.Escape(match.Title)}");
                }
                return 1;
            }

            var targetIssue = matches[0];

            // Get hierarchy-filtered issues
            var hierarchyIssues = await issueService.GetIssueHierarchyAsync(
                targetIssue.Id,
                includeParents: !settings.ChildrenOnly,
                includeChildren: !settings.ParentsOnly);

            hierarchyIds = new HashSet<string>(
                hierarchyIssues.Select(i => i.Id),
                StringComparer.OrdinalIgnoreCase);
        }

        // Apply filtering via the issue service or search service
        IReadOnlyList<Issue> issues;
        if (!string.IsNullOrWhiteSpace(settings.Search))
        {
            // Use search service when --search is specified
            var query = searchService.ParseQuery(settings.Search);
            issues = await searchService.SearchWithFiltersAsync(
                query,
                status,
                type,
                settings.Priority,
                settings.AssignedTo,
                settings.Tags,
                settings.LinkedPr,
                settings.All,
                keyedTags);
        }
        else
        {
            // Use standard filtering
            issues = await issueService.FilterAsync(
                status,
                type,
                settings.Priority,
                settings.AssignedTo,
                settings.Tags,
                settings.LinkedPr,
                settings.All,
                keyedTags);
        }

        // Apply hierarchy filtering if an issue ID was specified
        if (hierarchyIds is not null)
        {
            issues = issues.Where(i => hierarchyIds.Contains(i.Id)).ToList();
        }

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
        // Resolve optional root issue ID (deprecated --tree-root)
        // Only apply if no IssueId is specified (hierarchy filtering handles that case)
        Issue? rootIssue = null;
        if (string.IsNullOrWhiteSpace(settings.IssueId) && !string.IsNullOrWhiteSpace(settings.TreeRoot))
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

        // When a tree-root is specified (deprecated), constrain to the root + its transitive descendants
        // Note: When IssueId is specified, hierarchy filtering is already applied before this method
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
