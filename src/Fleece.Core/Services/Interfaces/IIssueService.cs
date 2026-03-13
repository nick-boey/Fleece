using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

public interface IIssueService
{
    // --- Issue Graph Methods ---

    /// <summary>
    /// Builds a complete issue graph from all issues.
    /// The graph includes parent-child and next/previous relationships.
    /// Next/Previous are only computed for siblings under Series execution mode parents.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A complete issue graph.</returns>
    Task<IssueGraph> BuildGraphAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a filtered subgraph based on the query parameters.
    ///
    /// IMPORTANT: Next/Previous relationships are computed from the FULL graph,
    /// then the subgraph is filtered. This means relationships reflect the actual
    /// execution order in the full hierarchy.
    /// </summary>
    /// <param name="query">Query parameters for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A filtered subgraph.</returns>
    Task<IssueGraph> QueryGraphAsync(
        GraphQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all issues that are currently actionable (can be worked on next).
    /// An issue is actionable if:
    /// - It has Open or Review status
    /// - All its PreviousIssueIds are done (Complete, Archived, or Closed)
    /// - It has no incomplete children
    /// - Its parent's ExecutionMode allows it (for Series, it must be the first incomplete child)
    ///
    /// Results are sorted by: Review status > description presence > priority > title.
    /// </summary>
    /// <param name="parentId">Optional parent ID to filter results to descendants of that parent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of actionable issues.</returns>
    Task<IReadOnlyList<Issue>> GetNextIssuesAsync(
        string? parentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a positioned task graph layout for rendering.
    /// The graph is organized bottom-up, with actionable tasks at the left (lane 0)
    /// and root/parent tasks at the right (higher lanes).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A positioned task graph ready for rendering.</returns>
    Task<TaskGraph> BuildTaskGraphLayoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a positioned task graph layout filtered by the given matched issue IDs.
    /// Includes matched issues and their ancestor issues for context.
    /// The resulting graph includes a set of matched IDs for highlighting.
    /// </summary>
    /// <param name="matchedIds">IDs of the issues that matched the search.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A positioned task graph with matched IDs for highlighting.</returns>
    Task<TaskGraph> BuildFilteredTaskGraphLayoutAsync(
        IReadOnlySet<string> matchedIds,
        CancellationToken cancellationToken = default);

    // --- CRUD Methods ---

    /// <summary>
    /// Creates a new issue.
    /// </summary>
    /// <param name="title">The issue title (required).</param>
    /// <param name="type">The issue type (required).</param>
    /// <param name="description">Optional issue description.</param>
    /// <param name="status">Initial status (default: Open).</param>
    /// <param name="priority">Optional priority (1-5, 1=highest).</param>
    /// <param name="linkedPr">
    /// Deprecated: Use tags with hsp-linked-pr key instead.
    /// If provided, this value will be converted to an hsp-linked-pr tag.
    /// </param>
    /// <param name="linkedIssues">Optional list of linked issue IDs.</param>
    /// <param name="parentIssues">Optional list of parent issue references.</param>
    /// <param name="assignedTo">Optional assignee username.</param>
    /// <param name="tags">Optional list of tags. Use hsp-linked-pr=N tags for linked PRs.</param>
    /// <param name="workingBranchId">Optional working branch ID.</param>
    /// <param name="executionMode">Optional execution mode for child issues.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created issue.</returns>
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

    Task<IReadOnlyList<Issue>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Issue?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a partial ID (3+ characters) to matching issues.
    /// Returns issues whose ID starts with the given partial ID (case-insensitive).
    /// If the partial ID is less than 3 characters, requires exact match.
    /// </summary>
    Task<IReadOnlyList<Issue>> ResolveByPartialIdAsync(string partialId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing issue.
    /// </summary>
    /// <param name="linkedPr">
    /// Deprecated: Use tags with hsp-linked-pr key instead.
    /// If provided, this value will be converted to an hsp-linked-pr tag.
    /// </param>
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

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Issue>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Filters issues by various criteria.
    /// </summary>
    /// <param name="linkedPr">
    /// Deprecated: Use tags filter with hsp-linked-pr=N value instead.
    /// If provided, will filter issues that have this PR number in their hsp-linked-pr tags.
    /// </param>
    Task<IReadOnlyList<Issue>> FilterAsync(
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false,
        IReadOnlyList<(string Key, string Value)>? keyedTags = null,
        CancellationToken cancellationToken = default);

    Task<Issue> UpdateQuestionsAsync(
        string id,
        IReadOnlyList<Question> questions,
        CancellationToken cancellationToken = default);
}
