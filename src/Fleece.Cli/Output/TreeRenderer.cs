using Fleece.Core.Models;
using Spectre.Console;
using System.Text.Json;

namespace Fleece.Cli.Output;

/// <summary>
/// Renders issues in a tree view based on parent-child relationships.
/// </summary>
public static class TreeRenderer
{
    /// <summary>
    /// Returns a list containing the root issue and all its transitive descendants from the given issue list.
    /// The root issue is always included even if it wasn't in the original filtered list.
    /// </summary>
    public static List<Issue> ScopeToDescendants(Issue rootIssue, List<Issue> filteredIssues)
    {
        var lookup = filteredIssues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var descendantIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(rootIssue.Id);

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            foreach (var issue in filteredIssues)
            {
                if (descendantIds.Contains(issue.Id))
                {
                    continue;
                }
                if (issue.ParentIssues?.Any(p => p.ParentIssue.Equals(parentId, StringComparison.OrdinalIgnoreCase)) ?? false)
                {
                    descendantIds.Add(issue.Id);
                    queue.Enqueue(issue.Id);
                }
            }
        }

        // Always include the root issue itself
        var result = new List<Issue>();
        if (!lookup.ContainsKey(rootIssue.Id))
        {
            result.Add(rootIssue);
        }
        else
        {
            result.Add(lookup[rootIssue.Id]);
        }

        result.AddRange(filteredIssues.Where(i => descendantIds.Contains(i.Id)));
        return result;
    }

    public static void RenderTree(List<Issue> issues)
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
                if (i.ParentIssues is null || i.ParentIssues.Count == 0)
                {
                    return true;
                }
                // Check if ALL parents are NOT in the lookup (meaning this is an orphan)
                var allParentsMissing = i.ParentIssues.All(p => !issueLookup.ContainsKey(p.ParentIssue));
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
        AnsiConsole.MarkupLine($"{prefix}{connector2}{IssueLineFormatter.FormatMarkup(issue)}");

        // Find children (issues that have this issue as a parent), sorted by SortOrder
        var children = allIssues
            .Where(i => i.ParentIssues?.Any(p => p.ParentIssue.Equals(issue.Id, StringComparison.OrdinalIgnoreCase)) ?? false)
            .Select(i => new
            {
                Issue = i,
                SortOrder = i.ParentIssues?.FirstOrDefault(p => p.ParentIssue.Equals(issue.Id, StringComparison.OrdinalIgnoreCase))?.SortOrder ?? "zzz"
            })
            .OrderBy(x => x.SortOrder, StringComparer.Ordinal)
            .ThenBy(x => x.Issue.Priority ?? 99)
            .ThenBy(x => x.Issue.Title)
            .Select(x => x.Issue)
            .ToList();

        // Render children with proper indentation
        var childPrefix = isRoot ? "" : prefix + (isLast ? "    " : "\u2502   ");
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var isLastChild = i == children.Count - 1;

            // Check if this child has multiple parents within the filtered set
            var parentsInSet = (child.ParentIssues ?? [])
                .Where(p => issueLookup.ContainsKey(p.ParentIssue))
                .ToList();

            if (parentsInSet.Count > 1 && !rendered.Contains(child.Id))
            {
                // Show virtual parent indicator for other parents
                var otherParents = parentsInSet.Where(p => !p.ParentIssue.Equals(issue.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                if (otherParents.Count > 0)
                {
                    var virtualConnector = isLastChild ? "\u2514\u2500\u2500 " : "\u251c\u2500\u2500 ";
                    AnsiConsole.MarkupLine($"{childPrefix}{virtualConnector}[dim](also child of: {string.Join(", ", otherParents.Select(p => p.ParentIssue))})[/]");
                }
            }

            RenderIssueNode(child, childPrefix, isLastChild, false, issueLookup, rendered, allIssues);
        }
    }

    public static void RenderJsonTree(List<Issue> issues)
    {
        // Build a lookup for quick access
        var issueLookup = issues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var rendered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Find root issues
        var rootIssues = issues
            .Where(i => i.ParentIssues is null || i.ParentIssues.Count == 0 ||
                        i.ParentIssues.All(p => !issueLookup.ContainsKey(p.ParentIssue)))
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
            .Where(i => i.ParentIssues?.Any(p => p.ParentIssue.Equals(issue.Id, StringComparison.OrdinalIgnoreCase)) ?? false)
            .Select(i => new
            {
                Issue = i,
                SortOrder = i.ParentIssues?.FirstOrDefault(p => p.ParentIssue.Equals(issue.Id, StringComparison.OrdinalIgnoreCase))?.SortOrder ?? "zzz"
            })
            .OrderBy(x => x.SortOrder, StringComparer.Ordinal)
            .ThenBy(x => x.Issue.Priority ?? 99)
            .ThenBy(x => x.Issue.Title)
            .Select(x => x.Issue)
            .ToList();

        return new
        {
            id = issue.Id,
            title = issue.Title,
            type = issue.Type.ToString().ToLowerInvariant(),
            status = issue.Status.ToString().ToLowerInvariant(),
            priority = issue.Priority,
            executionMode = issue.ExecutionMode.ToString().ToLowerInvariant(),
            assignedTo = issue.AssignedTo,
            linkedPR = issue.LinkedPR,
            tags = issue.Tags,
            children = children.Select(c => BuildJsonNode(c, issueLookup, rendered, allIssues)).ToList()
        };
    }
}
