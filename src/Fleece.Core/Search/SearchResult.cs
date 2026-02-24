using Fleece.Core.Models;

namespace Fleece.Core.Search;

/// <summary>
/// The result of a search operation, including matched issues and context issues.
/// </summary>
public sealed record SearchResult
{
    /// <summary>
    /// Issues that directly matched the search query.
    /// </summary>
    public required IReadOnlyList<Issue> MatchedIssues { get; init; }

    /// <summary>
    /// IDs of the matched issues (for quick lookup).
    /// </summary>
    public required IReadOnlySet<string> MatchedIds { get; init; }

    /// <summary>
    /// Context issues (parents/ancestors of matched issues) that are included for hierarchy context.
    /// </summary>
    public required IReadOnlyList<Issue> ContextIssues { get; init; }

    /// <summary>
    /// All issues (matched + context) combined.
    /// </summary>
    public IReadOnlyList<Issue> AllIssues
    {
        get
        {
            var result = new List<Issue>(MatchedIssues.Count + ContextIssues.Count);
            result.AddRange(MatchedIssues);
            result.AddRange(ContextIssues);
            return result;
        }
    }
}
