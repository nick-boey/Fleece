using Fleece.Core.Models;

namespace Fleece.Core.Search;

/// <summary>
/// Service for searching issues using a query DSL.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Parses a search query string into a SearchQuery.
    /// </summary>
    /// <param name="query">The query string to parse.</param>
    /// <returns>The parsed SearchQuery.</returns>
    SearchQuery ParseQuery(string? query);

    /// <summary>
    /// Searches for issues matching the given query.
    /// </summary>
    /// <param name="query">The parsed search query.</param>
    /// <param name="includeTerminal">Whether to include terminal statuses (complete, archived, closed, deleted).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching issues.</returns>
    Task<IReadOnlyList<Issue>> SearchAsync(
        SearchQuery query,
        bool includeTerminal = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for issues matching the given query, with CLI filter overrides.
    /// CLI filters take precedence over query filters for the same field.
    /// </summary>
    /// <param name="query">The parsed search query.</param>
    /// <param name="status">CLI status filter override (null = use query).</param>
    /// <param name="type">CLI type filter override (null = use query).</param>
    /// <param name="priority">CLI priority filter override (null = use query).</param>
    /// <param name="assignedTo">CLI assigned filter override (null = use query).</param>
    /// <param name="tags">CLI tags filter override (null = use query).</param>
    /// <param name="linkedPr">CLI linked PR filter override (null = use query).</param>
    /// <param name="includeTerminal">Whether to include terminal statuses.</param>
    /// <param name="keyedTags">Keyed tag filters (key=value pairs).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching issues.</returns>
    Task<IReadOnlyList<Issue>> SearchWithFiltersAsync(
        SearchQuery query,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false,
        IReadOnlyList<(string Key, string Value)>? keyedTags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for issues with full hierarchy context.
    /// Returns both matched issues and their ancestor issues for tree display.
    /// </summary>
    /// <param name="query">The parsed search query.</param>
    /// <param name="status">CLI status filter override.</param>
    /// <param name="type">CLI type filter override.</param>
    /// <param name="priority">CLI priority filter override.</param>
    /// <param name="assignedTo">CLI assigned filter override.</param>
    /// <param name="tags">CLI tags filter override.</param>
    /// <param name="linkedPr">CLI linked PR filter override.</param>
    /// <param name="includeTerminal">Whether to include terminal statuses.</param>
    /// <param name="keyedTags">Keyed tag filters (key=value pairs).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search result with matched and context issues.</returns>
    Task<SearchResult> SearchWithContextAsync(
        SearchQuery query,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false,
        IReadOnlyList<(string Key, string Value)>? keyedTags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests whether an issue matches the given search query.
    /// </summary>
    /// <param name="issue">The issue to test.</param>
    /// <param name="query">The search query.</param>
    /// <returns>True if the issue matches.</returns>
    bool Matches(Issue issue, SearchQuery query);
}
