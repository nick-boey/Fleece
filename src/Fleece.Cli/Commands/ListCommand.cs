using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Models.Graph;
using Fleece.Core.Search;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class ListCommand(
    IFleeceService fleeceService,
    IIssueLayoutService issueLayoutService,
    ISettingsService settingsService,
    IAnsiConsole console) : AsyncCommand<ListSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings)
    {
        var (hasMultiple, message) = await fleeceService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            console.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        var isTree = settings.Tree || !string.IsNullOrWhiteSpace(settings.TreeRoot);

        if (isTree && settings.Next)
        {
            console.MarkupLine("[red]Error:[/] --tree and --next cannot be used together");
            return 1;
        }

        if ((isTree || settings.Next) && settings.OneLine)
        {
            console.MarkupLine("[red]Error:[/] --tree/--next cannot be used with --one-line");
            return 1;
        }

        if (settings.Next && settings.Json)
        {
            console.MarkupLine("[red]Error:[/] --next and --json cannot be used together");
            return 1;
        }

        if (settings.ParentsOnly && settings.ChildrenOnly)
        {
            console.MarkupLine("[red]Error:[/] --parents and --children cannot be used together");
            return 1;
        }

        if ((settings.ParentsOnly || settings.ChildrenOnly) && string.IsNullOrWhiteSpace(settings.IssueId))
        {
            console.MarkupLine("[red]Error:[/] --parents and --children require an issue ID to be specified");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(settings.IssueId) && !string.IsNullOrWhiteSpace(settings.TreeRoot))
        {
            console.MarkupLine("[yellow]Warning:[/] --tree-root is deprecated and ignored when an issue ID is specified. Use '<id> --children' instead.");
        }
        else if (!string.IsNullOrWhiteSpace(settings.TreeRoot))
        {
            console.MarkupLine("[yellow]Warning:[/] --tree-root is deprecated. Use '<id> --children' instead.");
        }

        IssueStatus? status = null;
        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            if (!Enum.TryParse<IssueStatus>(settings.Status, ignoreCase: true, out var parsedStatus))
            {
                console.MarkupLine($"[red]Error:[/] Invalid status '{settings.Status}'. Use: draft, open, progress, review, complete, archived, closed");
                return 1;
            }
            status = parsedStatus;
        }

        IssueType? type = null;
        if (!string.IsNullOrWhiteSpace(settings.Type))
        {
            if (!Enum.TryParse<IssueType>(settings.Type, ignoreCase: true, out var parsedType))
            {
                console.MarkupLine($"[red]Error:[/] Invalid type '{settings.Type}'. Use: task, bug, chore, feature, idea, verify");
                return 1;
            }
            type = parsedType;
        }

        if (settings.Next)
        {
            if (settings.Me && !string.IsNullOrWhiteSpace(settings.AssignedTo))
            {
                console.MarkupLine("[red]Error:[/] --me and --assigned cannot be used together");
                return 1;
            }

            string? assignedTo = settings.AssignedTo;
            if (settings.Me)
            {
                var effectiveSettings = await settingsService.GetEffectiveSettingsAsync();
                if (string.IsNullOrWhiteSpace(effectiveSettings.Identity))
                {
                    console.MarkupLine("[red]Error:[/] No identity configured. Run 'fleece config --set identity=<name>' or set git user.name");
                    return 1;
                }
                assignedTo = effectiveSettings.Identity;
            }

            GraphSortConfig? sortConfig = null;
            if (!string.IsNullOrWhiteSpace(settings.Sort))
            {
                try
                {
                    sortConfig = GraphSortConfig.Parse(settings.Sort);
                }
                catch (ArgumentException ex)
                {
                    console.MarkupLine($"[red]Error:[/] {ex.Message}");
                    return 1;
                }
            }

            HashSet<string>? nextHierarchyIds = null;
            if (!string.IsNullOrWhiteSpace(settings.IssueId))
            {
                var matches = await fleeceService.ResolveByPartialIdAsync(settings.IssueId);

                if (matches.Count == 0)
                {
                    console.MarkupLine($"[red]Error:[/] Issue '{settings.IssueId}' not found");
                    return 1;
                }

                if (matches.Count > 1)
                {
                    console.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.IssueId}':");
                    foreach (var match in matches)
                    {
                        console.MarkupLine($"  {match.Id} {Markup.Escape(match.Title)}");
                    }
                    return 1;
                }

                var targetIssue = matches[0];

                var hierarchyIssues = await fleeceService.GetIssueHierarchyAsync(
                    targetIssue.Id,
                    includeParents: !settings.ChildrenOnly,
                    includeChildren: !settings.ParentsOnly);

                nextHierarchyIds = new HashSet<string>(
                    hierarchyIssues.Select(i => i.Id),
                    StringComparer.OrdinalIgnoreCase);
            }

            var inactiveVisibility = InactiveVisibility.Hide;
            if (settings.All)
            {
                inactiveVisibility = InactiveVisibility.Always;
            }
            if (!string.IsNullOrWhiteSpace(settings.ShowInactive))
            {
                inactiveVisibility = settings.ShowInactive.ToLowerInvariant() switch
                {
                    "hide" => InactiveVisibility.Hide,
                    "if-active-children" => InactiveVisibility.IfHasActiveDescendants,
                    "always" => InactiveVisibility.Always,
                    _ => InactiveVisibility.Hide
                };

                if (settings.ShowInactive.ToLowerInvariant() is not ("hide" or "if-active-children" or "always"))
                {
                    console.MarkupLine($"[red]Error:[/] Invalid --show-inactive value '{settings.ShowInactive}'. Use: hide, if-active-children, always");
                    return 1;
                }
            }

            var loadResultForGraph = await fleeceService.LoadIssuesWithDiagnosticsAsync();
            var allIssues = loadResultForGraph.Issues;

            GraphLayout<Issue> graph;
            IReadOnlySet<string>? graphMatchedIds = null;
            if (!string.IsNullOrWhiteSpace(settings.Search))
            {
                var query = fleeceService.ParseSearchQuery(settings.Search);
                var searchResult = await fleeceService.SearchWithContextAsync(
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
                    console.MarkupLine("[dim]No issues found matching search[/]");
                    return 0;
                }

                var matchedIds = nextHierarchyIds is not null
                    ? new HashSet<string>(searchResult.MatchedIds.Where(id => nextHierarchyIds.Contains(id)), StringComparer.OrdinalIgnoreCase)
                    : searchResult.MatchedIds;

                if (matchedIds.Count == 0)
                {
                    console.MarkupLine("[dim]No issues found matching search within hierarchy[/]");
                    return 0;
                }

                graph = issueLayoutService.LayoutForNext(allIssues, matchedIds, sort: sortConfig);
                graphMatchedIds = matchedIds;
            }
            else if (nextHierarchyIds is not null)
            {
                graph = issueLayoutService.LayoutForNext(allIssues, nextHierarchyIds, sort: sortConfig);
                graphMatchedIds = nextHierarchyIds;
            }
            else
            {
                graph = issueLayoutService.LayoutForTree(
                    allIssues,
                    visibility: inactiveVisibility,
                    assignedTo: assignedTo,
                    sort: sortConfig);
            }

            var actionableIds = ComputeActionableIds(allIssues);
            TaskGraphRenderer.Render(console, graph, actionableIds, graphMatchedIds);
            return 0;
        }

        var loadResult = await fleeceService.LoadIssuesWithDiagnosticsAsync();
        var hasWarnings = DiagnosticFormatter.RenderDiagnostics(console, loadResult.Diagnostics);

        if (settings.Strict && hasWarnings)
        {
            console.MarkupLine("[red]Error:[/] Schema warnings detected in strict mode.");
            return 1;
        }

        if (settings.OneLine && (settings.Json || settings.JsonVerbose))
        {
            console.MarkupLine("[red]Error:[/] --one-line cannot be used with --json or --json-verbose");
            return 1;
        }

        HashSet<string>? hierarchyIds = null;
        if (!string.IsNullOrWhiteSpace(settings.IssueId))
        {
            var matches = await fleeceService.ResolveByPartialIdAsync(settings.IssueId);

            if (matches.Count == 0)
            {
                console.MarkupLine($"[red]Error:[/] Issue '{settings.IssueId}' not found");
                return 1;
            }

            if (matches.Count > 1)
            {
                console.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.IssueId}':");
                foreach (var match in matches)
                {
                    console.MarkupLine($"  {match.Id} {Markup.Escape(match.Title)}");
                }
                return 1;
            }

            var targetIssue = matches[0];

            // Get hierarchy-filtered issues
            var hierarchyIssues = await fleeceService.GetIssueHierarchyAsync(
                targetIssue.Id,
                includeParents: !settings.ChildrenOnly,
                includeChildren: !settings.ParentsOnly);

            hierarchyIds = new HashSet<string>(
                hierarchyIssues.Select(i => i.Id),
                StringComparer.OrdinalIgnoreCase);
        }

        // Apply filtering via the fleece service or search
        IReadOnlyList<Issue> issues;
        if (!string.IsNullOrWhiteSpace(settings.Search))
        {
            // Use search when --search is specified
            var query = fleeceService.ParseSearchQuery(settings.Search);
            issues = await fleeceService.SearchWithFiltersAsync(
                query,
                status,
                type,
                settings.Priority,
                settings.AssignedTo,
                settings.Tags,
                settings.LinkedPr,
                settings.All);
        }
        else
        {
            // Use standard filtering
            issues = await fleeceService.FilterAsync(
                status,
                type,
                settings.Priority,
                settings.AssignedTo,
                settings.Tags,
                settings.LinkedPr,
                settings.All);
        }

        // Apply hierarchy filtering if an issue ID was specified
        if (hierarchyIds is not null)
        {
            issues = issues.Where(i => hierarchyIds.Contains(i.Id)).ToList();
        }

        if (isTree)
        {
            return await ExecuteTreeMode(issues.ToList(), settings);
        }

        IReadOnlyDictionary<string, SyncStatus>? syncStatuses = null;
        if (settings.SyncStatus)
        {
            syncStatuses = await fleeceService.GetSyncStatusesAsync();
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
            TableFormatter.RenderIssues(console, issues, syncStatuses);
        }

        return 0;
    }

    private async Task<int> ExecuteTreeMode(
        List<Issue> issueList,
        ListSettings settings)
    {
        Issue? rootIssue = null;
        if (string.IsNullOrWhiteSpace(settings.IssueId) && !string.IsNullOrWhiteSpace(settings.TreeRoot))
        {
            var matches = await fleeceService.ResolveByPartialIdAsync(settings.TreeRoot);

            if (matches.Count == 0)
            {
                console.MarkupLine($"[red]Error:[/] Issue '{settings.TreeRoot}' not found");
                return 1;
            }

            if (matches.Count > 1)
            {
                console.MarkupLine($"[red]Error:[/] Multiple issues match '{settings.TreeRoot}':");
                foreach (var match in matches)
                {
                    console.MarkupLine($"  {match.Id} {Markup.Escape(match.Title)}");
                }
                return 1;
            }

            rootIssue = matches[0];
        }

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
            TreeRenderer.RenderTree(console, issueList);
        }

        return 0;
    }

    private static IReadOnlySet<string> ComputeActionableIds(IReadOnlyList<Issue> issues)
    {
        var graph = Issues.BuildGraph(issues);
        return new HashSet<string>(
            graph.Nodes.Values.Where(Issues.IsActionable).Select(n => n.Issue.Id),
            StringComparer.OrdinalIgnoreCase);
    }

    private void RenderOneLine(IReadOnlyList<Issue> issues, IReadOnlyDictionary<string, SyncStatus>? syncStatuses)
    {
        if (issues.Count == 0)
        {
            console.MarkupLine("[dim]No issues found[/]");
            return;
        }

        foreach (var issue in issues)
        {
            var syncStatus = syncStatuses?.GetValueOrDefault(issue.Id);
            Console.WriteLine(IssueLineFormatter.FormatPlainText(issue, syncStatus));
        }
    }
}
