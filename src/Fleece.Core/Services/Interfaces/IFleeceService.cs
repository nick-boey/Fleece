using Fleece.Core.Models;
using Fleece.Core.Search;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Unified service interface that consolidates all Fleece operations:
/// CRUD, filtering, search, graph, dependencies, maintenance, and diagnostics.
/// </summary>
public interface IFleeceService
{
    #region Issue CRUD

    /// <summary>
    /// Creates a new issue, handling ID collision retry against tombstones.
    /// </summary>
    Task<Issue> CreateAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing issue's properties. Only non-null parameters are applied.
    /// </summary>
    Task<Issue> UpdateAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes an issue by setting its status to Deleted.
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single issue by its full ID.
    /// </summary>
    Task<Issue?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all issues from storage.
    /// </summary>
    Task<IReadOnlyList<Issue>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves issues by partial ID (3+ characters prefix match).
    /// </summary>
    Task<IReadOnlyList<Issue>> ResolveByPartialIdAsync(string partialId, CancellationToken cancellationToken = default);

    #endregion

    #region Filtering & Search

    /// <summary>
    /// Filters issues by status, type, priority, assigned user, tags, linked PR, and terminal inclusion.
    /// </summary>
    Task<IReadOnlyList<Issue>> FilterAsync(
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches issues by a text query string.
    /// </summary>
    Task<IReadOnlyList<Issue>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a search query string into a structured SearchQuery.
    /// </summary>
    SearchQuery ParseSearchQuery(string? query);

    /// <summary>
    /// Searches issues using a parsed query with CLI filter overrides.
    /// CLI filters take precedence over query filters for the same field.
    /// </summary>
    Task<IReadOnlyList<Issue>> SearchWithFiltersAsync(
        SearchQuery query,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches issues with full hierarchy context, returning matched issues and ancestor context.
    /// </summary>
    Task<SearchResult> SearchWithContextAsync(
        SearchQuery query,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false,
        CancellationToken cancellationToken = default);

    #endregion

    #region Graph Operations

    /// <summary>
    /// Builds the full issue dependency graph.
    /// </summary>
    Task<IssueGraph> BuildGraphAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries a filtered subgraph based on the provided query criteria.
    /// </summary>
    Task<IssueGraph> QueryGraphAsync(
        GraphQuery query,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a task graph layout for display with lane assignments.
    /// </summary>
    Task<TaskGraph> BuildTaskGraphLayoutAsync(
        InactiveVisibility inactiveVisibility = InactiveVisibility.Hide,
        string? assignedTo = null,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a task graph layout filtered to highlight search result matches.
    /// </summary>
    Task<TaskGraph> BuildFilteredTaskGraphLayoutAsync(
        IReadOnlySet<string> matchedIds,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next actionable issues based on dependency ordering.
    /// </summary>
    Task<IReadOnlyList<Issue>> GetNextIssuesAsync(
        string? parentId = null,
        GraphSortConfig? sortConfig = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Dependencies

    /// <summary>
    /// Adds a parent-child dependency relationship.
    /// </summary>
    Task<Issue> AddDependencyAsync(
        string parentId,
        string childId,
        DependencyPosition? position = null,
        bool replaceExisting = false,
        bool makePrimary = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a parent-child dependency relationship.
    /// </summary>
    Task<Issue> RemoveDependencyAsync(
        string parentId,
        string childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a child issue up in its parent's ordering.
    /// </summary>
    Task<MoveResult> MoveUpAsync(
        string parentId,
        string childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a child issue down in its parent's ordering.
    /// </summary>
    Task<MoveResult> MoveDownAsync(
        string parentId,
        string childId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Maintenance

    /// <summary>
    /// Cleans deleted issues, creating tombstones and optionally stripping dangling references.
    /// </summary>
    Task<CleanResult> CleanAsync(
        bool includeComplete = false,
        bool includeClosed = false,
        bool includeArchived = false,
        bool stripReferences = true,
        bool dryRun = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges duplicate issue files, resolving conflicts. Returns the number of duplicates found.
    /// </summary>
    Task<int> MergeAsync(bool dryRun = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs migrations on issue data (timestamp addition, linkedPR-to-tags, unknown property cleanup).
    /// </summary>
    Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether any migrations are needed.
    /// </summary>
    Task<bool> IsMigrationNeededAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that there are no cyclic dependencies in the issue graph.
    /// </summary>
    Task<DependencyValidationResult> ValidateDependenciesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether there are multiple unmerged issue files.
    /// </summary>
    Task<(bool HasMultiple, string Message)> HasMultipleUnmergedFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the hierarchy of an issue, optionally including parents and/or children.
    /// </summary>
    Task<IReadOnlyList<Issue>> GetIssueHierarchyAsync(
        string issueId,
        bool includeParents = true,
        bool includeChildren = true,
        CancellationToken cancellationToken = default);

    #endregion

    #region Diagnostics

    /// <summary>
    /// Loads issues with schema validation diagnostics.
    /// </summary>
    Task<LoadIssuesResult> LoadIssuesWithDiagnosticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the sync status for all issues by comparing working directory,
    /// HEAD commit, and remote upstream.
    /// </summary>
    Task<IReadOnlyDictionary<string, SyncStatus>> GetSyncStatusesAsync(CancellationToken cancellationToken = default);

    #endregion
}
