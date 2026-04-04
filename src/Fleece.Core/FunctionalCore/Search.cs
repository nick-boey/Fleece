using Fleece.Core.Models;
using Fleece.Core.Search;

namespace Fleece.Core.FunctionalCore;

/// <summary>
/// Pure, static search operations over in-memory issue collections.
/// </summary>
public static class SearchOps
{
    private static readonly SearchQueryParser Parser = new();

    /// <summary>
    /// Parses a search query string into a SearchQuery.
    /// </summary>
    public static SearchQuery ParseQuery(string? query) => Parser.Parse(query);

    /// <summary>
    /// Tests whether a single issue matches the given search query.
    /// </summary>
    public static bool Matches(Issue issue, SearchQuery query)
    {
        if (query.IsEmpty)
        {
            return true;
        }

        var effectiveFilters = BuildEffectiveFilters(query, null, null, null, null, null, null);
        return MatchesFilters(issue, query, effectiveFilters);
    }

    /// <summary>
    /// Searches issues matching the given query.
    /// </summary>
    public static IReadOnlyList<Issue> Search(
        IReadOnlyList<Issue> issues,
        SearchQuery query,
        bool includeTerminal)
    {
        return SearchWithFilters(issues, query, includeTerminal: includeTerminal);
    }

    /// <summary>
    /// Searches issues matching the given query with CLI filter overrides.
    /// </summary>
    public static IReadOnlyList<Issue> SearchWithFilters(
        IReadOnlyList<Issue> issues,
        SearchQuery query,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false)
    {
        var effectiveFilters = BuildEffectiveFilters(query, status, type, priority, assignedTo, tags, linkedPr);
        var results = new List<Issue>();

        foreach (var issue in issues)
        {
            if (!includeTerminal && issue.Status.IsTerminal())
            {
                continue;
            }

            if (MatchesFilters(issue, query, effectiveFilters))
            {
                results.Add(issue);
            }
        }

        return results;
    }

    /// <summary>
    /// Searches issues with full hierarchy context, returning matched issues and their ancestors for tree display.
    /// </summary>
    public static SearchResult SearchWithContext(
        IReadOnlyList<Issue> issues,
        SearchQuery query,
        IssueStatus? status = null,
        IssueType? type = null,
        int? priority = null,
        string? assignedTo = null,
        IReadOnlyList<string>? tags = null,
        int? linkedPr = null,
        bool includeTerminal = false)
    {
        var issueLookup = issues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        var matchedIssues = SearchWithFilters(issues, query, status, type, priority, assignedTo, tags, linkedPr, includeTerminal);
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

    /// <summary>
    /// Builds effective filters by combining query tokens with CLI overrides.
    /// </summary>
    internal static EffectiveFilters BuildEffectiveFilters(
        SearchQuery query,
        IssueStatus? cliStatus,
        IssueType? cliType,
        int? cliPriority,
        string? cliAssigned,
        IReadOnlyList<string>? cliTags,
        int? cliLinkedPr)
    {
        var filters = new EffectiveFilters();

        foreach (var token in query.Tokens)
        {
            switch (token.Type)
            {
                case SearchTokenType.StatusFilter:
                    if (cliStatus is null)
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
    internal static bool MatchesFilters(Issue issue, SearchQuery query, EffectiveFilters filters)
    {
        if (filters.CliStatus is not null && issue.Status != filters.CliStatus)
        {
            return false;
        }

        if (filters.CliType is not null && issue.Type != filters.CliType)
        {
            return false;
        }

        if (filters.CliPriority is not null && issue.Priority != filters.CliPriority)
        {
            return false;
        }

        if (filters.CliAssigned is not null &&
            !string.Equals(issue.AssignedTo, filters.CliAssigned, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filters.CliTags is not null && filters.CliTags.Count > 0)
        {
            if (!filters.CliTags.All(tag =>
                issue.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))))
            {
                return false;
            }
        }

        if (filters.CliLinkedPr is not null && issue.LinkedPR != filters.CliLinkedPr)
        {
            return false;
        }

        if (filters.StatusFilter is not null)
        {
            if (!MatchesEnumFilter(issue.Status.ToString(), filters.StatusFilter))
            {
                return false;
            }
        }

        if (filters.TypeFilter is not null)
        {
            if (!MatchesEnumFilter(issue.Type.ToString(), filters.TypeFilter))
            {
                return false;
            }
        }

        if (filters.PriorityFilter is not null)
        {
            var priorityStr = issue.Priority?.ToString() ?? "";
            if (!MatchesValueFilter(priorityStr, filters.PriorityFilter))
            {
                return false;
            }
        }

        if (filters.AssignedFilter is not null)
        {
            if (!MatchesValueFilter(issue.AssignedTo ?? "", filters.AssignedFilter))
            {
                return false;
            }
        }

        if (filters.TagFilter is not null)
        {
            if (!MatchesTagFilter(issue.Tags, filters.TagFilter))
            {
                return false;
            }
        }

        if (filters.LinkedPrFilter is not null)
        {
            var prStr = issue.LinkedPR?.ToString() ?? "";
            if (!MatchesValueFilter(prStr, filters.LinkedPrFilter))
            {
                return false;
            }
        }

        if (filters.IdFilter is not null)
        {
            if (!MatchesValueFilter(issue.Id, filters.IdFilter))
            {
                return false;
            }
        }

        foreach (var textToken in filters.TextTokens)
        {
            if (!MatchesTextSearch(issue, textToken))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesEnumFilter(string value, SearchToken filter)
    {
        bool matches = filter.Values.Any(v =>
            string.Equals(value, v, StringComparison.OrdinalIgnoreCase));

        return filter.IsNegated ? !matches : matches;
    }

    private static bool MatchesValueFilter(string value, SearchToken filter)
    {
        bool matches = filter.Values.Any(v =>
            string.Equals(value, v, StringComparison.OrdinalIgnoreCase));

        return filter.IsNegated ? !matches : matches;
    }

    private static bool MatchesTagFilter(IReadOnlyList<string> tags, SearchToken filter)
    {
        bool matches = filter.Values.Any(filterTag =>
            tags.Any(t => string.Equals(t, filterTag, StringComparison.OrdinalIgnoreCase)));

        return filter.IsNegated ? !matches : matches;
    }

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
    /// Holds effective filter state combining query tokens and CLI overrides.
    /// </summary>
    internal sealed class EffectiveFilters
    {
        public SearchToken? StatusFilter { get; set; }
        public SearchToken? TypeFilter { get; set; }
        public SearchToken? PriorityFilter { get; set; }
        public SearchToken? AssignedFilter { get; set; }
        public SearchToken? TagFilter { get; set; }
        public SearchToken? LinkedPrFilter { get; set; }
        public SearchToken? IdFilter { get; set; }
        public List<SearchToken> TextTokens { get; } = [];

        public IssueStatus? CliStatus { get; set; }
        public IssueType? CliType { get; set; }
        public int? CliPriority { get; set; }
        public string? CliAssigned { get; set; }
        public IReadOnlyList<string>? CliTags { get; set; }
        public int? CliLinkedPr { get; set; }
    }
}
