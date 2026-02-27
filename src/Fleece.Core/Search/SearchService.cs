using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Search;

/// <summary>
/// Service for searching issues using a query DSL.
/// </summary>
public sealed class SearchService : ISearchService
{
    private readonly IIssueService _issueService;
    private readonly SearchQueryParser _parser;

    public SearchService(IIssueService issueService)
    {
        _issueService = issueService;
        _parser = new SearchQueryParser();
    }

    /// <inheritdoc />
    public SearchQuery ParseQuery(string? query) => _parser.Parse(query);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Issue>> SearchAsync(
        SearchQuery query,
        bool includeTerminal = false,
        CancellationToken cancellationToken = default)
    {
        return await SearchWithFiltersAsync(query, includeTerminal: includeTerminal, cancellationToken: cancellationToken);
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
        IReadOnlyList<(string Key, string Value)>? keyedTags = null,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await _issueService.GetAllAsync(cancellationToken);

        // Build effective filters (CLI overrides query)
        var effectiveFilters = BuildEffectiveFilters(query, status, type, priority, assignedTo, tags, linkedPr);

        var results = new List<Issue>();

        foreach (var issue in allIssues)
        {
            // Check terminal status
            if (!includeTerminal && issue.Status.IsTerminal())
            {
                continue;
            }

            // Check all filters
            if (MatchesFilters(issue, query, effectiveFilters))
            {
                // Check keyed tags filter
                if (keyedTags is { Count: > 0 })
                {
                    if (!MatchesKeyedTags(issue.Tags, keyedTags))
                    {
                        continue;
                    }
                }
                results.Add(issue);
            }
        }

        return results;
    }

