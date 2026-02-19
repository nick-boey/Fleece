using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Service for building a task graph layout from issues.
/// The graph is organized bottom-up, with actionable tasks at the left (lane 0)
/// and root/parent tasks at the right (higher lanes).
/// </summary>
public sealed class TaskGraphService(IIssueService issueService, INextService nextService) : ITaskGraphService
{
    /// <inheritdoc />
    public async Task<TaskGraph> BuildGraphAsync(CancellationToken cancellationToken = default)
    {
        var allIssues = await issueService.GetAllAsync(cancellationToken);
        var issueList = allIssues.ToList();

        if (issueList.Count == 0)
        {
            return new TaskGraph { Nodes = [], TotalLanes = 0 };
        }

        // Filter to non-terminal issues only
        var activeIssues = issueList.Where(i => !i.Status.IsTerminal()).ToList();

        if (activeIssues.Count == 0)
        {
            return new TaskGraph { Nodes = [], TotalLanes = 0 };
        }

        // Build lookup for active issues
        var issueLookup = activeIssues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Determine which issues are actionable
        var actionableIssues = await nextService.GetNextIssuesAsync(cancellationToken: cancellationToken);
        var actionableIds = new HashSet<string>(
            actionableIssues.Select(i => i.Id),
            StringComparer.OrdinalIgnoreCase);

        // Build children lookup: parentId -> sorted list of incomplete children
        var childrenOf = BuildChildrenLookup(activeIssues, issueLookup);

        // Find root issues (no parent in the active set)
        var rootIssues = activeIssues
            .Where(i => i.ParentIssues.Count == 0 ||
                        i.ParentIssues.All(p => !issueLookup.ContainsKey(p.ParentIssue)))
            .OrderBy(i => i.Priority ?? 99)
            .ThenBy(i => i.Title)
            .ToList();

        // Layout each root subtree
        var nodeList = new List<TaskGraphNode>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int maxLane = 0;

        foreach (var root in rootIssues)
        {
            var rootMax = LayoutSubtree(root, 0, nodeList, childrenOf, issueLookup, actionableIds, visited, parentExecutionMode: null);
            maxLane = Math.Max(maxLane, rootMax);
        }

        return new TaskGraph
        {
            Nodes = nodeList,
            TotalLanes = maxLane + 1
        };
    }

    /// <summary>
    /// Recursively lays out a subtree, emitting nodes in depth-first order.
    /// Returns the maximum lane used by the subtree.
    /// </summary>
    private static int LayoutSubtree(
        Issue issue,
        int startLane,
        List<TaskGraphNode> nodeList,
        Dictionary<string, List<Issue>> childrenOf,
        Dictionary<string, Issue> issueLookup,
        HashSet<string> actionableIds,
        HashSet<string> visited,
        ExecutionMode? parentExecutionMode)
    {
        // Skip issues already placed by a previous parent traversal (DAG support)
        if (!visited.Add(issue.Id))
        {
            return startLane;
        }

        // Get incomplete children of this issue
        var incompleteChildren = GetIncompleteChildren(issue, childrenOf);

        // Leaf issue (no incomplete children)
        if (incompleteChildren.Count == 0)
        {
            nodeList.Add(new TaskGraphNode
            {
                Issue = issue,
                Row = nodeList.Count,
                Lane = startLane,
                IsActionable = actionableIds.Contains(issue.Id),
                ParentExecutionMode = parentExecutionMode
            });
            return startLane;
        }

        int maxLane;

        if (issue.ExecutionMode == ExecutionMode.Parallel)
        {
            maxLane = LayoutParallelChildren(issue, startLane, incompleteChildren, nodeList, childrenOf, issueLookup, actionableIds, visited);
        }
        else // Series (default)
        {
            maxLane = LayoutSeriesChildren(issue, startLane, incompleteChildren, nodeList, childrenOf, issueLookup, actionableIds, visited);
        }

        // Place the parent issue itself at maxLane + 1
        int parentLane = maxLane + 1;
        nodeList.Add(new TaskGraphNode
        {
            Issue = issue,
            Row = nodeList.Count,
            Lane = parentLane,
            IsActionable = actionableIds.Contains(issue.Id),
            ParentExecutionMode = parentExecutionMode
        });

        return parentLane;
    }

    /// <summary>
    /// Lays out children of a parallel parent. All children start at the same lane.
    /// </summary>
    private static int LayoutParallelChildren(
        Issue parent,
        int startLane,
        List<Issue> children,
        List<TaskGraphNode> nodeList,
        Dictionary<string, List<Issue>> childrenOf,
        Dictionary<string, Issue> issueLookup,
        HashSet<string> actionableIds,
        HashSet<string> visited)
    {
        int maxChildLane = startLane;

        foreach (var child in children)
        {
            // Skip children already visited via another parent (DAG support)
            if (visited.Contains(child.Id))
            {
                continue;
            }

            var childIncomplete = GetIncompleteChildren(child, childrenOf);

            if (childIncomplete.Count == 0)
            {
                // Leaf child — mark visited and add node
                visited.Add(child.Id);
                nodeList.Add(new TaskGraphNode
                {
                    Issue = child,
                    Row = nodeList.Count,
                    Lane = startLane,
                    IsActionable = actionableIds.Contains(child.Id),
                    ParentExecutionMode = ExecutionMode.Parallel
                });
            }
            else
            {
                // Subtree child — LayoutSubtree handles visited tracking
                var childMax = LayoutSubtree(child, startLane, nodeList, childrenOf, issueLookup, actionableIds, visited, parentExecutionMode: ExecutionMode.Parallel);
                maxChildLane = Math.Max(maxChildLane, childMax);
            }
        }

        return maxChildLane;
    }

