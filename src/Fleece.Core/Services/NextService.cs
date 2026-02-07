using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Service for finding issues that can be worked on next based on dependencies and execution mode.
/// </summary>
public sealed class NextService(IIssueService issueService) : INextService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Issue>> GetNextIssuesAsync(
        string? parentId = null,
        CancellationToken cancellationToken = default)
    {
        var allIssues = await issueService.GetAllAsync(cancellationToken);
        var issueList = allIssues.ToList();

        if (issueList.Count == 0)
        {
            return [];
        }

        // Build lookup for quick access
        var issueLookup = issueList.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Find actionable issues
        var actionable = issueList
            .Where(issue => IsActionable(issue, issueLookup))
            .ToList();

        // Apply parent filter if specified
        if (!string.IsNullOrWhiteSpace(parentId))
        {
            var descendants = GetDescendantIds(parentId, issueList);
            actionable = actionable.Where(i => descendants.Contains(i.Id)).ToList();
        }

        return actionable;
    }

    /// <summary>
    /// Determines if an issue is actionable (can be worked on next).
    /// </summary>
    private static bool IsActionable(Issue issue, Dictionary<string, Issue> issueLookup)
    {
        // Must be Open status to be actionable
        if (issue.Status != IssueStatus.Open)
        {
            return false;
        }

        // Parent issues with incomplete children cannot be next for completion
        if (HasIncompleteChildren(issue, issueLookup))
        {
            return false;
        }

        // Check if all PreviousIssues are done
        if (!ArePreviousIssuesDone(issue, issueLookup))
        {
            return false;
        }

        // Check if parent's ExecutionMode allows this issue to be worked on
        if (!IsAllowedByParentExecutionMode(issue, issueLookup))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if this issue has any incomplete (non-done) children.
    /// A parent issue with incomplete children cannot be "next" for completion.
    /// </summary>
    private static bool HasIncompleteChildren(Issue issue, Dictionary<string, Issue> issueLookup)
    {
        return issueLookup.Values.Any(i =>
            i.ParentIssues.Any(p =>
                string.Equals(p.ParentIssue, issue.Id, StringComparison.OrdinalIgnoreCase)) &&
            !i.Status.IsDone());
    }

    /// <summary>
    /// Checks if all preceding siblings (in series mode) are done.
    /// This is now handled by IsAllowedByParentExecutionMode using sort order.
    /// </summary>
    private static bool ArePreviousIssuesDone(Issue issue, Dictionary<string, Issue> issueLookup)
    {
        // In the new model, ordering is determined by SortOrder within ParentIssues.
        // The IsAllowedByParentExecutionMode method handles the series ordering logic.
        return true;
    }

    /// <summary>
    /// Checks if this issue is allowed to be worked on based on parent's ExecutionMode.
    /// For Series mode, only the first incomplete child is allowed.
    /// For Parallel mode, all children are allowed.
    /// </summary>
    private static bool IsAllowedByParentExecutionMode(Issue issue, Dictionary<string, Issue> issueLookup)
    {
        if (issue.ParentIssues.Count == 0)
        {
            return true;
        }

        // Check each parent - all parents must allow this issue
        foreach (var parentRef in issue.ParentIssues)
        {
            // If parent doesn't exist, treat as parallel (allow)
            if (!issueLookup.TryGetValue(parentRef.ParentIssue, out var parent))
            {
                continue;
            }

            // For parallel mode, always allowed
            if (parent.ExecutionMode == ExecutionMode.Parallel)
            {
                continue;
            }

            // For series mode, this issue must be the first incomplete child
            if (!IsFirstIncompleteChild(issue, parent, issueLookup))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines if the given issue is the first incomplete child of the parent (in sort order).
    /// Sort order is determined by the SortOrder field in the ParentIssueRef.
    /// </summary>
    private static bool IsFirstIncompleteChild(Issue issue, Issue parent, Dictionary<string, Issue> issueLookup)
    {
        // Get all children of this parent with their sort orders
        var childrenWithSortOrder = issueLookup.Values
            .Select(i => new
            {
                Issue = i,
                ParentRef = i.ParentIssues.FirstOrDefault(p =>
                    string.Equals(p.ParentIssue, parent.Id, StringComparison.OrdinalIgnoreCase))
            })
            .Where(x => x.ParentRef is not null)
            .OrderBy(x => x.ParentRef!.SortOrder, StringComparer.Ordinal)
            .Select(x => x.Issue)
            .ToList();

        // Find the first incomplete child
        var firstIncomplete = childrenWithSortOrder.FirstOrDefault(c => !c.Status.IsDone());

        // This issue is allowed if it's the first incomplete child
        return firstIncomplete != null &&
               string.Equals(firstIncomplete.Id, issue.Id, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets all descendant IDs (children, grandchildren, etc.) of the given parent.
    /// </summary>
    private static HashSet<string> GetDescendantIds(string parentId, List<Issue> allIssues)
    {
        var descendants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toProcess = new Queue<string>();
        toProcess.Enqueue(parentId);

        while (toProcess.Count > 0)
        {
            var currentId = toProcess.Dequeue();
            var children = allIssues
                .Where(i => i.ParentIssues.Any(p =>
                    string.Equals(p.ParentIssue, currentId, StringComparison.OrdinalIgnoreCase)))
                .Select(i => i.Id)
                .ToList();

            foreach (var childId in children)
            {
                if (descendants.Add(childId))
                {
                    toProcess.Enqueue(childId);
                }
            }
        }

        return descendants;
    }
}
