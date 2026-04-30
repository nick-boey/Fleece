using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Models.Graph;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services.GraphLayout;

/// <summary>
/// Fleece-specific adapter that wires the generic <see cref="IGraphLayoutService"/> to
/// the issue model. Handles visibility filtering, ancestor-context collection, sort, and
/// the <c>HasActiveDescendants</c> child-pruning rule.
/// </summary>
public sealed class IssueLayoutService : IIssueLayoutService
{
    private readonly IGraphLayoutService _engine;

    public IssueLayoutService(IGraphLayoutService engine)
    {
        _engine = engine;
    }

    public GraphLayout<Issue> LayoutForTree(
        IReadOnlyList<Issue> issues,
        InactiveVisibility visibility = InactiveVisibility.Hide,
        string? assignedTo = null,
        GraphSortConfig? sort = null,
        LayoutMode mode = LayoutMode.IssueGraph)
    {
        if (issues.Count == 0)
        {
            return EmptyLayout();
        }

        var fullLookup = issues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        var activeIssues = issues.Where(i =>
            (visibility == InactiveVisibility.Always || !i.Status.IsTerminal()) &&
            (assignedTo == null || string.Equals(i.AssignedTo, assignedTo, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        if (activeIssues.Count == 0)
        {
            return EmptyLayout();
        }

        if (visibility == InactiveVisibility.IfHasActiveDescendants)
        {
            var terminalWithActive = CollectTerminalIssuesWithActiveDescendants(activeIssues, fullLookup);
            activeIssues.AddRange(terminalWithActive);
        }

        var displayList = CollectIssuesToDisplay(activeIssues, fullLookup);
        return RunEngine(displayList, sort, mode);
    }

    public GraphLayout<Issue> LayoutForNext(
        IReadOnlyList<Issue> issues,
        IReadOnlySet<string>? matchedIds = null,
        InactiveVisibility visibility = InactiveVisibility.Hide,
        string? assignedTo = null,
        GraphSortConfig? sort = null)
    {
        if (matchedIds is null)
        {
            return LayoutForTree(issues, visibility, assignedTo, sort);
        }

        if (issues.Count == 0 || matchedIds.Count == 0)
        {
            return EmptyLayout();
        }

        var fullLookup = issues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        var matched = new List<Issue>();
        foreach (var id in matchedIds)
        {
            if (fullLookup.TryGetValue(id, out var issue))
            {
                matched.Add(issue);
            }
        }
        if (matched.Count == 0)
        {
            return EmptyLayout();
        }

        var displayList = CollectMatchedAndAncestors(matched, matchedIds, fullLookup);
        return RunEngine(displayList, sort);
    }

    private GraphLayout<Issue> RunEngine(
        List<Issue> displayList,
        GraphSortConfig? sort,
        LayoutMode mode = LayoutMode.IssueGraph)
    {
        var displayLookup = displayList.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var childrenOf = Issues.BuildChildrenLookup(displayList, displayLookup);

        var rootIssues = displayList
            .Where(i => i.ActiveParentIssues.Count == 0 ||
                        i.ActiveParentIssues.All(p => !displayLookup.ContainsKey(p.ParentIssue)))
            .ToList();
        Issues.ApplyGraphSort(rootIssues, sort ?? GraphSortConfig.Default);

        if (rootIssues.Count == 0)
        {
            // No node qualifies as a root, but issues exist — this only happens when
            // every issue has a parent inside the display set, i.e. the parent graph
            // contains a cycle. Surface it explicitly.
            if (displayList.Count > 0)
            {
                var cycle = FindParentCycle(displayList, childrenOf);
                if (cycle != null)
                {
                    throw new InvalidGraphException(cycle);
                }
            }
            return EmptyLayout();
        }

        var request = new GraphLayoutRequest<Issue>
        {
            AllNodes = displayList,
            RootFinder = _ => rootIssues,
            ChildIterator = parent => GetIncompleteChildrenForLayout(parent, childrenOf),
            Mode = mode
        };

        var result = _engine.Layout(request);
        return result switch
        {
            GraphLayoutResult<Issue>.Success success => success.Layout,
            GraphLayoutResult<Issue>.CycleDetected cycle => throw new InvalidGraphException(cycle.Cycle),
            _ => throw new InvalidOperationException($"Unexpected layout result type: {result.GetType().Name}")
        };
    }

    private static IReadOnlyList<string>? FindParentCycle(
        List<Issue> issues,
        Dictionary<string, List<Issue>> childrenOf)
    {
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pathStack = new Stack<string>();

        foreach (var issue in issues)
        {
            var cycle = Dfs(issue.Id);
            if (cycle != null)
            {
                return cycle;
            }
        }
        return null;

        IReadOnlyList<string>? Dfs(string id)
        {
            if (visiting.Contains(id))
            {
                var bottomToTop = pathStack.Reverse().ToList();
                var idx = bottomToTop.FindIndex(s =>
                    string.Equals(s, id, StringComparison.OrdinalIgnoreCase));
                var cyc = idx >= 0 ? bottomToTop.Skip(idx).ToList() : new List<string> { id };
                cyc.Add(id);
                return cyc;
            }
            if (visited.Contains(id))
            {
                return null;
            }

            visiting.Add(id);
            pathStack.Push(id);
            if (childrenOf.TryGetValue(id, out var children))
            {
                foreach (var child in children)
                {
                    var c = Dfs(child.Id);
                    if (c != null)
                    {
                        return c;
                    }
                }
            }
            pathStack.Pop();
            visiting.Remove(id);
            visited.Add(id);
            return null;
        }
    }

    private static GraphLayout<Issue> EmptyLayout() => new()
    {
        Nodes = Array.Empty<PositionedNode<Issue>>(),
        Edges = Array.Empty<Edge<Issue>>(),
        Occupancy = new OccupancyCell[0, 0],
        TotalRows = 0,
        TotalLanes = 0
    };

    private static List<Issue> GetIncompleteChildrenForLayout(
        Issue issue,
        Dictionary<string, List<Issue>> childrenOf)
    {
        if (!childrenOf.TryGetValue(issue.Id, out var children))
        {
            return new List<Issue>();
        }
        return children.Where(c => HasActiveDescendants(c, childrenOf)).ToList();
    }

    private static bool HasActiveDescendants(Issue issue, Dictionary<string, List<Issue>> childrenOf)
    {
        if (!issue.Status.IsDone())
        {
            return true;
        }
        if (!childrenOf.TryGetValue(issue.Id, out var children))
        {
            return false;
        }
        return children.Any(c => HasActiveDescendants(c, childrenOf));
    }

    private static List<Issue> CollectIssuesToDisplay(
        List<Issue> activeIssues,
        Dictionary<string, Issue> fullLookup)
    {
        var displayIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in activeIssues)
        {
            displayIds.Add(issue.Id);
        }

        var toProcess = new Queue<Issue>(activeIssues);
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (toProcess.Count > 0)
        {
            var issue = toProcess.Dequeue();
            if (!processed.Add(issue.Id))
            {
                continue;
            }
            foreach (var parentRef in issue.ActiveParentIssues)
            {
                if (fullLookup.TryGetValue(parentRef.ParentIssue, out var parent))
                {
                    if (displayIds.Add(parent.Id))
                    {
                        toProcess.Enqueue(parent);
                    }
                }
            }
        }

        var result = new List<Issue>();
        var addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in activeIssues)
        {
            result.Add(issue);
            addedIds.Add(issue.Id);
        }
        foreach (var id in displayIds)
        {
            if (!addedIds.Contains(id) && fullLookup.TryGetValue(id, out var issue) && issue.Status.IsTerminal())
            {
                result.Add(issue);
            }
        }
        return result;
    }

    private static List<Issue> CollectTerminalIssuesWithActiveDescendants(
        List<Issue> activeIssues,
        Dictionary<string, Issue> fullLookup)
    {
        var activeIds = new HashSet<string>(activeIssues.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);

        var childrenOf = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in fullLookup.Values)
        {
            foreach (var parentRef in issue.ActiveParentIssues)
            {
                if (!childrenOf.TryGetValue(parentRef.ParentIssue, out var children))
                {
                    children = new List<string>();
                    childrenOf[parentRef.ParentIssue] = children;
                }
                children.Add(issue.Id);
            }
        }

        var result = new List<Issue>();
        var checkedIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var issue in fullLookup.Values)
        {
            if (!issue.Status.IsTerminal() || activeIds.Contains(issue.Id))
            {
                continue;
            }
            if (HasActiveDescendant(issue.Id, childrenOf, activeIds, checkedIds))
            {
                result.Add(issue);
            }
        }
        return result;
    }

    private static bool HasActiveDescendant(
        string issueId,
        Dictionary<string, List<string>> childrenOf,
        HashSet<string> activeIds,
        Dictionary<string, bool> checkedIds)
    {
        if (checkedIds.TryGetValue(issueId, out var cached))
        {
            return cached;
        }
        checkedIds[issueId] = false;
        if (!childrenOf.TryGetValue(issueId, out var children))
        {
            return false;
        }
        foreach (var childId in children)
        {
            if (activeIds.Contains(childId) || HasActiveDescendant(childId, childrenOf, activeIds, checkedIds))
            {
                checkedIds[issueId] = true;
                return true;
            }
        }
        return false;
    }

    private static List<Issue> CollectMatchedAndAncestors(
        List<Issue> matched,
        IReadOnlySet<string> matchedIds,
        Dictionary<string, Issue> fullLookup)
    {
        var contextIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toProcess = new Queue<Issue>(matched);
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (toProcess.Count > 0)
        {
            var issue = toProcess.Dequeue();
            if (!processed.Add(issue.Id))
            {
                continue;
            }
            foreach (var parentRef in issue.ActiveParentIssues)
            {
                if (fullLookup.TryGetValue(parentRef.ParentIssue, out var parent))
                {
                    if (!matchedIds.Contains(parent.Id) && contextIds.Add(parent.Id))
                    {
                        toProcess.Enqueue(parent);
                    }
                }
            }
        }

        var result = new List<Issue>();
        foreach (var issue in matched)
        {
            result.Add(issue);
        }
        foreach (var id in contextIds)
        {
            if (fullLookup.TryGetValue(id, out var issue))
            {
                result.Add(issue);
            }
        }
        return result;
    }
}
