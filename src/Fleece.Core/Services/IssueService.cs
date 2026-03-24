using System.Text.RegularExpressions;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Utilities;

namespace Fleece.Core.Services;

/// <summary>
/// Unified issue service providing CRUD operations, filtering, searching,
/// and graph building/querying with computed relationships.
/// </summary>
public sealed partial class IssueService(
    IStorageService storage,
    IIdGenerator idGenerator,
    IGitConfigService gitConfigService,
    ITagService tagService) : IIssueService
{
    // Regex pattern for invalid Git branch name characters
    // Valid: alphanumeric, hyphen, underscore, forward slash (not at start/end), dot (not at start, not consecutive)
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9_\-/.]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$")]
    private static partial Regex ValidBranchNamePattern();

    [GeneratedRegex(@"[~^:\?\*\[\]@{}\s\\]|\.\.|\.$|^\.|\.lock$|//$|^/|/$")]
    private static partial Regex InvalidBranchNamePattern();

    /// <summary>
    /// Validates that a string is a valid Git branch name.
    /// </summary>
    internal static bool IsValidGitBranchName(string? branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return true; // null/empty is valid (means no branch)
        }

        // Check for invalid characters and patterns
        if (InvalidBranchNamePattern().IsMatch(branchName))
        {
            return false;
        }

        // Check for valid pattern
        return ValidBranchNamePattern().IsMatch(branchName);
    }

    public async Task<Issue> CreateAsync(
        string title,
        IssueType type,
        string? description = null,
        IssueStatus status = IssueStatus.Open,
        int? priority = null,
        int? linkedPr = null,
        IReadOnlyList<string>? linkedIssues = null,
        IReadOnlyList<ParentIssueRef>? parentIssues = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        string? workingBranchId = null,
        ExecutionMode? executionMode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (!IsValidGitBranchName(workingBranchId))
        {
            throw new ArgumentException("Working branch ID contains invalid characters for a Git branch name.", nameof(workingBranchId));
        }

        // Validate tags
        if (tags is not null)
        {
            var tagErrors = tagService.ValidateTags(tags);
            if (tagErrors.Count > 0)
            {
                throw new ArgumentException($"Invalid tags: {string.Join("; ", tagErrors)}", nameof(tags));
            }
        }

        var id = idGenerator.Generate(title);

        // Check for tombstone collision and retry with salt if needed
        var tombstones = await storage.LoadTombstonesAsync(cancellationToken);
        var tombstoneIds = tombstones.Select(t => t.IssueId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        const int maxSaltRetries = 10;
        for (var salt = 1; salt <= maxSaltRetries && tombstoneIds.Contains(id); salt++)
        {
            id = idGenerator.Generate(title, salt);
        }

        if (tombstoneIds.Contains(id))
        {
            throw new InvalidOperationException(
                $"Cannot generate a unique ID for title '{title}'. All salted variants collide with tombstoned issue IDs.");
        }

        var now = DateTimeOffset.UtcNow;
        var createdBy = gitConfigService.GetUserName();

        // Convert deprecated linkedPr parameter to tag
        var effectiveTags = tags ?? [];
        if (linkedPr.HasValue)
        {
            effectiveTags = KeyedTag.AddValue(effectiveTags, KeyedTag.LinkedPrKey, linkedPr.Value.ToString());
        }

        var issue = new Issue
        {
            Id = id,
            Title = title,
            TitleLastUpdate = now,
            TitleModifiedBy = createdBy,
            Description = description,
            DescriptionLastUpdate = description is not null ? now : null,
            DescriptionModifiedBy = description is not null ? createdBy : null,
            Status = status,
            StatusLastUpdate = now,
            StatusModifiedBy = createdBy,
            Type = type,
            TypeLastUpdate = now,
            TypeModifiedBy = createdBy,
            Priority = priority,
            PriorityLastUpdate = priority is not null ? now : null,
            PriorityModifiedBy = priority is not null ? createdBy : null,
            LinkedIssues = linkedIssues ?? [],
            LinkedIssuesLastUpdate = now,
            LinkedIssuesModifiedBy = createdBy,
            ParentIssues = parentIssues ?? [],
            ParentIssuesLastUpdate = now,
            ParentIssuesModifiedBy = createdBy,
            AssignedTo = assignedTo,
            AssignedToLastUpdate = assignedTo is not null ? now : null,
            AssignedToModifiedBy = assignedTo is not null ? createdBy : null,
            Tags = effectiveTags,
            TagsLastUpdate = now,
            TagsModifiedBy = createdBy,
            WorkingBranchId = workingBranchId,
            WorkingBranchIdLastUpdate = workingBranchId is not null ? now : null,
            WorkingBranchIdModifiedBy = workingBranchId is not null ? createdBy : null,
            ExecutionMode = executionMode ?? ExecutionMode.Series,
            ExecutionModeLastUpdate = executionMode is not null ? now : null,
            ExecutionModeModifiedBy = executionMode is not null ? createdBy : null,
            CreatedBy = createdBy,
            CreatedByLastUpdate = createdBy is not null ? now : null,
            LastUpdate = now,
            CreatedAt = now
        };

        await storage.AppendIssueAsync(issue, cancellationToken);

        return issue;
    }

    public async Task<IReadOnlyList<Issue>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await LoadAndNormalizeAsync(cancellationToken);
    }

    public async Task<Issue?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var issues = await LoadAndNormalizeAsync(cancellationToken);
        return issues.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<Issue>> GetIssueHierarchyAsync(
        string issueId,
        bool includeParents = true,
        bool includeChildren = true,
        CancellationToken cancellationToken = default)
    {
        var graph = await BuildGraphAsync(cancellationToken);

        // Check if issue exists in the graph
        if (!graph.Nodes.ContainsKey(issueId))
        {
            return [];
        }

        var resultIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { issueId };

        if (includeParents)
        {
            CollectAncestorIds(issueId, graph, resultIds);
        }

        if (includeChildren)
        {
            var descendants = GetDescendantIds(issueId, graph);
            foreach (var id in descendants)
            {
                resultIds.Add(id);
            }
        }

        return graph.Nodes.Values
            .Where(n => resultIds.Contains(n.Issue.Id))
            .Select(n => n.Issue)
            .ToList();
    }

    private static void CollectAncestorIds(string issueId, IssueGraph graph, HashSet<string> resultIds)
    {
        var node = graph.GetNode(issueId);
        if (node == null)
        {
            return;
        }

        foreach (var parentId in node.ParentIssueIds)
        {
            if (resultIds.Add(parentId))
            {
                CollectAncestorIds(parentId, graph, resultIds);
            }
        }
    }

    public async Task<IReadOnlyList<Issue>> ResolveByPartialIdAsync(string partialId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partialId))
        {
            return [];
        }

        var issues = await LoadAndNormalizeAsync(cancellationToken);

        // If partial ID is less than 3 characters, require exact match only
        if (partialId.Length < 3)
        {
            var exactMatch = issues.FirstOrDefault(i => i.Id.Equals(partialId, StringComparison.OrdinalIgnoreCase));
            return exactMatch is not null ? [exactMatch] : [];
        }

        // For 3+ characters, find all issues whose ID starts with the partial ID
        return issues
            .Where(i => i.Id.StartsWith(partialId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<Issue> UpdateAsync(
        string id,
        string? title = null,
        string? description = null,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        int? linkedPr = null,
        IReadOnlyList<string>? linkedIssues = null,
        IReadOnlyList<ParentIssueRef>? parentIssues = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        string? workingBranchId = null,
        ExecutionMode? executionMode = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidGitBranchName(workingBranchId))
        {
            throw new ArgumentException("Working branch ID contains invalid characters for a Git branch name.", nameof(workingBranchId));
        }

        // Validate tags
        if (tags is not null)
        {
            var tagErrors = tagService.ValidateTags(tags);
            if (tagErrors.Count > 0)
            {
                throw new ArgumentException($"Invalid tags: {string.Join("; ", tagErrors)}", nameof(tags));
            }
        }

        var issues = (await LoadAndNormalizeAsync(cancellationToken)).ToList();
        var existingIndex = issues.FindIndex(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (existingIndex < 0)
        {
            throw new KeyNotFoundException($"Issue with ID '{id}' not found.");
        }

        var existing = issues[existingIndex];
        var now = DateTimeOffset.UtcNow;
        var modifiedBy = gitConfigService.GetUserName();
        var newId = existing.Id;

        // Determine effective tags - apply linkedPr if provided (deprecated parameter)
        var effectiveTags = tags ?? existing.Tags;
        var tagsModified = tags is not null;
        if (linkedPr.HasValue)
        {
            effectiveTags = KeyedTag.AddValue(effectiveTags, KeyedTag.LinkedPrKey, linkedPr.Value.ToString());
            tagsModified = true;
        }

        var updated = new Issue
        {
            Id = newId,
            Title = title ?? existing.Title,
            TitleLastUpdate = title is not null ? now : existing.TitleLastUpdate,
            TitleModifiedBy = title is not null ? modifiedBy : existing.TitleModifiedBy,
            Description = description ?? existing.Description,
            DescriptionLastUpdate = description is not null ? now : existing.DescriptionLastUpdate,
            DescriptionModifiedBy = description is not null ? modifiedBy : existing.DescriptionModifiedBy,
            Status = status ?? existing.Status,
            StatusLastUpdate = status is not null ? now : existing.StatusLastUpdate,
            StatusModifiedBy = status is not null ? modifiedBy : existing.StatusModifiedBy,
            Type = type ?? existing.Type,
            TypeLastUpdate = type is not null ? now : existing.TypeLastUpdate,
            TypeModifiedBy = type is not null ? modifiedBy : existing.TypeModifiedBy,
            Priority = priority ?? existing.Priority,
            PriorityLastUpdate = priority is not null ? now : existing.PriorityLastUpdate,
            PriorityModifiedBy = priority is not null ? modifiedBy : existing.PriorityModifiedBy,
            LinkedIssues = linkedIssues ?? existing.LinkedIssues,
            LinkedIssuesLastUpdate = linkedIssues is not null ? now : existing.LinkedIssuesLastUpdate,
            LinkedIssuesModifiedBy = linkedIssues is not null ? modifiedBy : existing.LinkedIssuesModifiedBy,
            ParentIssues = parentIssues ?? existing.ParentIssues,
            ParentIssuesLastUpdate = parentIssues is not null ? now : existing.ParentIssuesLastUpdate,
            ParentIssuesModifiedBy = parentIssues is not null ? modifiedBy : existing.ParentIssuesModifiedBy,
            AssignedTo = assignedTo ?? existing.AssignedTo,
            AssignedToLastUpdate = assignedTo is not null ? now : existing.AssignedToLastUpdate,
            AssignedToModifiedBy = assignedTo is not null ? modifiedBy : existing.AssignedToModifiedBy,
            Tags = effectiveTags,
            TagsLastUpdate = tagsModified ? now : existing.TagsLastUpdate,
            TagsModifiedBy = tagsModified ? modifiedBy : existing.TagsModifiedBy,
            WorkingBranchId = workingBranchId ?? existing.WorkingBranchId,
            WorkingBranchIdLastUpdate = workingBranchId is not null ? now : existing.WorkingBranchIdLastUpdate,
            WorkingBranchIdModifiedBy = workingBranchId is not null ? modifiedBy : existing.WorkingBranchIdModifiedBy,
            ExecutionMode = executionMode ?? existing.ExecutionMode,
            ExecutionModeLastUpdate = executionMode is not null ? now : existing.ExecutionModeLastUpdate,
            ExecutionModeModifiedBy = executionMode is not null ? modifiedBy : existing.ExecutionModeModifiedBy,
            CreatedBy = existing.CreatedBy,
            CreatedByLastUpdate = existing.CreatedByLastUpdate,
            LastUpdate = now,
            CreatedAt = existing.CreatedAt
        };

        issues[existingIndex] = updated;
        await storage.SaveIssuesAsync(issues, cancellationToken);

        return updated;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var issues = (await LoadAndNormalizeAsync(cancellationToken)).ToList();
        var existingIndex = issues.FindIndex(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (existingIndex < 0)
        {
            return false;
        }

        var existing = issues[existingIndex];
        var now = DateTimeOffset.UtcNow;
        var modifiedBy = gitConfigService.GetUserName();
        var deleted = existing with
        {
            Status = IssueStatus.Deleted,
            StatusLastUpdate = now,
            StatusModifiedBy = modifiedBy,
            LastUpdate = now
        };

        issues[existingIndex] = deleted;
        await storage.SaveIssuesAsync(issues, cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<Issue>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var issues = await LoadAndNormalizeAsync(cancellationToken);

        // Check for key:value pattern (keyed tag search)
        // Pattern must have colon, no spaces, and content on both sides
        var colonIndex = query.IndexOf(':');
        if (colonIndex > 0 && colonIndex < query.Length - 1 && !query.Contains(' '))
        {
            var searchKey = query[..colonIndex];
            var searchValue = query[(colonIndex + 1)..];
            return issues.Where(i => tagService.HasKeyedTag(i, searchKey, searchValue)).ToList();
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
    /// Terminal statuses that are excluded from results by default (when includeTerminal is false).
    /// </summary>
    private static readonly IssueStatus[] TerminalStatuses =
        [IssueStatus.Complete, IssueStatus.Archived, IssueStatus.Closed, IssueStatus.Deleted];

    public async Task<IReadOnlyList<Issue>> FilterAsync(
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false,
        CancellationToken cancellationToken = default)
    {
        var issues = await LoadAndNormalizeAsync(cancellationToken);

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
    /// Matches a tag filter value against an issue's tags.
    /// If the filter contains '=', matches as an exact key=value keyed tag.
    /// Otherwise, matches as a simple tag (exact match) or as a key-only keyed tag match.
    /// </summary>
    private bool MatchesTag(Issue issue, string tagFilter)
    {
        var equalsIndex = tagFilter.IndexOf('=');
        if (equalsIndex > 0 && equalsIndex < tagFilter.Length - 1)
        {
            // key=value format: exact keyed tag match
            var key = tagFilter[..equalsIndex];
            var value = tagFilter[(equalsIndex + 1)..];
            return tagService.HasKeyedTag(issue, key, value);
        }

        // Key-only or simple tag: match exact simple tags OR any keyed tag with this key
        return (issue.Tags?.Contains(tagFilter, StringComparer.OrdinalIgnoreCase) ?? false)
            || tagService.HasTagKey(issue, tagFilter);
    }

    public async Task<Issue> UpdateQuestionsAsync(
        string id,
        IReadOnlyList<Question> questions,
        CancellationToken cancellationToken = default)
    {
        var issues = (await LoadAndNormalizeAsync(cancellationToken)).ToList();
        var existingIndex = issues.FindIndex(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (existingIndex < 0)
        {
            throw new KeyNotFoundException($"Issue with ID '{id}' not found.");
        }

        var existing = issues[existingIndex];
        var now = DateTimeOffset.UtcNow;
        var modifiedBy = gitConfigService.GetUserName();

        var updated = existing with
        {
            Questions = questions,
            QuestionsLastUpdate = now,
            QuestionsModifiedBy = modifiedBy,
            LastUpdate = now
        };

        issues[existingIndex] = updated;
        await storage.SaveIssuesAsync(issues, cancellationToken);

        return updated;
    }

    #region Issue Graph Methods

    /// <inheritdoc />
    public async Task<IssueGraph> BuildGraphAsync(CancellationToken cancellationToken = default)
    {
        var allIssues = await GetAllAsync(cancellationToken);
        return BuildGraphFromIssues(allIssues.ToList());
    }

    /// <inheritdoc />
    public async Task<IssueGraph> QueryGraphAsync(
        GraphQuery query,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default)
    {
        // Build full graph first
        var fullGraph = await BuildGraphAsync(cancellationToken);

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

    /// <inheritdoc />
    public async Task<IReadOnlyList<Issue>> GetNextIssuesAsync(
        string? parentId = null,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await BuildGraphAsync(cancellationToken);

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

        var issues = actionable.Select(n => n.Issue).ToList();
        ApplyGraphSort(issues, sortConfig ?? GraphSortConfig.Default);
        return issues;
    }

    /// <inheritdoc />
    public async Task<TaskGraph> BuildTaskGraphLayoutAsync(
        InactiveVisibility inactiveVisibility = InactiveVisibility.Hide,
        string? assignedTo = null,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await GetAllAsync(cancellationToken);
        var issueList = allIssues.ToList();

        if (issueList.Count == 0)
        {
            return new TaskGraph { Nodes = [], TotalLanes = 0 };
        }

        // Build a lookup for ALL issues (needed to find terminal parents)
        var fullLookup = issueList.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Filter issues based on parameters:
        // - Hide: exclude terminal statuses (default)
        // - IfHasActiveDescendants: exclude terminal statuses initially, then add back those with active descendants
        // - Always: include all statuses
        // - When assignedTo is provided, filter to only matching assignees
        var activeIssues = issueList.Where(i =>
            (inactiveVisibility == InactiveVisibility.Always || !i.Status.IsTerminal()) &&
            (assignedTo == null || string.Equals(i.AssignedTo, assignedTo, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        if (activeIssues.Count == 0)
        {
            return new TaskGraph { Nodes = [], TotalLanes = 0 };
        }

        // For IfHasActiveDescendants mode, find terminal issues that have active descendants
        if (inactiveVisibility == InactiveVisibility.IfHasActiveDescendants)
        {
            var terminalWithActiveDescendants = CollectTerminalIssuesWithActiveDescendants(activeIssues, fullLookup);
            activeIssues.AddRange(terminalWithActiveDescendants);
        }

        // Collect ancestors of active issues (even terminal ones) to provide hierarchy context
        var issuesToDisplay = CollectIssuesToDisplay(activeIssues, fullLookup);

        // Build lookup for display issues
        var issueLookup = issuesToDisplay.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Determine which issues are actionable
        var actionableIssues = await GetNextIssuesAsync(cancellationToken: cancellationToken);
        var actionableIds = new HashSet<string>(
            actionableIssues.Select(i => i.Id),
            StringComparer.OrdinalIgnoreCase);

        // Build children lookup: parentId -> sorted list of incomplete children
        var childrenOf = BuildChildrenLookup(issuesToDisplay, issueLookup);

        // Find root issues (no parent in the display set)
        var rootIssues = issuesToDisplay
            .Where(i => i.ParentIssues.Count == 0 ||
                        i.ParentIssues.All(p => !issueLookup.ContainsKey(p.ParentIssue)))
            .ToList();
        ApplyGraphSort(rootIssues, sortConfig ?? GraphSortConfig.Default);

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

        return new TaskGraph
        {
            Nodes = nodeList,
            TotalLanes = maxLane + 1
        };
    }

    /// <inheritdoc />
    public async Task<TaskGraph> BuildFilteredTaskGraphLayoutAsync(
        IReadOnlySet<string> matchedIds,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await GetAllAsync(cancellationToken);
        var issueList = allIssues.ToList();

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
        var actionableIssues = await GetNextIssuesAsync(cancellationToken: cancellationToken);
        var actionableIds = new HashSet<string>(
            actionableIssues.Select(i => i.Id),
            StringComparer.OrdinalIgnoreCase);

        // Build children lookup
        var childrenOf = BuildChildrenLookup(issuesToDisplay, issueLookup);

        // Find root issues (no parent in the display set)
        var rootIssues = issuesToDisplay
            .Where(i => i.ParentIssues.Count == 0 ||
                        i.ParentIssues.All(p => !issueLookup.ContainsKey(p.ParentIssue)))
            .ToList();
        ApplyGraphSort(rootIssues, sortConfig ?? GraphSortConfig.Default);

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

        return new TaskGraph
        {
            Nodes = nodeList,
            TotalLanes = maxLane + 1,
            MatchedIds = matchedIds
        };
    }

    #endregion

    #region Graph Building

    private IssueGraph BuildGraphFromIssues(List<Issue> issues)
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
        var issueLookup = issues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var childrenOf = BuildChildrenLookup(issues, issueLookup);

        // Compute Next/Previous for each issue (Series mode only)
        var nextPrevious = ComputeNextPrevious(issues, issueLookup, childrenOf);

        // Build nodes
        var nodes = new Dictionary<string, IssueGraphNode>(StringComparer.OrdinalIgnoreCase);
        var rootIds = new List<string>();

        foreach (var issue in issues)
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
    /// Computes Next/Previous relationships for issues.
    /// ONLY siblings under a Series execution mode parent have Next/Previous.
    /// Parallel mode siblings and root issues have empty lists.
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
        // For issues with multiple parents, return the first valid parent's mode
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

        // Include this parent (it has an active descendant)
        if (includedIds.Add(parentId))
        {
            // Recursively include grandparents
            foreach (var grandparentId in node.ParentIssueIds)
            {
                IncludeAncestorsWithActiveDescendants(grandparentId, graph, includedIds);
            }
        }
    }

    private static HashSet<string> GetDescendantIds(string parentId, IssueGraph graph)
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
    private static bool IsActionable(IssueGraphNode node)
    {
        var issue = node.Issue;

        // Ideas are never actionable - they represent future possibilities, not current work
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
    /// Returns the maximum lane used by the subtree.
    /// </summary>
    /// <param name="renderingParentIdForFirstLeaf">
    /// If provided, the first leaf node emitted by this subtree will use this ID
    /// as its RenderingParentId, overriding the normal parent connection.
    /// Used for cascading series flow.
    /// </param>
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
        // Skip issues already placed by a previous parent traversal (DAG support)
        if (!visited.Add(issue.Id))
        {
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

    /// <summary>
    /// Lays out children of a parallel parent. All children start at the same lane.
    /// </summary>
    /// <param name="renderingParentIdForFirstLeaf">
    /// If provided, the first leaf node emitted will use this ID as its RenderingParentId.
    /// </param>
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
            // Skip children already visited via another parent (DAG support)
            if (visited.Contains(child.Id))
            {
                continue;
            }

            var childIncomplete = GetIncompleteChildrenForLayout(child, childrenOf);

            // Determine rendering parent override for this child
            // Only the first child gets the override; parallel siblings connect normally
            string? childRenderingParent = isFirstChild ? renderingParentIdForFirstLeaf : null;

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
                    ParentExecutionMode = ExecutionMode.Parallel,
                    RenderingParentId = childRenderingParent
                });
            }
            else
            {
                // Subtree child — LayoutSubtree handles visited tracking
                var childMax = LayoutSubtree(child, startLane, nodeList, childrenOf, issueLookup, actionableIds, visited, parentExecutionMode: ExecutionMode.Parallel, renderingParentIdForFirstLeaf: childRenderingParent);
                maxChildLane = Math.Max(maxChildLane, childMax);
            }

            isFirstChild = false;
        }

        return maxChildLane;
    }

    /// <summary>
    /// Lays out children of a series parent. Children share the same lane,
    /// with subtrees pushing the current lane rightward.
    /// </summary>
    /// <param name="renderingParentIdForFirstLeaf">
    /// If provided, the first leaf node emitted will use this ID as its RenderingParentId.
    /// For subsequent siblings in series mode, we pass the previous sibling's ID as the
    /// rendering parent to create cascading connections.
    /// </param>
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

            // Skip children already visited via another parent (DAG support)
            if (visited.Contains(child.Id))
            {
                continue;
            }

            var childIncomplete = GetIncompleteChildrenForLayout(child, childrenOf);

            // Determine rendering parent override for this child's first leaf
            // First child uses the passed-in override (if any)
            // Subsequent children use the previous sibling's ID to create cascading flow
            string? childRenderingParent = isFirstChild ? renderingParentIdForFirstLeaf : previousSiblingId;

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
                    ParentExecutionMode = ExecutionMode.Series,
                    RenderingParentId = childRenderingParent
                });
            }
            else
            {
                // Subtree child
                // First non-skipped child starts at currentLane; subsequent start at currentLane + 1
                int subtreeStart = isFirstChild ? currentLane : currentLane + 1;
                var childMax = LayoutSubtree(child, subtreeStart, nodeList, childrenOf, issueLookup, actionableIds, visited, parentExecutionMode: ExecutionMode.Series, renderingParentIdForFirstLeaf: childRenderingParent);
                currentLane = childMax;
            }

            // Track this child as the previous sibling for cascading
            previousSiblingId = child.Id;
            isFirstChild = false;
        }

        return currentLane;
    }

    /// <summary>
    /// Gets the children of an issue that need to be traversed for layout purposes.
    /// </summary>
    private static List<Issue> GetIncompleteChildrenForLayout(Issue issue, Dictionary<string, List<Issue>> childrenOf)
    {
        if (!childrenOf.TryGetValue(issue.Id, out var children))
        {
            return [];
        }

        return children.Where(c => HasActiveDescendants(c, childrenOf)).ToList();
    }

    /// <summary>
    /// Checks if an issue has any active (non-done) descendants, including itself.
    /// </summary>
    private static bool HasActiveDescendants(Issue issue, Dictionary<string, List<Issue>> childrenOf)
    {
        // If this issue is not done, it counts as active
        if (!issue.Status.IsDone())
        {
            return true;
        }

        // Otherwise, check if any children have active descendants
        if (!childrenOf.TryGetValue(issue.Id, out var children))
        {
            return false;
        }

        return children.Any(c => HasActiveDescendants(c, childrenOf));
    }

    /// <summary>
    /// Loads issues from storage and normalizes SortOrder on all ParentIssueRefs.
    /// </summary>
    private async Task<IReadOnlyList<Issue>> LoadAndNormalizeAsync(CancellationToken cancellationToken)
    {
        var issues = await storage.LoadIssuesAsync(cancellationToken);
        return NormalizeSortOrders(issues);
    }

    /// <summary>
    /// Ensures every ParentIssueRef has a non-null SortOrder.
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

    /// <summary>
    /// Builds a lookup from parent ID to sorted list of children.
    /// </summary>
    private static Dictionary<string, List<Issue>> BuildChildrenLookup(
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
    /// Applies a configurable sort to a list of issues in-place using the provided sort rules.
    /// </summary>
    private static void ApplyGraphSort(List<Issue> issues, GraphSortConfig config)
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
    /// Determines if the first actionable issue in a subtree has a description.
    /// </summary>
    private static bool FirstActionableIssueHasDescription(
        Issue root,
        Dictionary<string, List<Issue>> childrenOf,
        HashSet<string> actionableIds)
    {
        var firstActionable = FindFirstActionableInSubtree(root, childrenOf, actionableIds);
        return firstActionable is not null && !string.IsNullOrEmpty(firstActionable.Description);
    }

    /// <summary>
    /// Recursively finds the first actionable issue in a subtree.
    /// </summary>
    private static Issue? FindFirstActionableInSubtree(
        Issue issue,
        Dictionary<string, List<Issue>> childrenOf,
        HashSet<string> actionableIds)
    {
        if (actionableIds.Contains(issue.Id))
        {
            return issue;
        }

        if (!childrenOf.TryGetValue(issue.Id, out var children))
        {
            return null;
        }

        foreach (var child in children)
        {
            var found = FindFirstActionableInSubtree(child, childrenOf, actionableIds);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Collects the set of issues to display in the task graph.
    /// Starts with non-terminal issues and adds all ancestor issues (even terminal ones).
    /// </summary>
    private static List<Issue> CollectIssuesToDisplay(
        List<Issue> activeIssues,
        Dictionary<string, Issue> fullLookup)
    {
        var displayIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Start with all active (non-terminal) issues
        foreach (var issue in activeIssues)
        {
            displayIds.Add(issue.Id);
        }

        // Walk up parent chains to collect ancestors (even terminal ones)
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

        // Return issues in a consistent order
        var result = new List<Issue>();
        var addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in activeIssues)
        {
            result.Add(issue);
            addedIds.Add(issue.Id);
        }

        // Add terminal ancestors that weren't already in the active set
        foreach (var id in displayIds)
        {
            if (!addedIds.Contains(id) && fullLookup.TryGetValue(id, out var issue) && issue.Status.IsTerminal())
            {
                result.Add(issue);
            }
        }

        return result;
    }

    /// <summary>
    /// Finds terminal issues that have at least one active (non-terminal) descendant at any depth.
    /// Builds a children lookup from all issues, then for each terminal issue checks if any
    /// descendant is in the active set.
    /// </summary>
    private static List<Issue> CollectTerminalIssuesWithActiveDescendants(
        List<Issue> activeIssues,
        Dictionary<string, Issue> fullLookup)
    {
        var activeIds = new HashSet<string>(activeIssues.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);

        // Build children lookup: parentId -> list of child issue IDs
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

        // For each terminal issue, check if it has any active descendant
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

    /// <summary>
    /// Recursively checks whether an issue has any active descendant.
    /// Results are memoized in the checkedIds dictionary.
    /// </summary>
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

        // Mark as false first to handle cycles
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
}
