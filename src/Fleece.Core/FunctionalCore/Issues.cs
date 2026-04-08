using Fleece.Core.Models;
using Fleece.Core.Utilities;

namespace Fleece.Core.FunctionalCore;

/// <summary>
/// Pure functions for issue filtering, searching, and graph building.
/// All methods are static and operate on in-memory issue collections with no I/O.
/// </summary>
public static class Issues
{
    /// <summary>
    /// Terminal statuses that are excluded from results by default (when includeTerminal is false).
    /// </summary>
    private static readonly IssueStatus[] TerminalStatuses =
        [IssueStatus.Complete, IssueStatus.Archived, IssueStatus.Closed, IssueStatus.Deleted];

    /// <summary>
    /// Filters issues by various criteria.
    /// </summary>
    public static IReadOnlyList<Issue> Filter(
        IReadOnlyList<Issue> issues,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false)
    {
        return issues
            .Where(i => status is null || i.Status == status)
            // Exclude terminal statuses unless includeTerminal is true or a specific status was requested
            .Where(i => status is not null || includeTerminal || !TerminalStatuses.Contains(i.Status))
            .Where(i => type is null || i.Type == type)
            .Where(i => priority is null || i.Priority == priority)
            .Where(i => assignedTo is null || string.Equals(i.AssignedTo, assignedTo, StringComparison.OrdinalIgnoreCase))
            .Where(i => tags is null || tags.Count == 0 || tags.Any(t => MatchesTag(i, t)))
            .Where(i => linkedPr is null || i.LinkedPRs.Contains(linkedPr.Value) || i.LinkedPR == linkedPr)
            .ToList();
    }

