using Fleece.Cli.Output;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Text.Json;

namespace Fleece.Cli.Commands;

public sealed class TreeCommand(IIssueService issueService, IStorageService storageService) : AsyncCommand<TreeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, TreeSettings settings)
    {
        var (hasMultiple, message) = await storageService.HasMultipleUnmergedFilesAsync();
        if (hasMultiple)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
            return 1;
        }

        IssueStatus? status = null;
        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            if (!Enum.TryParse<IssueStatus>(settings.Status, ignoreCase: true, out var parsedStatus))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid status '{settings.Status}'. Use: idea, spec, next, progress, review, complete, archived, closed");
                return 1;
            }
            status = parsedStatus;
        }

        IssueType? type = null;
        if (!string.IsNullOrWhiteSpace(settings.Type))
        {
            if (!Enum.TryParse<IssueType>(settings.Type, ignoreCase: true, out var parsedType))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid type '{settings.Type}'. Use: task, bug, chore, feature");
                return 1;
            }
            type = parsedType;
        }

        var issues = await issueService.FilterAsync(status, type, settings.Priority, settings.Group, settings.AssignedTo, settings.Tags, settings.LinkedPr, settings.All);
        var issueList = issues.ToList();

        if (settings.Json)
        {
            RenderJsonTree(issueList);
        }
        else
        {
            RenderTree(issueList);
        }

        return 0;
    }

    private static void RenderTree(List<Issue> issues)
    {
        if (issues.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No issues found[/]");
            return;
        }

        // Build a lookup for quick access
        var issueLookup = issues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Track which issues have been rendered
        var rendered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Find root issues (those with no parents, or whose parents are not in the filtered list)
        var rootIssues = issues
            .Where(i =>
            {
                if (i.ParentIssues.Count == 0)
                {
                    return true;
                }
                // Check if ALL parents are NOT in the lookup (meaning this is an orphan)
                var allParentsMissing = i.ParentIssues.All(p => !issueLookup.ContainsKey(p));
                return allParentsMissing;
            })
            .OrderBy(i => i.Priority ?? 99)
            .ThenBy(i => i.Title)
            .ToList();

        // Render each root issue and its children using depth-first traversal
        foreach (var root in rootIssues)
        {
            RenderIssueNode(root, "", true, true, issueLookup, rendered, issues);
        }

        // Render any orphaned issues that weren't reached (shouldn't happen normally)
        var orphans = issues.Where(i => !rendered.Contains(i.Id)).ToList();
        if (orphans.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Unlinked issues:[/]");
            foreach (var orphan in orphans)
            {
                RenderIssueNode(orphan, "", true, true, issueLookup, rendered, issues);
            }
        }
    }

    private static void RenderIssueNode(
        Issue issue,
        string prefix,
        bool isLast,
        bool isRoot,
        Dictionary<string, Issue> issueLookup,
        HashSet<string> rendered,
        List<Issue> allIssues)
    {
        // Check if this issue has already been rendered
        var alreadyRendered = rendered.Contains(issue.Id);

        if (alreadyRendered)
        {
            // Show a virtual parent indicator pointing to where it was already rendered
            var connector = isLast ? "\u2514\u2500\u2500 " : "\u251c\u2500\u2500 ";
            AnsiConsole.MarkupLine($"{prefix}{connector}[dim](see {issue.Id} above)[/]");
            return;
        }

        // Mark as rendered
        rendered.Add(issue.Id);

        // Render the current issue
        var connector2 = isRoot ? "" : (isLast ? "\u2514\u2500\u2500 " : "\u251c\u2500\u2500 ");
        var statusColor = issue.Status switch
        {
            IssueStatus.Idea => "magenta",
            IssueStatus.Spec => "cyan",
            IssueStatus.Next => "yellow",
            IssueStatus.Progress => "blue",
            IssueStatus.Review => "purple",
            IssueStatus.Complete => "green",
            IssueStatus.Archived => "dim",
            IssueStatus.Closed => "dim",
            _ => "white"
        };
        var typeIcon = issue.Type switch
        {
            IssueType.Bug => "\ud83d\udc1b",
            IssueType.Feature => "\u2728",
            IssueType.Task => "\u2611\ufe0f",
            IssueType.Chore => "\ud83e\uddf9",
            _ => "\u2022"
        };

        var priorityStr = issue.Priority.HasValue ? $"[dim]P{issue.Priority}[/] " : "";
        AnsiConsole.MarkupLine($"{prefix}{connector2}[{statusColor}]{typeIcon}[/] [{statusColor}]{issue.Id}[/] {priorityStr}{Markup.Escape(issue.Title)}");

        // Find children (issues that have this issue as a parent)
        var children = allIssues
            .Where(i => i.ParentIssues.Contains(issue.Id, StringComparer.OrdinalIgnoreCase))
            .OrderBy(i => i.Priority ?? 99)
            .ThenBy(i => i.Title)
            .ToList();

        // Render children with proper indentation
        var childPrefix = isRoot ? "" : prefix + (isLast ? "    " : "\u2502   ");
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var isLastChild = i == children.Count - 1;

            // Check if this child has multiple parents within the filtered set
            var parentsInSet = child.ParentIssues
                .Where(p => issueLookup.ContainsKey(p))
                .ToList();

            if (parentsInSet.Count > 1 && !rendered.Contains(child.Id))
            {
                // Show virtual parent indicator for other parents
                var otherParents = parentsInSet.Where(p => !p.Equals(issue.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                if (otherParents.Count > 0)
                {
                    var virtualConnector = isLastChild ? "\u2514\u2500\u2500 " : "\u251c\u2500\u2500 ";
                    AnsiConsole.MarkupLine($"{childPrefix}{virtualConnector}[dim](also child of: {string.Join(", ", otherParents)})[/]");
                }
            }

            RenderIssueNode(child, childPrefix, isLastChild, false, issueLookup, rendered, allIssues);
        }
    }

    private static void RenderJsonTree(List<Issue> issues)
    {
        // Build a lookup for quick access
        var issueLookup = issues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var rendered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Find root issues
        var rootIssues = issues
            .Where(i => i.ParentIssues.Count == 0 ||
                        i.ParentIssues.All(p => !issueLookup.ContainsKey(p)))
            .OrderBy(i => i.Priority ?? 99)
            .ThenBy(i => i.Title)
            .ToList();

        var tree = rootIssues.Select(r => BuildJsonNode(r, issueLookup, rendered, issues)).ToList();

        var options = new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine(JsonSerializer.Serialize(tree, options));
    }

    private static object BuildJsonNode(
        Issue issue,
        Dictionary<string, Issue> issueLookup,
        HashSet<string> rendered,
        List<Issue> allIssues)
    {
        if (rendered.Contains(issue.Id))
        {
            return new { id = issue.Id, reference = true };
        }

        rendered.Add(issue.Id);

        var children = allIssues
            .Where(i => i.ParentIssues.Contains(issue.Id, StringComparer.OrdinalIgnoreCase))
            .OrderBy(i => i.Priority ?? 99)
            .ThenBy(i => i.Title)
            .ToList();

        return new
        {
            id = issue.Id,
            title = issue.Title,
            type = issue.Type.ToString().ToLowerInvariant(),
            status = issue.Status.ToString().ToLowerInvariant(),
            priority = issue.Priority,
            children = children.Select(c => BuildJsonNode(c, issueLookup, rendered, allIssues)).ToList()
        };
    }
}
