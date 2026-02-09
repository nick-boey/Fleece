using Fleece.Core.Models;

namespace Fleece.Cli.Output;

/// <summary>
/// Builds hierarchy context for the show command, computing parent details,
/// series position, sibling navigation, and child listings.
/// </summary>
public static class IssueHierarchyHelper
{
    /// <summary>
    /// Builds an <see cref="IssueShowDto"/> with enriched hierarchy context for the given issue.
    /// </summary>
    public static IssueShowDto BuildShowContext(Issue issue, IReadOnlyList<Issue> allIssues)
    {
        var issueLookup = allIssues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        var parents = BuildParentContexts(issue, issueLookup, allIssues);
        var children = BuildChildList(issue, allIssues);

        return new IssueShowDto
        {
            Issue = IssueDto.FromIssue(issue),
            Parents = parents,
            Children = children,
            ExecutionMode = issue.ExecutionMode
        };
    }

    private static IReadOnlyList<ParentContextDto> BuildParentContexts(
        Issue issue,
        Dictionary<string, Issue> issueLookup,
        IReadOnlyList<Issue> allIssues)
    {
        if (issue.ParentIssues is null || issue.ParentIssues.Count == 0)
        {
            return [];
        }

        var result = new List<ParentContextDto>();

        foreach (var parentRef in issue.ParentIssues)
        {
            if (!issueLookup.TryGetValue(parentRef.ParentIssue, out var parentIssue))
            {
                continue;
            }

            var parentSummary = IssueSummaryDto.FromIssue(parentIssue);

            // Get sorted siblings under this parent (same sorting as TreeCommand)
            var siblings = GetSortedChildren(parentIssue.Id, allIssues);

            if (parentIssue.ExecutionMode == ExecutionMode.Parallel)
            {
                result.Add(new ParentContextDto
                {
                    Parent = parentSummary,
                    ExecutionMode = ExecutionMode.Parallel
                });
            }
            else
            {
                // Series mode: compute position and prev/next
                var position = siblings.FindIndex(s =>
                    s.Id.Equals(issue.Id, StringComparison.OrdinalIgnoreCase));

                IssueSummaryDto? previousSibling = position > 0
                    ? IssueSummaryDto.FromIssue(siblings[position - 1])
                    : null;

                IssueSummaryDto? nextSibling = position >= 0 && position < siblings.Count - 1
                    ? IssueSummaryDto.FromIssue(siblings[position + 1])
                    : null;

                result.Add(new ParentContextDto
                {
                    Parent = parentSummary,
                    ExecutionMode = ExecutionMode.Series,
                    Position = position >= 0 ? position + 1 : null, // 1-based
                    TotalSiblings = siblings.Count,
                    PreviousSibling = previousSibling,
                    NextSibling = nextSibling
                });
            }
        }

        return result;
    }

    private static IReadOnlyList<IssueSummaryDto> BuildChildList(Issue issue, IReadOnlyList<Issue> allIssues)
    {
        var children = GetSortedChildren(issue.Id, allIssues);
        return children.Select(IssueSummaryDto.FromIssue).ToList();
    }

    /// <summary>
    /// Gets children of a given parent issue, sorted by SortOrder → Priority → Title.
    /// This matches the sorting logic in TreeCommand.
    /// </summary>
    internal static List<Issue> GetSortedChildren(string parentId, IReadOnlyList<Issue> allIssues)
    {
        return allIssues
            .Where(i => i.ParentIssues?.Any(p =>
                p.ParentIssue.Equals(parentId, StringComparison.OrdinalIgnoreCase)) ?? false)
            .Select(i => new
            {
                Issue = i,
                SortOrder = i.ParentIssues?.FirstOrDefault(p =>
                    p.ParentIssue.Equals(parentId, StringComparison.OrdinalIgnoreCase))?.SortOrder ?? "zzz"
            })
            .OrderBy(x => x.SortOrder, StringComparer.Ordinal)
            .ThenBy(x => x.Issue.Priority ?? 99)
            .ThenBy(x => x.Issue.Title)
            .Select(x => x.Issue)
            .ToList();
    }
}
