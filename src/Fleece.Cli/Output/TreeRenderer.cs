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
                if (issue.ActiveParentIssues?.Any(p => p.ParentIssue.Equals(parentId, StringComparison.OrdinalIgnoreCase)) ?? false)
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

    /// <summary>
    /// Pre-computes how many times each issue will appear in the tree
    /// (once per parent in the display set, minimum 1 for roots).
    /// </summary>
    private static Dictionary<string, int> ComputeTotalAppearances(
        List<Issue> issues,
        Dictionary<string, Issue> issueLookup)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in issues)
        {
            var parentsInSet = (issue.ActiveParentIssues ?? [])
                .Count(p => issueLookup.ContainsKey(p.ParentIssue));
            counts[issue.Id] = Math.Max(1, parentsInSet);
        }
        return counts;
    }

    public static void RenderTree(IAnsiConsole console, List<Issue> issues)
    {
        if (issues.Count == 0)
        {
            console.MarkupLine("[dim]No issues found[/]");
            return;
        }

        // Build a lookup for quick access
        var issueLookup = issues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Track how many times each issue has been rendered (for AppearanceIndex)
        var renderCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Pre-compute total appearances per issue
        var totalAppearances = ComputeTotalAppearances(issues, issueLookup);

        // Find root issues (those with no parents, or whose parents are not in the filtered list)
        var rootIssues = issues
            .Where(i =>
            {
                if (i.ActiveParentIssues is null || i.ActiveParentIssues.Count == 0)
                {
                    return true;
                }
                // Check if ALL parents are NOT in the lookup (meaning this is an orphan)
                var allParentsMissing = i.ActiveParentIssues.All(p => !issueLookup.ContainsKey(p.ParentIssue));
                return allParentsMissing;
            })
            .OrderBy(i => i.Priority ?? 99)
            .ThenBy(i => i.Title)
            .ToList();

        // Render each root issue and its children using depth-first traversal
        foreach (var root in rootIssues)
        {
            RenderIssueNode(console, root, "", true, true, issueLookup, renderCount, totalAppearances, issues);
        }

        // Render any orphaned issues that weren't reached (shouldn't happen normally)
        var orphans = issues.Where(i => !renderCount.ContainsKey(i.Id)).ToList();
        if (orphans.Count > 0)
        {
            console.WriteLine();
            console.MarkupLine("[dim]Unlinked issues:[/]");
            foreach (var orphan in orphans)
            {
                RenderIssueNode(console, orphan, "", true, true, issueLookup, renderCount, totalAppearances, issues);
            }
        }
    }

    private static void RenderIssueNode(
        IAnsiConsole console,
        Issue issue,
        string prefix,
        bool isLast,
        bool isRoot,
        Dictionary<string, Issue> issueLookup,
        Dictionary<string, int> renderCount,
        Dictionary<string, int> totalAppearances,
        List<Issue> allIssues)
    {
        // Track appearance index
        renderCount.TryGetValue(issue.Id, out var currentCount);
        currentCount++;
        renderCount[issue.Id] = currentCount;

        var total = totalAppearances.GetValueOrDefault(issue.Id, 1);
        var appearanceSuffix = total > 1 ? $" [dim]({currentCount}/{total})[/]" : "";

        // Render the current issue
        var connector = isRoot ? "" : (isLast ? "\u2514\u2500\u2500 " : "\u251c\u2500\u2500 ");
        console.MarkupLine($"{prefix}{connector}{IssueLineFormatter.FormatMarkup(issue)}{appearanceSuffix}");

        // Only show children on the first encounter
        if (currentCount > 1)
        {
            return;
        }

        // Find children (issues that have this issue as a parent), sorted by SortOrder
        var children = allIssues
            .Where(i => i.ActiveParentIssues?.Any(p => p.ParentIssue.Equals(issue.Id, StringComparison.OrdinalIgnoreCase)) ?? false)
            .Select(i => new
            {
                Issue = i,
                SortOrder = i.ActiveParentIssues?.FirstOrDefault(p => p.ParentIssue.Equals(issue.Id, StringComparison.OrdinalIgnoreCase))?.SortOrder ?? "zzz"
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
            RenderIssueNode(console, child, childPrefix, isLastChild, false, issueLookup, renderCount, totalAppearances, allIssues);
        }
    }

    public static void RenderJsonTree(List<Issue> issues)
    {
        // Build a lookup for quick access
        var issueLookup = issues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var renderCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var totalAppearances = ComputeTotalAppearances(issues, issueLookup);

        // Find root issues
        var rootIssues = issues
            .Where(i => i.ActiveParentIssues is null || i.ActiveParentIssues.Count == 0 ||
                        i.ActiveParentIssues.All(p => !issueLookup.ContainsKey(p.ParentIssue)))
            .OrderBy(i => i.Priority ?? 99)
            .ThenBy(i => i.Title)
            .ToList();

        var tree = rootIssues.Select(r => BuildJsonNode(r, issueLookup, renderCount, totalAppearances, issues)).ToList();

        var options = new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine(JsonSerializer.Serialize(tree, options));
    }

    private static object BuildJsonNode(
        Issue issue,
        Dictionary<string, Issue> issueLookup,
        Dictionary<string, int> renderCount,
        Dictionary<string, int> totalAppearances,
        List<Issue> allIssues)
    {
        // Track appearance index
        renderCount.TryGetValue(issue.Id, out var currentCount);
        currentCount++;
        renderCount[issue.Id] = currentCount;

        var total = totalAppearances.GetValueOrDefault(issue.Id, 1);

        // On 2nd+ encounter, render as a leaf (no children)
        if (currentCount > 1)
        {
            return new
            {
                id = issue.Id,
                title = issue.Title,
                type = issue.Type.ToString().ToLowerInvariant(),
                status = issue.Status.ToString().ToLowerInvariant(),
                priority = issue.Priority,
                executionMode = issue.ExecutionMode.ToString().ToLowerInvariant(),
                assignedTo = issue.AssignedTo,
                linkedPRs = issue.LinkedPRs,
                tags = issue.Tags,
                appearanceIndex = currentCount,
                totalAppearances = total,
                children = new List<object>()
            };
        }

        var children = allIssues
            .Where(i => i.ActiveParentIssues?.Any(p => p.ParentIssue.Equals(issue.Id, StringComparison.OrdinalIgnoreCase)) ?? false)
            .Select(i => new
            {
                Issue = i,
                SortOrder = i.ActiveParentIssues?.FirstOrDefault(p => p.ParentIssue.Equals(issue.Id, StringComparison.OrdinalIgnoreCase))?.SortOrder ?? "zzz"
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
            linkedPRs = issue.LinkedPRs,
            tags = issue.Tags,
            appearanceIndex = currentCount,
            totalAppearances = total,
            children = children.Select(c => BuildJsonNode(c, issueLookup, renderCount, totalAppearances, allIssues)).ToList()
        };
    }
}
