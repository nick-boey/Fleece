using System.Text.RegularExpressions;
using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Search;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;
using Fleece.Core.Utilities;

namespace Fleece.Core.Services;

/// <summary>
/// Unified service that consolidates all Fleece operations.
/// Loads from storage, delegates to static FunctionalCore classes, and persists writes.
/// Thread-safe via internal SemaphoreSlim.
/// </summary>
public sealed partial class FleeceService : IFleeceService
{
    private readonly IStorageService _storage;
    private readonly IIdGenerator _idGenerator;
    private readonly IGitConfigService _gitConfigService;
    private readonly ISettingsService _settingsService;
    private readonly SyncStatusService? _syncStatusService;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Regex patterns for Git branch name validation (shared with IssueService)
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9_\-/.]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$")]
    private static partial Regex ValidBranchNamePattern();

    [GeneratedRegex(@"[~^:\?\*\[\]@{}\s\\]|\.\.|\.$|^\.|\.lock$|//$|^/|/$")]
    private static partial Regex InvalidBranchNamePattern();

    internal FleeceService(
        IStorageService storage,
        IIdGenerator idGenerator,
        IGitConfigService gitConfigService,
        ISettingsService settingsService,
        SyncStatusService? syncStatusService = null)
    {
        _storage = storage;
        _idGenerator = idGenerator;
        _gitConfigService = gitConfigService;
        _settingsService = settingsService;
        _syncStatusService = syncStatusService;
    }

    /// <summary>
    /// Creates a FleeceService configured for a specific file path (single-file mode).
    /// </summary>
    public static FleeceService ForFile(
        string filePath,
        ISettingsService settingsService,
        IGitConfigService gitConfigService,
        System.IO.Abstractions.IFileSystem? fileSystem = null)
    {
        var fs = fileSystem ?? new Testably.Abstractions.RealFileSystem();
        var serializer = new JsonlSerializer();
        var schemaValidator = new SchemaValidator();
        var storage = new SingleFileStorageService(filePath, serializer, schemaValidator, fs);
        var idGenerator = new GuidIdGenerator();
        return new FleeceService(storage, idGenerator, gitConfigService, settingsService);
    }

    #region Issue CRUD

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
        ValidateBranchName(workingBranchId);
        ValidateTags(tags);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var existingIssues = await _storage.LoadIssuesAsync(cancellationToken);
            var tombstones = await _storage.LoadTombstonesAsync(cancellationToken);
            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in existingIssues)
            {
                usedIds.Add(existing.Id);
            }

            foreach (var tombstone in tombstones)
            {
                usedIds.Add(tombstone.IssueId);
            }

            var id = _idGenerator.Generate();
            const int maxRetries = 10;
            for (var attempt = 1; attempt <= maxRetries && usedIds.Contains(id); attempt++)
            {
                id = _idGenerator.Generate();
            }

            if (usedIds.Contains(id))
            {
                throw new InvalidOperationException(
                    "Cannot generate a unique ID. All attempts collide with existing or tombstoned issue IDs.");
            }

            var now = DateTimeOffset.UtcNow;
            var createdBy = _gitConfigService.GetUserName();

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
                Description = description,
                Status = status,
                Type = type,
                Priority = priority,
                LinkedIssues = linkedIssues ?? [],
                ParentIssues = (parentIssues ?? []).Select(p => p with { Active = true }).ToList(),
                AssignedTo = assignedTo,
                Tags = effectiveTags,
                WorkingBranchId = workingBranchId,
                ExecutionMode = executionMode ?? ExecutionMode.Series,
                CreatedBy = createdBy,
                LastUpdate = now,
                CreatedAt = now
            };

            await _storage.AppendIssueAsync(issue, cancellationToken);
            return issue;
        }
        finally
        {
            _lock.Release();
        }
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
        ValidateBranchName(workingBranchId);
        ValidateTags(tags);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var issues = (await LoadAndNormalizeAsync(cancellationToken)).ToList();
            var existingIndex = issues.FindIndex(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingIndex < 0)
            {
                throw new KeyNotFoundException($"Issue with ID '{id}' not found.");
            }

            var existing = issues[existingIndex];
            var now = DateTimeOffset.UtcNow;

            // Determine effective tags - apply linkedPr if provided (deprecated parameter)
            var effectiveTags = tags ?? existing.Tags;
            if (linkedPr.HasValue)
            {
                effectiveTags = KeyedTag.AddValue(effectiveTags, KeyedTag.LinkedPrKey, linkedPr.Value.ToString());
            }

            var updated = new Issue
            {
                Id = existing.Id,
                Title = title ?? existing.Title,
                Description = description ?? existing.Description,
                Status = status ?? existing.Status,
                Type = type ?? existing.Type,
                Priority = priority ?? existing.Priority,
                LinkedIssues = linkedIssues ?? existing.LinkedIssues,
                ParentIssues = parentIssues is not null
                    ? parentIssues.Select(p => p with { Active = true }).ToList()
                    : existing.ParentIssues,
                AssignedTo = assignedTo ?? existing.AssignedTo,
                Tags = effectiveTags,
                WorkingBranchId = workingBranchId ?? existing.WorkingBranchId,
                ExecutionMode = executionMode ?? existing.ExecutionMode,
                CreatedBy = existing.CreatedBy,
                LastUpdate = now,
                CreatedAt = existing.CreatedAt
            };

            issues[existingIndex] = updated;
            await _storage.SaveIssuesAsync(issues, cancellationToken);
            return updated;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var issues = (await LoadAndNormalizeAsync(cancellationToken)).ToList();
            var existingIndex = issues.FindIndex(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingIndex < 0)
            {
                return false;
            }

            var existing = issues[existingIndex];
            var now = DateTimeOffset.UtcNow;
            var deleted = existing with
            {
                Status = IssueStatus.Deleted,
                LastUpdate = now
            };

            issues[existingIndex] = deleted;
            await _storage.SaveIssuesAsync(issues, cancellationToken);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Issue?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var issues = await LoadAndNormalizeAsync(cancellationToken);
        return issues.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<Issue>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await LoadAndNormalizeAsync(cancellationToken);
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

    #endregion

    #region Filtering & Search

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

    public async Task<IReadOnlyList<Issue>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var issues = await LoadAndNormalizeAsync(cancellationToken);
        return Issues.Search(issues, query);
    }

    public SearchQuery ParseSearchQuery(string? query) => SearchOps.ParseQuery(query);

    public async Task<IReadOnlyList<Issue>> SearchWithFiltersAsync(
        SearchQuery query,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await LoadAndNormalizeAsync(cancellationToken);
        return SearchOps.SearchWithFilters(allIssues, query, status, type, priority, assignedTo, tags, linkedPr, includeTerminal);
    }

    public async Task<SearchResult> SearchWithContextAsync(
        SearchQuery query,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await LoadAndNormalizeAsync(cancellationToken);
        return SearchOps.SearchWithContext(allIssues, query, status, type, priority, assignedTo, tags, linkedPr, includeTerminal);
    }

    #endregion

    #region Graph Operations

    public async Task<IssueGraph> BuildGraphAsync(CancellationToken cancellationToken = default)
    {
        var allIssues = await LoadAndNormalizeAsync(cancellationToken);
        return Issues.BuildGraph(allIssues);
    }

    public async Task<IssueGraph> QueryGraphAsync(
        GraphQuery query,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await LoadAndNormalizeAsync(cancellationToken);
        return Issues.QueryGraph(allIssues, query, sortConfig);
    }

    public async Task<IReadOnlyList<Issue>> GetNextIssuesAsync(
        string? parentId = null,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await LoadAndNormalizeAsync(cancellationToken);
        return Issues.GetNextIssues(allIssues, parentId, sortConfig);
    }

    #endregion

    #region Dependencies

    public async Task<Issue> AddDependencyAsync(
        string parentId,
        string childId,
        DependencyPosition? position = null,
        bool replaceExisting = false,
        bool makePrimary = false,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var allIssues = (await LoadAndNormalizeAsync(cancellationToken)).ToList();

            var resolvedParent = ResolveIssue(allIssues, parentId, "parent");
            var resolvedChild = ResolveIssue(allIssues, childId, "child");

            // Check for circular dependency
            if (Validation.WouldCreateCycle(allIssues, resolvedParent.Id, resolvedChild.Id))
            {
                throw new InvalidOperationException(
                    $"Adding '{resolvedParent.Id}' as a parent of '{resolvedChild.Id}' would create a circular dependency");
            }

            var updated = Dependencies.AddDependency(
                resolvedChild, resolvedParent.Id, allIssues, position, replaceExisting, makePrimary);

            // Persist via full save
            var childIndex = allIssues.FindIndex(i => i.Id.Equals(resolvedChild.Id, StringComparison.OrdinalIgnoreCase));
            var now = DateTimeOffset.UtcNow;

            var persisted = allIssues[childIndex] with
            {
                ParentIssues = updated.ParentIssues,
                LastUpdate = now
            };
            allIssues[childIndex] = persisted;
            await _storage.SaveIssuesAsync(allIssues, cancellationToken);
            return persisted;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Issue> RemoveDependencyAsync(
        string parentId,
        string childId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var allIssues = (await LoadAndNormalizeAsync(cancellationToken)).ToList();

            var resolvedParent = ResolveIssue(allIssues, parentId, "parent");
            var resolvedChild = ResolveIssue(allIssues, childId, "child");

            var updated = Dependencies.RemoveDependency(resolvedChild, resolvedParent.Id);

            // Persist
            var childIndex = allIssues.FindIndex(i => i.Id.Equals(resolvedChild.Id, StringComparison.OrdinalIgnoreCase));
            var now = DateTimeOffset.UtcNow;

            var persisted = allIssues[childIndex] with
            {
                ParentIssues = updated.ParentIssues,
                LastUpdate = now
            };
            allIssues[childIndex] = persisted;
            await _storage.SaveIssuesAsync(allIssues, cancellationToken);
            return persisted;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<MoveResult> MoveUpAsync(
        string parentId,
        string childId,
        CancellationToken cancellationToken = default)
    {
        return await MoveAsync(parentId, childId, MoveDirection.Up, cancellationToken);
    }

    public async Task<MoveResult> MoveDownAsync(
        string parentId,
        string childId,
        CancellationToken cancellationToken = default)
    {
        return await MoveAsync(parentId, childId, MoveDirection.Down, cancellationToken);
    }

    private async Task<MoveResult> MoveAsync(
        string parentId,
        string childId,
        MoveDirection direction,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var allIssues = (await LoadAndNormalizeAsync(cancellationToken)).ToList();

            var resolvedParent = ResolveIssue(allIssues, parentId, "parent");
            var resolvedChild = ResolveIssue(allIssues, childId, "child");

            // Check if child is actually under this parent
            var isChild = resolvedChild.ActiveParentIssues.Any(p =>
                string.Equals(p.ParentIssue, resolvedParent.Id, StringComparison.OrdinalIgnoreCase));

            if (!isChild)
            {
                return new MoveResult
                {
                    Outcome = MoveOutcome.Invalid,
                    Reason = MoveInvalidReason.NotAChildOfParent,
                    Message = $"Issue '{resolvedChild.Id}' is not a child of '{resolvedParent.Id}'"
                };
            }

            // Check boundary conditions
            var siblings = Dependencies.GetSortedSiblings(resolvedParent.Id, allIssues);
            var childIndex = siblings.FindIndex(s =>
                string.Equals(s.Issue.Id, resolvedChild.Id, StringComparison.OrdinalIgnoreCase));

            if (direction == MoveDirection.Up && childIndex == 0)
            {
                return new MoveResult
                {
                    Outcome = MoveOutcome.Invalid,
                    Reason = MoveInvalidReason.AlreadyAtTop,
                    Message = $"Issue '{resolvedChild.Id}' is already at the top"
                };
            }

            if (direction == MoveDirection.Down && childIndex == siblings.Count - 1)
            {
                return new MoveResult
                {
                    Outcome = MoveOutcome.Invalid,
                    Reason = MoveInvalidReason.AlreadyAtBottom,
                    Message = $"Issue '{resolvedChild.Id}' is already at the bottom"
                };
            }

            // Delegate to pure function
            var (movedIssue, modifiedSiblings) = direction == MoveDirection.Up
                ? Dependencies.MoveUp(resolvedChild, resolvedParent.Id, allIssues)
                : Dependencies.MoveDown(resolvedChild, resolvedParent.Id, allIssues);

            var now = DateTimeOffset.UtcNow;

            // Apply normalized sibling changes to allIssues
            foreach (var sibling in modifiedSiblings)
            {
                var sibIndex = allIssues.FindIndex(i => i.Id.Equals(sibling.Id, StringComparison.OrdinalIgnoreCase));
                if (sibIndex >= 0)
                {
                    allIssues[sibIndex] = allIssues[sibIndex] with
                    {
                        ParentIssues = sibling.ParentIssues,
                        LastUpdate = now
                    };
                }
            }

            // Apply the moved issue
            var movedIndex = allIssues.FindIndex(i => i.Id.Equals(movedIssue.Id, StringComparison.OrdinalIgnoreCase));
            if (movedIndex >= 0)
            {
                allIssues[movedIndex] = allIssues[movedIndex] with
                {
                    ParentIssues = movedIssue.ParentIssues,
                    LastUpdate = now
                };
            }

            await _storage.SaveIssuesAsync(allIssues, cancellationToken);

            return new MoveResult
            {
                Outcome = direction == MoveDirection.Up ? MoveOutcome.MovedUp : MoveOutcome.MovedDown,
                UpdatedIssue = allIssues[movedIndex]
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion

    #region Maintenance

    public async Task<CleanResult> CleanAsync(
        bool includeComplete = false,
        bool includeClosed = false,
        bool includeArchived = false,
        bool stripReferences = true,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var issues = (await _storage.LoadIssuesAsync(cancellationToken)).ToList();
            var existingTombstones = await _storage.LoadTombstonesAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var cleanedBy = _gitConfigService.GetUserName() ?? "unknown";

            var plan = Cleaning.Plan(
                issues,
                existingTombstones.ToList(),
                includeComplete,
                includeClosed,
                includeArchived,
                stripReferences,
                now,
                cleanedBy);

            if (plan.IssuesToRemove.Count == 0)
            {
                return new CleanResult
                {
                    CleanedTombstones = [],
                    StrippedReferences = []
                };
            }

            if (!dryRun)
            {
                await _storage.SaveIssuesAsync(plan.UpdatedIssues.ToList(), cancellationToken);

                var allTombstones = existingTombstones.ToList();
                allTombstones.AddRange(plan.TombstonesToCreate);

                var deduplicatedTombstones = allTombstones
                    .GroupBy(t => t.IssueId, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(t => t.CleanedAt).First())
                    .ToList();

                await _storage.SaveTombstonesAsync(deduplicatedTombstones, cancellationToken);
            }

            return new CleanResult
            {
                CleanedTombstones = plan.TombstonesToCreate.ToList(),
                StrippedReferences = plan.StrippedReferences.ToList()
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<int> MergeAsync(bool dryRun = false, CancellationToken cancellationToken = default)
    {
        // Event-sourced storage merges via event replay — IssueMerger is now legacy-only.
        return Task.FromResult(0);
    }

    public async Task<DependencyValidationResult> ValidateDependenciesAsync(CancellationToken cancellationToken = default)
    {
        var issues = await LoadAndNormalizeAsync(cancellationToken);
        return Validation.ValidateDependencyCycles(issues);
    }

    public async Task<(bool HasMultiple, string Message)> HasMultipleUnmergedFilesAsync(CancellationToken cancellationToken = default)
    {
        return await _storage.HasMultipleUnmergedFilesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Issue>> GetIssueHierarchyAsync(
        string issueId,
        bool includeParents = true,
        bool includeChildren = true,
        CancellationToken cancellationToken = default)
    {
        var graph = await BuildGraphAsync(cancellationToken);

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


    #endregion

    #region Diagnostics

    public async Task<LoadIssuesResult> LoadIssuesWithDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        return await _storage.LoadIssuesWithDiagnosticsAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, SyncStatus>> GetSyncStatusesAsync(CancellationToken cancellationToken = default)
    {
        if (_syncStatusService is null)
        {
            return new Dictionary<string, SyncStatus>();
        }

        return await _syncStatusService.GetSyncStatusesAsync(cancellationToken);
    }

    #endregion

    #region Private Helpers

    private async Task<IReadOnlyList<Issue>> LoadAndNormalizeAsync(CancellationToken cancellationToken)
    {
        var issues = await _storage.LoadIssuesAsync(cancellationToken);
        return Issues.NormalizeSortOrders(issues);
    }

    private static void ValidateBranchName(string? workingBranchId)
    {
        if (!IsValidGitBranchName(workingBranchId))
        {
            throw new ArgumentException(
                "Working branch ID contains invalid characters for a Git branch name.",
                nameof(workingBranchId));
        }
    }

    private static void ValidateTags(IReadOnlyList<string>? tags)
    {
        if (tags is not null)
        {
            var tagErrors = Tags.ValidateTags(tags);
            if (tagErrors.Count > 0)
            {
                throw new ArgumentException($"Invalid tags: {string.Join("; ", tagErrors)}", nameof(tags));
            }
        }
    }

    internal static bool IsValidGitBranchName(string? branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return true;
        }

        if (InvalidBranchNamePattern().IsMatch(branchName))
        {
            return false;
        }

        return ValidBranchNamePattern().IsMatch(branchName);
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

    private static Issue ResolveIssue(IReadOnlyList<Issue> allIssues, string partialId, string role)
    {
        List<Issue> matches;

        if (string.IsNullOrWhiteSpace(partialId))
        {
            matches = [];
        }
        else if (partialId.Length < 3)
        {
            var exactMatch = allIssues.FirstOrDefault(i => i.Id.Equals(partialId, StringComparison.OrdinalIgnoreCase));
            matches = exactMatch is not null ? [exactMatch] : [];
        }
        else
        {
            matches = allIssues
                .Where(i => i.Id.StartsWith(partialId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return matches.Count switch
        {
            0 => throw new KeyNotFoundException($"No {role} issue found matching '{partialId}'"),
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Multiple issues match '{partialId}': {string.Join(", ", matches.Select(m => m.Id))}")
        };
    }

    private enum MoveDirection { Up, Down }

    #endregion
}