    /// <summary>
    /// Checks if issue tags contain all specified keyed tags.
    /// </summary>
    private static bool MatchesKeyedTags(IReadOnlyList<string> issueTags, IReadOnlyList<(string Key, string Value)> keyedTags)
    {
        foreach (var (key, value) in keyedTags)
        {
            var expectedTag = $"{key}={value}";
            if (!issueTags.Any(t => string.Equals(t, expectedTag, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }
        return true;
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
        IReadOnlyList<(string Key, string Value)>? keyedTags = null,
        CancellationToken cancellationToken = default)
    {
        // Get all issues for context lookup
        var allIssues = await _issueService.GetAllAsync(cancellationToken);
        var issueLookup = allIssues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Get matched issues
        var matchedIssues = await SearchWithFiltersAsync(
            query, status, type, priority, assignedTo, tags, linkedPr, includeTerminal, keyedTags, cancellationToken);

        var matchedIds = new HashSet<string>(matchedIssues.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);

        // Collect ancestor context
        var contextIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toProcess = new Queue<Issue>(matchedIssues);

        while (toProcess.Count > 0)
        {
            var issue = toProcess.Dequeue();

            foreach (var parentRef in issue.ParentIssues)
            {
                if (issueLookup.TryGetValue(parentRef.ParentIssue, out var parent))
                {
                    if (!matchedIds.Contains(parent.Id) && contextIds.Add(parent.Id))
                    {
                        toProcess.Enqueue(parent);
                    }
                }
            }
        }

        var contextIssues = contextIds.Select(id => issueLookup[id]).ToList();

        return new SearchResult
        {
            MatchedIssues = matchedIssues,
            MatchedIds = matchedIds,
            ContextIssues = contextIssues
        };
    }

    /// <inheritdoc />
    public bool Matches(Issue issue, SearchQuery query)
    {
        if (query.IsEmpty)
        {
            return true;
        }

        var effectiveFilters = BuildEffectiveFilters(query, null, null, null, null, null, null);
        return MatchesFilters(issue, query, effectiveFilters);
    }

    /// <summary>
    /// Builds effective filters by combining query tokens with CLI overrides.
    /// </summary>
    private static EffectiveFilters BuildEffectiveFilters(
        SearchQuery query,
        IssueStatus? cliStatus,
        IssueType? cliType,
        int? cliPriority,
        string? cliAssigned,
        IReadOnlyList<string>? cliTags,
        int? cliLinkedPr)
    {
        var filters = new EffectiveFilters();

        // Extract filters from query
        foreach (var token in query.Tokens)
        {
            switch (token.Type)
            {
                case SearchTokenType.StatusFilter:
                    if (cliStatus is null) // CLI doesn't override
                    {
                        filters.StatusFilter = token;
                    }
                    break;

                case SearchTokenType.TypeFilter:
                    if (cliType is null)
                    {
                        filters.TypeFilter = token;
                    }
                    break;

                case SearchTokenType.PriorityFilter:
                    if (cliPriority is null)
                    {
                        filters.PriorityFilter = token;
                    }
                    break;

                case SearchTokenType.AssignedFilter:
                    if (cliAssigned is null)
                    {
                        filters.AssignedFilter = token;
                    }
                    break;

                case SearchTokenType.TagFilter:
                    if (cliTags is null)
                    {
                        filters.TagFilter = token;
                    }
                    break;

                case SearchTokenType.LinkedPrFilter:
                    if (cliLinkedPr is null)
                    {
                        filters.LinkedPrFilter = token;
                    }
                    break;

                case SearchTokenType.IdFilter:
                    filters.IdFilter = token;
                    break;

                case SearchTokenType.Text:
                    filters.TextTokens.Add(token);
                    break;
            }
        }

        // Apply CLI overrides
        if (cliStatus is not null)
        {
            filters.CliStatus = cliStatus;
        }
        if (cliType is not null)
        {
            filters.CliType = cliType;
        }
        if (cliPriority is not null)
        {
            filters.CliPriority = cliPriority;
        }
        if (cliAssigned is not null)
        {
            filters.CliAssigned = cliAssigned;
        }
        if (cliTags is not null)
        {
            filters.CliTags = cliTags;
        }
        if (cliLinkedPr is not null)
        {
            filters.CliLinkedPr = cliLinkedPr;
        }

        return filters;
    }

    /// <summary>
    /// Tests whether an issue matches the given filters.
    /// </summary>
    private static bool MatchesFilters(Issue issue, SearchQuery query, EffectiveFilters filters)
    {
        // Check CLI status override
        if (filters.CliStatus is not null && issue.Status != filters.CliStatus)
        {
            return false;
        }

        // Check CLI type override
        if (filters.CliType is not null && issue.Type != filters.CliType)
        {
            return false;
        }

        // Check CLI priority override
        if (filters.CliPriority is not null && issue.Priority != filters.CliPriority)
        {
            return false;
        }

        // Check CLI assigned override
        if (filters.CliAssigned is not null &&
            !string.Equals(issue.AssignedTo, filters.CliAssigned, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check CLI tags override
        if (filters.CliTags is not null && filters.CliTags.Count > 0)
        {
            if (!filters.CliTags.All(tag =>
                issue.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))))
            {
                return false;
            }
        }

        // Check CLI linked PR override
        if (filters.CliLinkedPr is not null && issue.LinkedPR != filters.CliLinkedPr)
        {
            return false;
        }

        // Check query filters (when CLI doesn't override)

        // Status filter from query
        if (filters.StatusFilter is not null)
        {
            if (!MatchesEnumFilter(issue.Status.ToString(), filters.StatusFilter))
            {
                return false;
            }
        }

        // Type filter from query
        if (filters.TypeFilter is not null)
        {
            if (!MatchesEnumFilter(issue.Type.ToString(), filters.TypeFilter))
            {
                return false;
            }
        }

        // Priority filter from query
        if (filters.PriorityFilter is not null)
        {
            var priorityStr = issue.Priority?.ToString() ?? "";
            if (!MatchesValueFilter(priorityStr, filters.PriorityFilter))
            {
                return false;
            }
        }

        // Assigned filter from query
        if (filters.AssignedFilter is not null)
        {
            if (!MatchesValueFilter(issue.AssignedTo ?? "", filters.AssignedFilter))
            {
                return false;
            }
        }

        // Tag filter from query
        if (filters.TagFilter is not null)
        {
            if (!MatchesTagFilter(issue.Tags, filters.TagFilter))
            {
                return false;
            }
        }

        // Linked PR filter from query
        if (filters.LinkedPrFilter is not null)
        {
            var prStr = issue.LinkedPR?.ToString() ?? "";
            if (!MatchesValueFilter(prStr, filters.LinkedPrFilter))
            {
                return false;
            }
        }

        // ID filter from query
        if (filters.IdFilter is not null)
        {
            if (!MatchesValueFilter(issue.Id, filters.IdFilter))
            {
                return false;
            }
        }

        // Text search (AND logic between text tokens)
        foreach (var textToken in filters.TextTokens)
        {
            if (!MatchesTextSearch(issue, textToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Matches an enum value against a filter token.
    /// </summary>
    private static bool MatchesEnumFilter(string value, SearchToken filter)
    {
        // Check if any value matches
        bool matches = filter.Values.Any(v =>
            string.Equals(value, v, StringComparison.OrdinalIgnoreCase));

        return filter.IsNegated ? !matches : matches;
    }

    /// <summary>
    /// Matches a string value against a filter token.
    /// </summary>
    private static bool MatchesValueFilter(string value, SearchToken filter)
    {
        bool matches = filter.Values.Any(v =>
            string.Equals(value, v, StringComparison.OrdinalIgnoreCase));

        return filter.IsNegated ? !matches : matches;
    }

    /// <summary>
    /// Matches tags against a filter token.
    /// </summary>
    private static bool MatchesTagFilter(IReadOnlyList<string> tags, SearchToken filter)
    {
        // At least one of the filter values must match at least one tag
        bool matches = filter.Values.Any(filterTag =>
            tags.Any(t => string.Equals(t, filterTag, StringComparison.OrdinalIgnoreCase)));

        return filter.IsNegated ? !matches : matches;
    }

    /// <summary>
    /// Matches an issue against a text search token.
    /// Searches title, description, and tags.
    /// </summary>
    private static bool MatchesTextSearch(Issue issue, SearchToken token)
    {
        var searchText = token.Values[0];

        bool matches =
            issue.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            (issue.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            issue.Tags.Any(t => t.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
            issue.Id.Contains(searchText, StringComparison.OrdinalIgnoreCase);

        return token.IsNegated ? !matches : matches;
    }

    /// <summary>
    /// Internal class to hold effective filter state.
    /// </summary>
    private sealed class EffectiveFilters
    {
        // Query filters
        public SearchToken? StatusFilter { get; set; }
        public SearchToken? TypeFilter { get; set; }
        public SearchToken? PriorityFilter { get; set; }
        public SearchToken? AssignedFilter { get; set; }
        public SearchToken? TagFilter { get; set; }
        public SearchToken? LinkedPrFilter { get; set; }
        public SearchToken? IdFilter { get; set; }
        public List<SearchToken> TextTokens { get; } = [];

        // CLI overrides
        public IssueStatus? CliStatus { get; set; }
        public IssueType? CliType { get; set; }
        public int? CliPriority { get; set; }
        public string? CliAssigned { get; set; }
        public IReadOnlyList<string>? CliTags { get; set; }
        public int? CliLinkedPr { get; set; }
    }
}