    /// <summary>
    /// Lays out children of a series parent. Children share the same lane,
    /// with subtrees pushing the current lane rightward.
    /// </summary>
    private static int LayoutSeriesChildren(
        Issue parent,
        int startLane,
        List<Issue> children,
        List<TaskGraphNode> nodeList,
        Dictionary<string, List<Issue>> childrenOf,
        Dictionary<string, Issue> issueLookup,
        HashSet<string> actionableIds,
        HashSet<string> visited)
    {
        int currentLane = startLane;
        bool isFirstChild = true;

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];

            // Skip children already visited via another parent (DAG support)
            if (visited.Contains(child.Id))
            {
                continue;
            }

            var childIncomplete = GetIncompleteChildren(child, childrenOf);

            if (childIncomplete.Count == 0)
            {
                // Leaf child: place at currentLane, mark visited
                visited.Add(child.Id);
                nodeList.Add(new TaskGraphNode
                {
                    Issue = child,
                    Row = nodeList.Count,
                    Lane = currentLane,
                    IsActionable = actionableIds.Contains(child.Id),
                    ParentExecutionMode = ExecutionMode.Series
                });
            }
            else
            {
                // Subtree child
                // First non-skipped child starts at currentLane; subsequent start at currentLane + 1
                int subtreeStart = isFirstChild ? currentLane : currentLane + 1;
                var childMax = LayoutSubtree(child, subtreeStart, nodeList, childrenOf, issueLookup, actionableIds, visited, parentExecutionMode: ExecutionMode.Series);
                currentLane = childMax;
            }

            isFirstChild = false;
        }

        return currentLane;
    }

    /// <summary>
    /// Gets the incomplete (non-done) children of an issue, sorted by sort order.
    /// </summary>
    private static List<Issue> GetIncompleteChildren(Issue issue, Dictionary<string, List<Issue>> childrenOf)
    {
        if (!childrenOf.TryGetValue(issue.Id, out var children))
        {
            return [];
        }

        return children.Where(c => !c.Status.IsDone()).ToList();
    }

    /// <summary>
    /// Builds a lookup from parent ID to sorted list of children.
    /// </summary>
    private static Dictionary<string, List<Issue>> BuildChildrenLookup(
        List<Issue> activeIssues,
        Dictionary<string, Issue> issueLookup)
    {
        var childrenOf = new Dictionary<string, List<Issue>>(StringComparer.OrdinalIgnoreCase);

        foreach (var issue in activeIssues)
        {
            foreach (var parentRef in issue.ParentIssues)
            {
                if (!issueLookup.ContainsKey(parentRef.ParentIssue))
                {
                    continue; // Parent not in active set
                }

                if (!childrenOf.TryGetValue(parentRef.ParentIssue, out var children))
                {
                    children = [];
                    childrenOf[parentRef.ParentIssue] = children;
                }

                children.Add(issue);
            }
        }

        // Sort each children list by SortOrder, then by status, description, priority, and title
        foreach (var kvp in childrenOf)
        {
            var parentId = kvp.Key;
            kvp.Value.Sort((a, b) =>
            {
                var sortA = a.ParentIssues
                    .FirstOrDefault(p => string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase))
                    ?.SortOrder ?? "zzz";
                var sortB = b.ParentIssues
                    .FirstOrDefault(p => string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase))
                    ?.SortOrder ?? "zzz";
                var result = string.Compare(sortA, sortB, StringComparison.Ordinal);
                if (result != 0)
                {
                    return result;
                }

                // Review status before Open status
                var statusA = a.Status == IssueStatus.Review ? 0 : 1;
                var statusB = b.Status == IssueStatus.Review ? 0 : 1;
                result = statusA.CompareTo(statusB);
                if (result != 0)
                {
                    return result;
                }

                // Issues with descriptions before those without
                var hasDescA = string.IsNullOrWhiteSpace(a.Description) ? 1 : 0;
                var hasDescB = string.IsNullOrWhiteSpace(b.Description) ? 1 : 0;
                result = hasDescA.CompareTo(hasDescB);
                if (result != 0)
                {
                    return result;
                }

                result = (a.Priority ?? 99).CompareTo(b.Priority ?? 99);
                if (result != 0)
                {
                    return result;
                }

                return string.Compare(a.Title, b.Title, StringComparison.Ordinal);
            });
        }

        return childrenOf;
    }
}