    /// <summary>
    /// Searches issues by text across title, description, and tags.
    /// Supports key:value pattern for keyed tag searches.
    /// </summary>
    public static IReadOnlyList<Issue> Search(IReadOnlyList<Issue> issues, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        // Check for key:value pattern (keyed tag search)
        // Pattern must have colon, no spaces, and content on both sides
        var colonIndex = query.IndexOf(':');
        if (colonIndex > 0 && colonIndex < query.Length - 1 && !query.Contains(' '))
        {
            var searchKey = query[..colonIndex];
            var searchValue = query[(colonIndex + 1)..];
            return issues.Where(i => HasKeyedTag(i, searchKey, searchValue)).ToList();
        }

        // Existing substring search
        return issues
            .Where(i =>
                i.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (i.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.Tags?.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)) ?? false))
            .ToList();
    }

    /// <summary>
    /// Builds a complete issue graph from all issues.
    /// The graph includes parent-child and next/previous relationships.
    /// </summary>
    public static IssueGraph BuildGraph(IReadOnlyList<Issue> issues)
    {
        if (issues.Count == 0)
        {
            return new IssueGraph
            {
                Nodes = new Dictionary<string, IssueGraphNode>(StringComparer.OrdinalIgnoreCase),
                RootIssueIds = []
            };
        }

        // Build lookups
        var issueList = issues as List<Issue> ?? issues.ToList();
        var issueLookup = issueList.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var childrenOf = BuildChildrenLookup(issueList, issueLookup);

        // Compute Next/Previous for each issue (Series mode only)
        var nextPrevious = ComputeNextPrevious(issueList, issueLookup, childrenOf);

        // Build nodes
        var nodes = new Dictionary<string, IssueGraphNode>(StringComparer.OrdinalIgnoreCase);
        var rootIds = new List<string>();

        foreach (var issue in issueList)
        {
            var parentIds = issue.ParentIssues
                .Where(p => issueLookup.ContainsKey(p.ParentIssue))
                .Select(p => p.ParentIssue)
                .ToList();

            var childIds = childrenOf.TryGetValue(issue.Id, out var children)
                ? children.Select(c => c.Id).ToList()
                : new List<string>();

            var (prevIds, nextIds) = nextPrevious.TryGetValue(issue.Id, out var np)
                ? np
                : (new List<string>(), new List<string>());

            var hasIncompleteChildren = childIds.Any(id =>
                issueLookup.TryGetValue(id, out var child) && !child.Status.IsDone());

            var allPreviousDone = prevIds.All(id =>
                !issueLookup.TryGetValue(id, out var prev) || prev.Status.IsDone());

            var parentExecMode = GetParentExecutionMode(issue, issueLookup);

            nodes[issue.Id] = new IssueGraphNode
            {
                Issue = issue,
                ChildIssueIds = childIds,
                ParentIssueIds = parentIds,
                PreviousIssueIds = prevIds,
                NextIssueIds = nextIds,
                HasIncompleteChildren = hasIncompleteChildren,
                AllPreviousDone = allPreviousDone,
                ParentExecutionMode = parentExecMode
            };

            if (parentIds.Count == 0)
            {
                rootIds.Add(issue.Id);
            }
        }

        return new IssueGraph
        {
            Nodes = nodes,
            RootIssueIds = rootIds
        };
    }

    /// <summary>
    /// Builds a filtered subgraph based on the query parameters.
    /// Next/Previous relationships are computed from the FULL graph,
    /// then the subgraph is filtered.
    /// </summary>
    public static IssueGraph QueryGraph(
        IReadOnlyList<Issue> issues,
        GraphQuery query,
        GraphSortConfig? sort = null)
    {
        // Build full graph first
        var fullGraph = BuildGraph(issues);

        // Determine which issues to include
        var includedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in fullGraph.Nodes.Values)
        {
            if (ShouldIncludeInQuery(node, query))
            {
                includedIds.Add(node.Issue.Id);
            }
        }

        // Handle IncludeInactiveWithActiveDescendants
        if (query.IncludeInactiveWithActiveDescendants)
        {
            var activeIds = includedIds.ToList();
            foreach (var id in activeIds)
            {
                var node = fullGraph.GetNode(id);
                if (node == null)
                {
                    continue;
                }

                foreach (var parentId in node.ParentIssueIds)
                {
                    IncludeAncestorsWithActiveDescendants(parentId, fullGraph, includedIds);
                }
            }
        }

        // Handle RootIssueId scoping
        if (!string.IsNullOrWhiteSpace(query.RootIssueId))
        {
            var scopedIds = GetDescendantIds(query.RootIssueId, fullGraph);
            scopedIds.Add(query.RootIssueId); // Include the root itself
            includedIds.IntersectWith(scopedIds);
        }

        // Build filtered graph
        var filteredNodes = new Dictionary<string, IssueGraphNode>(StringComparer.OrdinalIgnoreCase);
        var filteredRoots = new List<string>();

        foreach (var id in includedIds)
        {
            if (fullGraph.Nodes.TryGetValue(id, out var node))
            {
                filteredNodes[id] = node;

                // Check if this is a root in the filtered context
                var hasParentInFiltered = node.ParentIssueIds.Any(p => includedIds.Contains(p));
                if (!hasParentInFiltered)
                {
                    filteredRoots.Add(id);
                }
            }
        }

        return new IssueGraph
        {
            Nodes = filteredNodes,
            RootIssueIds = filteredRoots
        };
    }

    /// <summary>
    /// Gets all issues that are currently actionable (can be worked on next).
    /// </summary>
    public static IReadOnlyList<Issue> GetNextIssues(
        IReadOnlyList<Issue> issues,
        string? parentId = null,
        GraphSortConfig? sort = null)
    {
        var graph = BuildGraph(issues);

        // Find actionable issues
        var actionable = graph.Nodes.Values
            .Where(IsActionable)
            .ToList();

        // Apply parent filter if specified
        if (!string.IsNullOrWhiteSpace(parentId))
        {
            var descendants = GetDescendantIds(parentId, graph);
            actionable = actionable.Where(n => descendants.Contains(n.Issue.Id)).ToList();
        }

        var result = actionable.Select(n => n.Issue).ToList();
        ApplyGraphSort(result, sort ?? GraphSortConfig.Default);
        return result;
    }

    /// <summary>
    /// Builds a positioned task graph layout for rendering.
    /// </summary>
    public static TaskGraph BuildTaskGraphLayout(
        IReadOnlyList<Issue> issues,
        InactiveVisibility visibility = InactiveVisibility.Hide,
        string? assignedTo = null,
        GraphSortConfig? sort = null)
    {
        var issueList = issues as List<Issue> ?? issues.ToList();

        if (issueList.Count == 0)
        {
            return new TaskGraph { Nodes = [], TotalLanes = 0 };
        }

        // Build a lookup for ALL issues (needed to find terminal parents)
        var fullLookup = issueList.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Filter issues based on parameters
        var activeIssues = issueList.Where(i =>
            (visibility == InactiveVisibility.Always || !i.Status.IsTerminal()) &&
            (assignedTo == null || string.Equals(i.AssignedTo, assignedTo, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        if (activeIssues.Count == 0)
        {
            return new TaskGraph { Nodes = [], TotalLanes = 0 };
        }

        // For IfHasActiveDescendants mode, find terminal issues that have active descendants
        if (visibility == InactiveVisibility.IfHasActiveDescendants)
        {
            var terminalWithActiveDescendants = CollectTerminalIssuesWithActiveDescendants(activeIssues, fullLookup);
            activeIssues.AddRange(terminalWithActiveDescendants);
        }

        // Collect ancestors of active issues (even terminal ones) to provide hierarchy context
        var issuesToDisplay = CollectIssuesToDisplay(activeIssues, fullLookup);

        // Build lookup for display issues
        var issueLookup = issuesToDisplay.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Determine which issues are actionable
        var graph = BuildGraph(issues);
        var actionableIds = new HashSet<string>(
            graph.Nodes.Values.Where(IsActionable).Select(n => n.Issue.Id),
            StringComparer.OrdinalIgnoreCase);

        // Build children lookup
        var childrenOf = BuildChildrenLookup(issuesToDisplay, issueLookup);

        // Find root issues (no parent in the display set)
        var rootIssues = issuesToDisplay
            .Where(i => i.ParentIssues.Count == 0 ||
                        i.ParentIssues.All(p => !issueLookup.ContainsKey(p.ParentIssue)))
            .ToList();
        ApplyGraphSort(rootIssues, sort ?? GraphSortConfig.Default);

        if (rootIssues.Count == 0)
        {
            return new TaskGraph { Nodes = [], TotalLanes = 0 };
        }

        // Layout each root subtree
        var nodeList = new List<TaskGraphNode>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int maxLane = 0;

        foreach (var root in rootIssues)
        {
            var rootMax = LayoutSubtree(root, 0, nodeList, childrenOf, issueLookup, actionableIds, visited, parentExecutionMode: null);
            maxLane = Math.Max(maxLane, rootMax);
        }

        var finalNodes = AssignAppearanceCounts(nodeList);

        return new TaskGraph
        {
            Nodes = finalNodes,
            TotalLanes = maxLane + 1
        };
    }

    /// <summary>
    /// Builds a positioned task graph layout filtered by the given matched issue IDs.
    /// </summary>
    public static TaskGraph BuildFilteredTaskGraphLayout(
        IReadOnlyList<Issue> issues,
        IReadOnlySet<string> matchedIds,
        GraphSortConfig? sort = null)
    {
        var issueList = issues as List<Issue> ?? issues.ToList();

        if (issueList.Count == 0 || matchedIds.Count == 0)
        {
            return new TaskGraph { Nodes = [], TotalLanes = 0, MatchedIds = matchedIds };
        }

        // Build full lookup
        var fullLookup = issueList.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Collect matched issues
        var matchedIssues = new List<Issue>();
        foreach (var id in matchedIds)
        {
            if (fullLookup.TryGetValue(id, out var issue))
            {
                matchedIssues.Add(issue);
            }
        }

        if (matchedIssues.Count == 0)
        {
            return new TaskGraph { Nodes = [], TotalLanes = 0, MatchedIds = matchedIds };
        }

        // Collect all ancestor issues for context (walk up parent chains)
        var contextIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toProcess = new Queue<Issue>(matchedIssues);
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (toProcess.Count > 0)
        {
            var issue = toProcess.Dequeue();
            if (!processed.Add(issue.Id))
            {
                continue;
            }

            foreach (var parentRef in issue.ParentIssues)
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

        // Combine matched + context issues
        var issuesToDisplay = new List<Issue>();
        foreach (var issue in matchedIssues)
        {
            issuesToDisplay.Add(issue);
        }
        foreach (var id in contextIds)
        {
            if (fullLookup.TryGetValue(id, out var issue))
            {
                issuesToDisplay.Add(issue);
            }
        }

        // Build lookup for display issues
        var issueLookup = issuesToDisplay.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Determine actionable issues
        var graph = BuildGraph(issues);
        var actionableIds = new HashSet<string>(
            graph.Nodes.Values.Where(IsActionable).Select(n => n.Issue.Id),
            StringComparer.OrdinalIgnoreCase);

        // Build children lookup
        var childrenOf = BuildChildrenLookup(issuesToDisplay, issueLookup);

        // Find root issues (no parent in the display set)
        var rootIssues = issuesToDisplay
            .Where(i => i.ParentIssues.Count == 0 ||
                        i.ParentIssues.All(p => !issueLookup.ContainsKey(p.ParentIssue)))
            .ToList();
        ApplyGraphSort(rootIssues, sort ?? GraphSortConfig.Default);

        if (rootIssues.Count == 0)
        {
            return new TaskGraph { Nodes = [], TotalLanes = 0, MatchedIds = matchedIds };
        }

        // Layout each root subtree
        var nodeList = new List<TaskGraphNode>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int maxLane = 0;

        foreach (var root in rootIssues)
        {
            var rootMax = LayoutSubtree(root, 0, nodeList, childrenOf, issueLookup, actionableIds, visited, parentExecutionMode: null);
            maxLane = Math.Max(maxLane, rootMax);
        }

        var finalNodes = AssignAppearanceCounts(nodeList);

        return new TaskGraph
        {
            Nodes = finalNodes,
            TotalLanes = maxLane + 1,
            MatchedIds = matchedIds
        };
    }

    #region Tag Matching

    /// <summary>
    /// Matches a tag filter value against an issue's tags.
    /// If the filter contains '=', matches as an exact key=value keyed tag.
    /// Otherwise, matches as a simple tag (exact match) or as a key-only keyed tag match.
    /// </summary>
    private static bool MatchesTag(Issue issue, string tagFilter)
    {
        var equalsIndex = tagFilter.IndexOf('=');
        if (equalsIndex > 0 && equalsIndex < tagFilter.Length - 1)
        {
            // key=value format: exact keyed tag match
            var key = tagFilter[..equalsIndex];
            var value = tagFilter[(equalsIndex + 1)..];
            return HasKeyedTag(issue, key, value);
        }

        // Key-only or simple tag: match exact simple tags OR any keyed tag with this key
        return (issue.Tags?.Contains(tagFilter, StringComparer.OrdinalIgnoreCase) ?? false)
            || HasTagKey(issue, tagFilter);
    }

    /// <summary>
    /// Checks if an issue has a keyed tag matching the given key and value.
    /// </summary>
    private static bool HasKeyedTag(Issue issue, string key, string value)
    {
        if (issue.Tags is null || issue.Tags.Count == 0)
        {
            return false;
        }

        foreach (var tag in issue.Tags)
        {
            var eqIdx = tag.IndexOf('=');
            if (eqIdx <= 0 || eqIdx == tag.Length - 1)
            {
                continue;
            }

            var tagKey = tag[..eqIdx];
            var tagValue = tag[(eqIdx + 1)..];

            if (tagKey.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                tagValue.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if an issue has any tag with the given key.
    /// </summary>
    private static bool HasTagKey(Issue issue, string key)
    {
        if (issue.Tags is null || issue.Tags.Count == 0)
        {
            return false;
        }

        foreach (var tag in issue.Tags)
        {
            var eqIdx = tag.IndexOf('=');
            string tagKey;
            if (eqIdx > 0)
            {
                tagKey = tag[..eqIdx];
            }
            else
            {
                tagKey = tag;
            }

            if (tagKey.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Graph Building Helpers

    /// <summary>
    /// Computes Next/Previous relationships for issues.
    /// ONLY siblings under a Series execution mode parent have Next/Previous.
    /// </summary>
    private static Dictionary<string, (List<string> Prev, List<string> Next)> ComputeNextPrevious(
        List<Issue> issues,
        Dictionary<string, Issue> issueLookup,
        Dictionary<string, List<Issue>> childrenOf)
    {
        var result = issues.ToDictionary(
            i => i.Id,
            _ => (Prev: new List<string>(), Next: new List<string>()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in childrenOf)
        {
            var parentId = kvp.Key;
            var children = kvp.Value;

            // Only compute next/previous for Series mode parents
            if (!issueLookup.TryGetValue(parentId, out var parent) ||
                parent.ExecutionMode != ExecutionMode.Series)
            {
                continue;
            }

            for (int i = 0; i < children.Count; i++)
            {
                var childId = children[i].Id;
                if (!result.ContainsKey(childId))
                {
                    continue;
                }

                // Previous is the immediately preceding sibling
                if (i > 0)
                {
                    var prevId = children[i - 1].Id;
                    if (!result[childId].Prev.Contains(prevId))
                    {
                        result[childId].Prev.Add(prevId);
                    }
                }

                // Next is the immediately following sibling
                if (i < children.Count - 1)
                {
                    var nextId = children[i + 1].Id;
                    if (!result[childId].Next.Contains(nextId))
                    {
                        result[childId].Next.Add(nextId);
                    }
                }
            }
        }

        return result;
    }

    private static ExecutionMode? GetParentExecutionMode(Issue issue, Dictionary<string, Issue> issueLookup)
    {
        foreach (var parentRef in issue.ParentIssues)
        {
            if (issueLookup.TryGetValue(parentRef.ParentIssue, out var parent))
            {
                return parent.ExecutionMode;
            }
        }
        return null;
    }

    #endregion

    #region Query Helpers

    private static bool ShouldIncludeInQuery(IssueGraphNode node, GraphQuery query)
    {
        var issue = node.Issue;

        // Status filter
        if (query.Status.HasValue && issue.Status != query.Status.Value)
        {
            return false;
        }

        // Exclude terminal unless explicitly included or a specific status was requested
        if (!query.IncludeTerminal && query.Status is null && issue.Status.IsTerminal())
        {
            return false;
        }

        // Type filter
        if (query.Type.HasValue && issue.Type != query.Type.Value)
        {
            return false;
        }

        // Priority filter
        if (query.Priority.HasValue && issue.Priority != query.Priority.Value)
        {
            return false;
        }

        // AssignedTo filter
        if (!string.IsNullOrWhiteSpace(query.AssignedTo) &&
            !string.Equals(issue.AssignedTo, query.AssignedTo, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Tags filter (any match)
        if (query.Tags is { Count: > 0 } &&
            !query.Tags.Any(t => issue.Tags?.Contains(t, StringComparer.OrdinalIgnoreCase) ?? false))
        {
            return false;
        }

        // LinkedPr filter
        if (query.LinkedPr.HasValue && issue.LinkedPR != query.LinkedPr.Value)
        {
            return false;
        }

        // SearchText filter
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var matchesTitle = issue.Title.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase);
            var matchesDescription = issue.Description?.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ?? false;
            var matchesTags = issue.Tags?.Any(t => t.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase)) ?? false;

            if (!matchesTitle && !matchesDescription && !matchesTags)
            {
                return false;
            }
        }

        return true;
    }

    private static void IncludeAncestorsWithActiveDescendants(
        string parentId,
        IssueGraph graph,
        HashSet<string> includedIds)
    {
        var node = graph.GetNode(parentId);
        if (node == null)
        {
            return;
        }

        if (includedIds.Add(parentId))
        {
            foreach (var grandparentId in node.ParentIssueIds)
            {
                IncludeAncestorsWithActiveDescendants(grandparentId, graph, includedIds);
            }
        }
    }

    internal static HashSet<string> GetDescendantIds(string parentId, IssueGraph graph)
    {
        var descendants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toProcess = new Queue<string>();
        toProcess.Enqueue(parentId);

        while (toProcess.Count > 0)
        {
            var currentId = toProcess.Dequeue();
            var node = graph.GetNode(currentId);
            if (node == null)
            {
                continue;
            }

            foreach (var childId in node.ChildIssueIds)
            {
                if (descendants.Add(childId))
                {
                    toProcess.Enqueue(childId);
                }
            }
        }

        return descendants;
    }

    #endregion

    #region Actionable Issue Detection

    /// <summary>
    /// Determines if a graph node represents an actionable issue.
    /// </summary>
    internal static bool IsActionable(IssueGraphNode node)
    {
        var issue = node.Issue;

        // Ideas are never actionable
        if (issue.Type == IssueType.Idea)
        {
            return false;
        }

        // Must be Open or Review status to be actionable
        if (issue.Status != IssueStatus.Open && issue.Status != IssueStatus.Review)
        {
            return false;
        }

        // Parent issues with incomplete children cannot be next for completion
        if (node.HasIncompleteChildren)
        {
            return false;
        }

        // All previous issues must be done
        if (!node.AllPreviousDone)
        {
            return false;
        }

        return true;
    }

    #endregion

    #region Task Graph Layout

    /// <summary>
    /// Recursively lays out a subtree, emitting nodes in depth-first order.
    /// </summary>
    private static int LayoutSubtree(
        Issue issue,
        int startLane,
        List<TaskGraphNode> nodeList,
        Dictionary<string, List<Issue>> childrenOf,
        Dictionary<string, Issue> issueLookup,
        HashSet<string> actionableIds,
        HashSet<string> visited,
        ExecutionMode? parentExecutionMode,
        string? renderingParentIdForFirstLeaf = null)
    {
        // If already visited, add a duplicate leaf node (no children traversal)
        if (!visited.Add(issue.Id))
        {
            nodeList.Add(new TaskGraphNode
            {
                Issue = issue,
                Row = nodeList.Count,
                Lane = startLane,
                IsActionable = actionableIds.Contains(issue.Id),
                ParentExecutionMode = parentExecutionMode,
                RenderingParentId = renderingParentIdForFirstLeaf
            });
            return startLane;
        }

        // Get incomplete children of this issue
        var incompleteChildren = GetIncompleteChildrenForLayout(issue, childrenOf);

        // Leaf issue (no incomplete children)
        if (incompleteChildren.Count == 0)
        {
            nodeList.Add(new TaskGraphNode
            {
                Issue = issue,
                Row = nodeList.Count,
                Lane = startLane,
                IsActionable = actionableIds.Contains(issue.Id),
                ParentExecutionMode = parentExecutionMode,
                RenderingParentId = renderingParentIdForFirstLeaf
            });
            return startLane;
        }

        int maxLane;

        if (issue.ExecutionMode == ExecutionMode.Parallel)
        {
            maxLane = LayoutParallelChildren(issue, startLane, incompleteChildren, nodeList, childrenOf, issueLookup, actionableIds, visited, renderingParentIdForFirstLeaf);
        }
        else // Series (default)
        {
            maxLane = LayoutSeriesChildren(issue, startLane, incompleteChildren, nodeList, childrenOf, issueLookup, actionableIds, visited, renderingParentIdForFirstLeaf);
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

    private static int LayoutParallelChildren(
        Issue parent,
        int startLane,
        List<Issue> children,
        List<TaskGraphNode> nodeList,
        Dictionary<string, List<Issue>> childrenOf,
        Dictionary<string, Issue> issueLookup,
        HashSet<string> actionableIds,
        HashSet<string> visited,
        string? renderingParentIdForFirstLeaf = null)
    {
        int maxChildLane = startLane;
        bool isFirstChild = true;

        foreach (var child in children)
        {
            var childIncomplete = GetIncompleteChildrenForLayout(child, childrenOf);

            string? childRenderingParent = isFirstChild ? renderingParentIdForFirstLeaf : null;

            if (childIncomplete.Count == 0 || visited.Contains(child.Id))
            {
                visited.Add(child.Id);
                nodeList.Add(new TaskGraphNode
                {
                    Issue = child,
                    Row = nodeList.Count,
                    Lane = startLane,
                    IsActionable = actionableIds.Contains(child.Id),
                    ParentExecutionMode = ExecutionMode.Parallel,
                    RenderingParentId = childRenderingParent
                });
            }
            else
            {
                var childMax = LayoutSubtree(child, startLane, nodeList, childrenOf, issueLookup, actionableIds, visited, parentExecutionMode: ExecutionMode.Parallel, renderingParentIdForFirstLeaf: childRenderingParent);
                maxChildLane = Math.Max(maxChildLane, childMax);
            }

            isFirstChild = false;
        }

        return maxChildLane;
    }

    private static int LayoutSeriesChildren(
        Issue parent,
        int startLane,
        List<Issue> children,
        List<TaskGraphNode> nodeList,
        Dictionary<string, List<Issue>> childrenOf,
        Dictionary<string, Issue> issueLookup,
        HashSet<string> actionableIds,
        HashSet<string> visited,
        string? renderingParentIdForFirstLeaf = null)
    {
        int currentLane = startLane;
        bool isFirstChild = true;
        string? previousSiblingId = null;

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];

            var childIncomplete = GetIncompleteChildrenForLayout(child, childrenOf);

            string? childRenderingParent = isFirstChild ? renderingParentIdForFirstLeaf : previousSiblingId;

            if (childIncomplete.Count == 0 || visited.Contains(child.Id))
            {
                visited.Add(child.Id);
                nodeList.Add(new TaskGraphNode
                {
                    Issue = child,
                    Row = nodeList.Count,
                    Lane = currentLane,
                    IsActionable = actionableIds.Contains(child.Id),
                    ParentExecutionMode = ExecutionMode.Series,
                    RenderingParentId = childRenderingParent
                });
            }
            else
            {
                int subtreeStart = isFirstChild ? currentLane : currentLane + 1;
                var childMax = LayoutSubtree(child, subtreeStart, nodeList, childrenOf, issueLookup, actionableIds, visited, parentExecutionMode: ExecutionMode.Series, renderingParentIdForFirstLeaf: childRenderingParent);
                currentLane = childMax;
            }

            previousSiblingId = child.Id;
            isFirstChild = false;
        }

        return currentLane;
    }

    private static List<TaskGraphNode> AssignAppearanceCounts(List<TaskGraphNode> nodeList)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodeList)
        {
            counts.TryGetValue(node.Issue.Id, out var count);
            counts[node.Issue.Id] = count + 1;
        }

        var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<TaskGraphNode>(nodeList.Count);
        foreach (var node in nodeList)
        {
            var total = counts[node.Issue.Id];
            indices.TryGetValue(node.Issue.Id, out var idx);
            idx++;
            indices[node.Issue.Id] = idx;

            result.Add(node with { AppearanceIndex = idx, TotalAppearances = total });
        }

        return result;
    }

    private static List<Issue> GetIncompleteChildrenForLayout(Issue issue, Dictionary<string, List<Issue>> childrenOf)
    {
        if (!childrenOf.TryGetValue(issue.Id, out var children))
        {
            return [];
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

    #endregion

    #region Shared Helpers

    /// <summary>
    /// Builds a lookup from parent ID to sorted list of children.
    /// </summary>
    internal static Dictionary<string, List<Issue>> BuildChildrenLookup(
        List<Issue> issues,
        Dictionary<string, Issue> issueLookup)
    {
        var childrenOf = new Dictionary<string, List<Issue>>(StringComparer.OrdinalIgnoreCase);

        foreach (var issue in issues)
        {
            foreach (var parentRef in issue.ParentIssues)
            {
                if (!issueLookup.ContainsKey(parentRef.ParentIssue))
                {
                    continue; // Parent not in set
                }

                if (!childrenOf.TryGetValue(parentRef.ParentIssue, out var children))
                {
                    children = [];
                    childrenOf[parentRef.ParentIssue] = children;
                }

                children.Add(issue);
            }
        }

        // Sort each children list by SortOrder (lexical order) only
        foreach (var kvp in childrenOf)
        {
            var parentId = kvp.Key;

            kvp.Value.Sort((a, b) =>
            {
                var sortA = a.ParentIssues
                    .First(p => string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase))
                    .SortOrder;
                var sortB = b.ParentIssues
                    .First(p => string.Equals(p.ParentIssue, parentId, StringComparison.OrdinalIgnoreCase))
                    .SortOrder;
                return string.Compare(sortA, sortB, StringComparison.Ordinal);
            });
        }

        return childrenOf;
    }

    /// <summary>
    /// Applies a configurable sort to a list of issues in-place.
    /// </summary>
    internal static void ApplyGraphSort(List<Issue> issues, GraphSortConfig config)
    {
        var rules = config.Rules;
        if (rules.Count == 0)
        {
            return;
        }

        issues.Sort((a, b) =>
        {
            foreach (var rule in rules)
            {
                var result = rule.Criteria switch
                {
                    GraphSortCriteria.CreatedAt => a.CreatedAt.CompareTo(b.CreatedAt),
                    GraphSortCriteria.Priority => (a.Priority ?? 99).CompareTo(b.Priority ?? 99),
                    GraphSortCriteria.HasDescription =>
                        (string.IsNullOrWhiteSpace(a.Description) ? 1 : 0)
                            .CompareTo(string.IsNullOrWhiteSpace(b.Description) ? 1 : 0),
                    GraphSortCriteria.Title => string.Compare(a.Title, b.Title, StringComparison.Ordinal),
                    _ => 0
                };

                if (rule.Direction == SortDirection.Descending)
                {
                    result = -result;
                }

                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        });
    }

    /// <summary>
    /// Collects the set of issues to display in the task graph.
    /// </summary>
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

            foreach (var parentRef in issue.ParentIssues)
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
            foreach (var parentRef in issue.ParentIssues)
            {
                if (!childrenOf.TryGetValue(parentRef.ParentIssue, out var children))
                {
                    children = [];
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

    #endregion

    #region Sort Order Normalization

    /// <summary>
    /// Groups siblings by parent, sorts alphabetically by title, and assigns LexoRank values
    /// to any refs with missing SortOrder.
    /// </summary>
    public static IReadOnlyList<Issue> NormalizeSortOrders(IReadOnlyList<Issue> issues)
    {
        // Find all issues that have at least one ParentIssueRef with null/empty SortOrder
        var needsNormalization = issues.Any(i =>
            i.ParentIssues.Any(p => string.IsNullOrEmpty(p.SortOrder)));

        if (!needsNormalization)
        {
            return issues;
        }

        // Build a lookup: parentId -> list of (issue, parentRef index) for refs missing SortOrder
        var missingByParent = new Dictionary<string, List<(Issue Issue, int RefIndex)>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < issues.Count; i++)
        {
            var issue = issues[i];
            for (var j = 0; j < issue.ParentIssues.Count; j++)
            {
                var parentRef = issue.ParentIssues[j];
                if (string.IsNullOrEmpty(parentRef.SortOrder))
                {
                    if (!missingByParent.TryGetValue(parentRef.ParentIssue, out var list))
                    {
                        list = [];
                        missingByParent[parentRef.ParentIssue] = list;
                    }

                    list.Add((issue, j));
                }
            }
        }

        if (missingByParent.Count == 0)
        {
            return issues;
        }

        // Track which issues need updating: issueId -> new ParentIssues list
        var updatedParentIssues = new Dictionary<string, List<ParentIssueRef>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (parentId, siblings) in missingByParent)
        {
            // Sort siblings alphabetically by title for deterministic ordering
            var sorted = siblings.OrderBy(s => s.Issue.Title, StringComparer.Ordinal).ToList();
            var ranks = LexoRank.GenerateInitialRanks(sorted.Count);

            for (var i = 0; i < sorted.Count; i++)
            {
                var (issue, refIndex) = sorted[i];

                if (!updatedParentIssues.TryGetValue(issue.Id, out var parentList))
                {
                    parentList = new List<ParentIssueRef>(issue.ParentIssues);
                    updatedParentIssues[issue.Id] = parentList;
                }

                parentList[refIndex] = parentList[refIndex] with { SortOrder = ranks[i] };
            }
        }

        // Rebuild the issues list with updated ParentIssues
        var result = new List<Issue>(issues.Count);
        foreach (var issue in issues)
        {
            if (updatedParentIssues.TryGetValue(issue.Id, out var newParents))
            {
                result.Add(issue with { ParentIssues = newParents });
            }
            else
            {
                result.Add(issue);
            }
        }

        return result;
    }

    #endregion
}
