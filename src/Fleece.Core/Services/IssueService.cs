using System.Text.RegularExpressions;
using Fleece.Core.FunctionalCore;
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
            var descendants = Issues.GetDescendantIds(issueId, graph);
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
        var issues = await LoadAndNormalizeAsync(cancellationToken);
        return Issues.Search(issues, query);
    }

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
        return Issues.Filter(issues, status, type, priority, assignedTo, tags, linkedPr, includeTerminal);
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
        return Issues.BuildGraph(allIssues);
    }

    /// <inheritdoc />
    public async Task<IssueGraph> QueryGraphAsync(
        GraphQuery query,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await GetAllAsync(cancellationToken);
        return Issues.QueryGraph(allIssues, query, sortConfig);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Issue>> GetNextIssuesAsync(
        string? parentId = null,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await GetAllAsync(cancellationToken);
        return Issues.GetNextIssues(allIssues, parentId, sortConfig);
    }

    /// <inheritdoc />
    public async Task<TaskGraph> BuildTaskGraphLayoutAsync(
        InactiveVisibility inactiveVisibility = InactiveVisibility.Hide,
        string? assignedTo = null,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await GetAllAsync(cancellationToken);
        return Issues.BuildTaskGraphLayout(allIssues, inactiveVisibility, assignedTo, sortConfig);
    }

    /// <inheritdoc />
    public async Task<TaskGraph> BuildFilteredTaskGraphLayoutAsync(
        IReadOnlySet<string> matchedIds,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await GetAllAsync(cancellationToken);
        return Issues.BuildFilteredTaskGraphLayout(allIssues, matchedIds, sortConfig);
    }

    #endregion

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
}
