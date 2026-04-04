using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Search;

/// <summary>
/// Service for searching issues using a query DSL.
/// </summary>
public sealed class SearchService : ISearchService
{
    private readonly IIssueService _issueService;

    public SearchService(IIssueService issueService)
    {
        _issueService = issueService;
    }

    /// <inheritdoc />
    public SearchQuery ParseQuery(string? query) => SearchOps.ParseQuery(query);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Issue>> SearchAsync(
        SearchQuery query,
        bool includeTerminal = false,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await _issueService.GetAllAsync(cancellationToken);
        return SearchOps.Search(allIssues, query, includeTerminal);
    }

    /// <inheritdoc />
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
        var allIssues = await _issueService.GetAllAsync(cancellationToken);
        return SearchOps.SearchWithFilters(allIssues, query, status, type, priority, assignedTo, tags, linkedPr, includeTerminal);
    }

    /// <inheritdoc />
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
        var allIssues = await _issueService.GetAllAsync(cancellationToken);
        return SearchOps.SearchWithContext(allIssues, query, status, type, priority, assignedTo, tags, linkedPr, includeTerminal);
    }

    /// <inheritdoc />
    public bool Matches(Issue issue, SearchQuery query) => SearchOps.Matches(issue, query);
}
